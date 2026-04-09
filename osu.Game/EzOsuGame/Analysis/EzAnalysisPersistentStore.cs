// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 本地持久化的 mania analysis 存储。
    ///
    /// 目标：对齐官方“有版本号、可增量补齐”的后台预处理体验，但不写回主谱面库的 Realm（client.realm），
    /// 以降低误操作/迁移导致主库损坏的风险。
    ///
    /// 存储键：BeatmapInfo.ID（Guid）+ BeatmapInfo.Hash（SHA-256）。
    /// - 只要 beatmap 内容变化（Hash 变化），对应条目会自动失效并重算。
    /// - AnalysisVersion 用于你这边算法变更时整体失效。
    ///
    /// 注意：此处使用 SQLite（而不是额外 Realm 文件），因为向 osu.Game 程序集新增 RealmObject 类型
    /// 会改变 client.realm 的 schema 并要求迁移；而 SQLite 独立文件更安全、易恢复。
    /// </summary>
    public class EzAnalysisPersistentStore
    {
        public readonly record struct SongsBranchRow(Guid BeatmapId, string BeatmapHash, string BeatmapMd5, double XxySr);

        public readonly record struct SongsBranchDescriptor(string DatabasePath, string RelativePath, SongsBranchMetadata Metadata);

        public readonly record struct SourceCollectionSnapshot(
            Guid CollectionId,
            string Name,
            long LastModifiedUnixMilliseconds,
            IReadOnlyList<string> BeatmapMd5Hashes);

        public readonly record struct SongsBranchMetadata(
            int RulesetOnlineId,
            string RulesetShortName,
            string ModsFingerprint,
            string ModsDisplay,
            int BeatmapCount,
            long CreatedAtUnixMilliseconds,
            string DisplayName,
            string ModsJson = "",
            bool HiddenApplied = false,
            Guid SourceCollectionId = default,
            string SourceCollectionName = "",
            long SourceCollectionLastModifiedUnixMilliseconds = 0,
            int SourceCollectionBeatmapCount = 0);

        /// <summary>
        /// 持久化总开关（默认关闭）：未来考虑是否允许用户通过配置关闭此功能以避免额外的磁盘读写。
        /// </summary>
        public static bool Enabled = true;

        public static readonly string DATABASE_FILENAME = $@"mania-analysis_v{ANALYSIS_VERSION}.sqlite";

        public const string SONGS_BRANCH_DATABASE_DIRECTORY = "EzData";
        private const string xxy_sr_branch_kind = "xxy_sr_branch";
        private const int xxy_sr_branch_schema_version = 1;

        // 手动维护：算法/序列化格式变更时递增。版本发生变化时，会强制重算所有已存条目。
        // 注意：此版本号与 osu! 官方服务器端的版本号无关，仅用于本地持久化存储的失效控制。
        // 注意：更新版本号后，务必通过注释保存旧版本的变更记录，方便日后排查问题。
        // v2: 初始版本，包含 kps_list_json, column_counts_json
        // v3: 添加 hold_note_counts_json 字段，分离普通note和长按note统计
        // v4: 添加 beatmap_md5 校验字段；kps_list_json 仅保存用于 UI 的下采样曲线（<=256 点）。
        // v5: 删除scratchText存储，改为动态计算。数据库可兼容，不升版。
        public const int ANALYSIS_VERSION = 5;

        private static readonly string[] allowed_columns =
        {
            "beatmap_id",
            "beatmap_hash",
            "beatmap_md5",
            "analysis_version",
            "average_kps",
            "max_kps",
            "kps_list_json",
            "xxy_sr",
            "column_counts_json",
            "hold_note_counts_json",
            "last_updated"
        };

        private readonly Storage storage;
        private readonly object initLock = new object();
        private static readonly IReadOnlyDictionary<Guid, double> empty_xxy_sr_values = new Dictionary<Guid, double>();

        private bool initialised;
        private string dbPath = string.Empty;

        private record PendingWrite(BeatmapInfo Beatmap, EzAnalysisResult Analysis, long Timestamp);

        private readonly ConcurrentDictionary<Guid, PendingWrite> pendingWrites = new ConcurrentDictionary<Guid, PendingWrite>();
        private CancellationTokenSource? writeCts;
        private Task? backgroundWriterTask;

        // Old versions earlier than v3 may not have sufficient data to safely upgrade without recomputation.
        // v3 introduced hold note counts, which are relied upon by parts of the UI.
        private const int min_inplace_upgrade_version = 3;

        public EzAnalysisPersistentStore(Storage storage)
        {
            this.storage = storage;
        }

        public void Initialise()
        {
            if (!Enabled)
                return;

            lock (initLock)
            {
                if (initialised)
                    return;

                dbPath = storage.GetFullPath(DATABASE_FILENAME, true);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

                    // If this is a new versioned DB file, attempt to clone from the latest previous version to avoid
                    // forcing a full recompute (when changes are only schema/serialization related).
                    tryClonePreviousDatabaseIfMissing();

                    Logger.Log($"EzManiaAnalysisPersistentStore path: {dbPath}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

                    using var connection = openConnection();

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA temp_store=MEMORY;
";
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS mania_analysis (
    beatmap_id TEXT PRIMARY KEY,
    beatmap_hash TEXT NOT NULL,
    beatmap_md5 TEXT NOT NULL,
    analysis_version INTEGER NOT NULL,
    average_kps REAL NOT NULL,
    max_kps REAL NOT NULL,
    kps_list_json TEXT NOT NULL,
    xxy_sr REAL NULL,
    column_counts_json TEXT NOT NULL,
    hold_note_counts_json TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_mania_analysis_version ON mania_analysis(analysis_version);
";
                        cmd.ExecuteNonQuery();
                    }

                    ensureCollectionHideTables(connection);

                    // 从旧版本平滑升级：如果缺少列则补齐（SQLite 不支持 IF NOT EXISTS 语法的 ADD COLUMN）。
                    if (!hasColumn(connection, "mania_analysis", "kps_list_json"))
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.CommandText = "ALTER TABLE mania_analysis ADD COLUMN kps_list_json TEXT NOT NULL DEFAULT '[]';";
                        cmd.ExecuteNonQuery();
                    }

                    if (!hasColumn(connection, "mania_analysis", "hold_note_counts_json"))
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.CommandText = "ALTER TABLE mania_analysis ADD COLUMN hold_note_counts_json TEXT NOT NULL DEFAULT '{}';";
                        cmd.ExecuteNonQuery();
                    }

                    if (!hasColumn(connection, "mania_analysis", "beatmap_md5"))
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.CommandText = "ALTER TABLE mania_analysis ADD COLUMN beatmap_md5 TEXT NOT NULL DEFAULT '';";
                        cmd.ExecuteNonQuery();
                    }

                    // Store the current analysis version as meta (informational).
                    setMeta(connection, "analysis_version", ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));

                    // Ensure last_updated column exists for bookkeeping (minimal schema extension).
                    if (!hasColumn(connection, "mania_analysis", "last_updated"))
                    {
                        using var add = connection.CreateCommand();
                        add.CommandText = "ALTER TABLE mania_analysis ADD COLUMN last_updated INTEGER NOT NULL DEFAULT 0;";
                        add.ExecuteNonQuery();
                    }

                    // 检查并清理不需要的列（处理版本升级时删除的字段）
                    cleanupUnrecognizedColumns(connection);

                    initialised = true;

                    // Start background writer for pending writes.
                    startBackgroundWriter();
                }
                catch (Exception e)
                {
                    // 如果数据库损坏/无法打开：不影响游戏运行；尝试备份并重新创建。
                    Logger.Error(e, "EzManiaAnalysisPersistentStore failed to initialise; recreating database.");

                    try
                    {
                        if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
                        {
                            string backup = dbPath + ".bak";
                            File.Copy(dbPath, backup, overwrite: true);
                            File.Delete(dbPath);
                        }
                    }
                    catch
                    {
                        // ignored
                    }

                    // Second attempt.
                    initialised = false;
                    Initialise();
                }
            }
        }

        public bool TryGet(BeatmapInfo beatmap, out EzAnalysisResult result)
        {
            result = default;

            if (!Enabled)
                return false;

            try
            {
                Initialise();

                // If we have a pending write for this beatmap, prefer that (latest in-memory result).
                if (pendingWrites.TryGetValue(beatmap.ID, out var pending))
                {
                    // Basic validation against hash to avoid returning stale pending for different beatmap content.
                    if (string.Equals(pending.Beatmap.Hash, beatmap.Hash, StringComparison.Ordinal))
                    {
                        result = pending.Analysis;
                        return true;
                    }
                    // otherwise fall through to DB lookup
                }

                using var connection = openConnection();

                string storedHash;
                string storedMd5;
                int storedVersion;
                double averageKps;
                double maxKps;
                string kpsListJson;
                double? xxySr;
                string columnCountsJson;
                string holdNoteCountsJson;

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT beatmap_hash, beatmap_md5, analysis_version, average_kps, max_kps, kps_list_json, xxy_sr, column_counts_json, hold_note_counts_json
FROM mania_analysis
WHERE beatmap_id = $id
LIMIT 1;
";
                    cmd.Parameters.AddWithValue("$id", beatmap.ID.ToString());

                    using var reader = cmd.ExecuteReader();

                    if (!reader.Read())
                        return false;

                    storedHash = reader.GetString(0);
                    storedMd5 = reader.GetString(1);
                    storedVersion = reader.GetInt32(2);
                    averageKps = reader.GetDouble(3);
                    maxKps = reader.GetDouble(4);
                    kpsListJson = reader.GetString(5);
                    xxySr = reader.IsDBNull(6) ? null : reader.GetDouble(6);
                    columnCountsJson = reader.GetString(7);
                    holdNoteCountsJson = reader.GetString(8);
                }

                if (!string.Equals(storedHash, beatmap.Hash, StringComparison.Ordinal))
                {
                    Logger.Log($"[EzManiaAnalysisPersistentStore] stored_hash mismatch for {beatmap.ID}: stored={storedHash} runtime={beatmap.Hash}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                    return false;
                }

                // md5 validation:
                // - If stored md5 is empty (older versions), accept hash match and upgrade in-place.
                // - If stored md5 is present, require it to match.
                if (!string.IsNullOrEmpty(storedMd5) && !string.Equals(storedMd5, beatmap.MD5Hash, StringComparison.Ordinal))
                {
                    Logger.Log($"[EzManiaAnalysisPersistentStore] stored_md5 mismatch for {beatmap.ID}: stored={storedMd5} runtime={beatmap.MD5Hash}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                    return false;
                }

                // If the stored version is newer than this build, ignore and let caller recompute.
                if (storedVersion > ANALYSIS_VERSION)
                    return false;

                var columnCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(columnCountsJson) ?? new Dictionary<int, int>();
                var holdNoteCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(holdNoteCountsJson) ?? new Dictionary<int, int>();
                var kpsList = JsonSerializer.Deserialize<List<double>>(kpsListJson) ?? new List<double>();

                // Allow in-place upgrade of compatible older entries to avoid full recompute.
                // If an older version is not compatible, treat it as a miss.
                if (storedVersion != ANALYSIS_VERSION)
                {
                    if (!canUpgradeInPlace(storedVersion))
                        return false;

                    bool mutated = string.IsNullOrEmpty(storedMd5);

                    // v4: store md5 for extra safety (hash already guards real content).

                    // v4: kps_list_json is UI graph only; keep it capped for perf.
                    if (kpsList.Count > OptimizedBeatmapCalculator.DEFAULT_KPS_GRAPH_POINTS)
                    {
                        kpsList = OptimizedBeatmapCalculator.DownsampleToFixedCount(kpsList, OptimizedBeatmapCalculator.DEFAULT_KPS_GRAPH_POINTS);
                        mutated = true;
                    }

                    if (mutated)
                    {
                        // Persist the upgraded row (without recomputing analysis).
                        writeUpgradedRow(connection, beatmap, averageKps, maxKps, kpsList, xxySr, columnCounts, holdNoteCounts);
                    }
                }

                result = new EzAnalysisResult(EzCommonAnalysisAttributes.Create(averageKps, maxKps, kpsList), EzManiaAnalysisAttributes.Create(columnCounts, holdNoteCounts, xxySr));

                // Validate the analysis result to ensure it's reasonable
                if (!isValidAnalysisResult(result))
                {
                    Logger.Log($"[EzManiaAnalysisPersistentStore] Invalid analysis result for {beatmap.ID}, ignoring cached data.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                    return false;
                }

                // missingRequiredXxySr = requireXxySr && xxySr == null;

                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TryGet failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        public IReadOnlyDictionary<Guid, double> GetStoredXxySrValues(IEnumerable<BeatmapInfo> beatmaps)
        {
            if (!Enabled)
                return empty_xxy_sr_values;

            try
            {
                Initialise();

                var beatmapList = beatmaps.Distinct().ToList();

                if (beatmapList.Count == 0)
                    return empty_xxy_sr_values;

                var beatmapsById = beatmapList.ToDictionary(b => b.ID);
                var resolvedValues = new Dictionary<Guid, double>(beatmapList.Count);
                var idsNeedingDatabaseLookup = new List<Guid>(beatmapList.Count);

                foreach (var beatmap in beatmapList)
                {
                    if (pendingWrites.TryGetValue(beatmap.ID, out var pending)
                        && string.Equals(pending.Beatmap.Hash, beatmap.Hash, StringComparison.Ordinal))
                    {
                        if (pending.Analysis.ManiaAttributes?.XxySr is double pendingXxySr)
                            resolvedValues[beatmap.ID] = pendingXxySr;

                        continue;
                    }

                    idsNeedingDatabaseLookup.Add(beatmap.ID);
                }

                if (idsNeedingDatabaseLookup.Count == 0)
                    return resolvedValues;

                using var connection = openConnection();

                for (int offset = 0; offset < idsNeedingDatabaseLookup.Count; offset += 800)
                {
                    using var cmd = connection.CreateCommand();

                    int batchCount = Math.Min(800, idsNeedingDatabaseLookup.Count - offset);
                    var parameterNames = new List<string>(batchCount);

                    for (int i = 0; i < batchCount; i++)
                    {
                        string parameterName = $"$id{i}";
                        parameterNames.Add(parameterName);
                        cmd.Parameters.AddWithValue(parameterName, idsNeedingDatabaseLookup[offset + i].ToString());
                    }

                    cmd.CommandText = $@"
SELECT beatmap_id, beatmap_hash, beatmap_md5, analysis_version, xxy_sr
FROM mania_analysis
WHERE beatmap_id IN ({string.Join(", ", parameterNames)})
  AND xxy_sr IS NOT NULL;
";

                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        if (!Guid.TryParse(reader.GetString(0), out var beatmapId))
                            continue;

                        if (!beatmapsById.TryGetValue(beatmapId, out var beatmap))
                            continue;

                        string storedHash = reader.GetString(1);
                        string storedMd5 = reader.GetString(2);
                        int storedVersion = reader.GetInt32(3);

                        if (!string.Equals(storedHash, beatmap.Hash, StringComparison.Ordinal))
                            continue;

                        if (!string.IsNullOrEmpty(storedMd5) && !string.Equals(storedMd5, beatmap.MD5Hash, StringComparison.Ordinal))
                            continue;

                        if (storedVersion > ANALYSIS_VERSION)
                            continue;

                        if (storedVersion != ANALYSIS_VERSION && !canUpgradeInPlace(storedVersion))
                            continue;

                        resolvedValues[beatmapId] = reader.GetDouble(4);
                    }
                }

                return resolvedValues;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetStoredXxySrValues failed.", Ez2ConfigManager.LOGGER_NAME);
                return empty_xxy_sr_values;
            }
        }

        /// <summary>
        /// 对比新计算结果和 SQLite 中的旧数据，如果有差异则更新。
        /// 主要场景：
        /// - xxysr 从 null 补算成有值（mania 模式的谱面被重新计算）
        /// - KPS 数据有显著变化（算法修复等）
        /// 工作机制：
        /// - 如果 stored 数据不存在，直接存储新数据
        /// - 如果 stored xxysr == null 而 computed 有值，说明需要补充 xxysr，更新
        /// - 如果都是 xxysr == null，说明是非 mania 模式数据，比较 KPS 数据是否相同
        /// </summary>
        public void StoreIfDifferent(BeatmapInfo beatmap, EzAnalysisResult analysis)
        {
            if (!Enabled)
                return;

            var commonAttributes = analysis.CommonAttributes;

            // Validate the analysis result before storing
            if (!isValidAnalysisResult(analysis))
            {
                Logger.Log($"[EzManiaAnalysisPersistentStore] Refusing to store invalid analysis result for {beatmap.ID}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                return;
            }

            // 跳过空谱面（no notes）- 不需要存储和管理。
            // KPS 是通用分析数据，因此这里以通用 KPS 列表是否为空作为存储门槛。
            if (commonAttributes == null || commonAttributes.KpsList.Count == 0)
                return;

            try
            {
                Initialise();

                using var connection = openConnection();

                // 尝试从 SQLite 读取旧数据
                if (!tryGetRawData(connection, beatmap, out var storedAnalysis))
                {
                    // 缓存不存在，直接存储
                    Store(beatmap, analysis);
                    return;
                }

                // 对比两个结果是否有差异
                if (hasDifference(storedAnalysis, analysis))
                {
                    Logger.Log($"[EzManiaAnalysisPersistentStore] Data difference detected for {beatmap.ID}, updating SQLite.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                    Store(beatmap, analysis);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore StoreIfDifferent failed.");
            }
        }

        /// <summary>
        /// 从数据库读取原始数据（不验证 hash/version）。
        /// </summary>
        private bool tryGetRawData(SqliteConnection connection, BeatmapInfo beatmap, out EzAnalysisResult result)
        {
            result = default;

            try
            {
                // Check pending writes first to ensure we return the freshest data even if not flushed.
                if (pendingWrites.TryGetValue(beatmap.ID, out var pending))
                {
                    if (string.Equals(pending.Beatmap.Hash, beatmap.Hash, StringComparison.Ordinal))
                    {
                        result = pending.Analysis;
                        return true;
                    }
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT average_kps, max_kps, kps_list_json, xxy_sr, column_counts_json, hold_note_counts_json
FROM mania_analysis
WHERE beatmap_id = $id
LIMIT 1;
";
                    cmd.Parameters.AddWithValue("$id", beatmap.ID.ToString());

                    using var reader = cmd.ExecuteReader();

                    if (!reader.Read())
                        return false;

                    double averageKps = reader.GetDouble(0);
                    double maxKps = reader.GetDouble(1);
                    string kpsListJson = reader.GetString(2);
                    double? xxySr = reader.IsDBNull(3) ? null : reader.GetDouble(3);
                    string columnCountsJson = reader.GetString(4);
                    string holdNoteCountsJson = reader.GetString(5);

                    var columnCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(columnCountsJson) ?? new Dictionary<int, int>();
                    var holdNoteCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(holdNoteCountsJson) ?? new Dictionary<int, int>();
                    var kpsList = JsonSerializer.Deserialize<List<double>>(kpsListJson) ?? new List<double>();

                    result = new EzAnalysisResult(EzCommonAnalysisAttributes.Create(averageKps, maxKps, kpsList), EzManiaAnalysisAttributes.Create(columnCounts, holdNoteCounts, xxySr));

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 比较两个分析结果是否有差异。
        /// 关键字段：xxysr, averageKps, maxKps, ColumnCounts, HoldNoteCounts
        /// </summary>
        private bool hasDifference(EzAnalysisResult stored, EzAnalysisResult computed)
        {
            var storedCommonAttributes = stored.CommonAttributes;
            var computedCommonAttributes = computed.CommonAttributes;
            var storedManiaAttributes = stored.ManiaAttributes;
            var computedManiaAttributes = computed.ManiaAttributes;
            double? storedXxySr = storedManiaAttributes?.XxySr;
            double? computedXxySr = computedManiaAttributes?.XxySr;

            // 检查 xxysr 差异（最重要）
            // 如果 stored 是 null 而 computed 有值，必须更新
            if (!storedXxySr.HasValue && computedXxySr.HasValue)
                return true;

            // 如果都有值，比较数值是否相同
            if (storedXxySr.HasValue && computedXxySr.HasValue)
            {
                if (!storedXxySr.Value.Equals(computedXxySr.Value))
                    return true;
            }

            // 检查 KPS 相关数据
            if (!stored.AverageKps.Equals(computed.AverageKps) || !stored.MaxKps.Equals(computed.MaxKps))
                return true;

            if (!(storedCommonAttributes?.KpsList ?? Array.Empty<double>()).SequenceEqual(computedCommonAttributes?.KpsList ?? Array.Empty<double>()))
                return true;

            var storedColumnCounts = storedManiaAttributes?.ColumnCounts ?? new Dictionary<int, int>();
            var computedColumnCounts = computedManiaAttributes?.ColumnCounts ?? new Dictionary<int, int>();

            // 检查列统计
            if (storedColumnCounts.Count != computedColumnCounts.Count)
                return true;

            foreach (var kvp in computedColumnCounts)
            {
                if (!storedColumnCounts.TryGetValue(kvp.Key, out int storedCount) || storedCount != kvp.Value)
                    return true;
            }

            var storedHoldNoteCounts = storedManiaAttributes?.HoldNoteCounts ?? new Dictionary<int, int>();
            var computedHoldNoteCounts = computedManiaAttributes?.HoldNoteCounts ?? new Dictionary<int, int>();

            // 检查长按统计
            if (storedHoldNoteCounts.Count != computedHoldNoteCounts.Count)
                return true;

            foreach (var kvp in computedHoldNoteCounts)
            {
                if (!storedHoldNoteCounts.TryGetValue(kvp.Key, out int storedCount) || storedCount != kvp.Value)
                    return true;
            }

            return false;
        }

        public void Store(BeatmapInfo beatmap, EzAnalysisResult analysis)
        {
            if (!Enabled)
                return;

            var commonAttributes = analysis.CommonAttributes;

            // Validate the analysis result before storing
            if (!isValidAnalysisResult(analysis))
            {
                Logger.Log($"[EzManiaAnalysisPersistentStore] Refusing to store invalid analysis result for {beatmap.ID}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                return;
            }

            // 跳过空谱面（no notes）- 不需要存储和管理。
            // 非 mania/common-only 结果也应允许持久化，因此不再依赖 mania 列统计是否存在。
            if (commonAttributes == null || commonAttributes.KpsList.Count == 0)
                return;

            // Enqueue pending write and return quickly. Background writer will flush to SQLite.
            try
            {
                Initialise();

                var pending = new PendingWrite(beatmap, analysis, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                pendingWrites[beatmap.ID] = pending;
            }
            catch (Exception e)
            {
                // If enqueue fails for some reason, fallback to synchronous write to avoid data loss.
                Logger.Error(e, "EzManiaAnalysisPersistentStore enqueue Store failed, falling back to sync write.");

                try
                {
                    // synchronous fallback
                    Initialise();
                    using var connection = openConnection();
                    writePendingEntryToConnection(connection, beatmap, analysis);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "EzManiaAnalysisPersistentStore Store fallback failed.");
                }
            }
        }

        public IReadOnlyList<Guid> GetBeatmapsNeedingRecompute(IEnumerable<(Guid id, string hash)> beatmaps) => GetBeatmapsNeedingRecompute(beatmaps, progress: null);

        public IReadOnlyList<Guid> GetBeatmapsNeedingRecompute(IEnumerable<(Guid id, string hash)> beatmaps, Action<int, int>? progress)
        {
            if (!Enabled)
                return Array.Empty<Guid>();

            try
            {
                Initialise();

                var beatmapList = beatmaps as IList<(Guid id, string hash)> ?? beatmaps.ToList();

                // 读出已有条目（id -> (hash, version)）。
                Dictionary<Guid, (string hash, int version)> existing = new Dictionary<Guid, (string hash, int version)>();

                using (var connection = openConnection())
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"SELECT beatmap_id, beatmap_hash, analysis_version FROM mania_analysis;";

                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        if (!Guid.TryParse(reader.GetString(0), out var id))
                            continue;

                        string storedHash = reader.GetString(1);
                        int storedVersion = reader.GetInt32(2);
                        existing[id] = (storedHash, storedVersion);
                    }
                }

                List<Guid> needing = new List<Guid>();

                int processed = 0;
                int total = beatmapList.Count;

                foreach (var (id, hash) in beatmapList)
                {
                    processed++;

                    if (processed == 1 || processed % 200 == 0)
                        progress?.Invoke(processed, total);

                    if (!existing.TryGetValue(id, out var row))
                    {
                        needing.Add(id);
                        continue;
                    }

                    // Only force recompute on:
                    // - missing entries
                    // - hash mismatch (beatmap changed)
                    // - versions which cannot be upgraded in-place.
                    // Version bumps which are only schema/serialization should be upgraded lazily on TryGet().
                    if (!string.Equals(row.hash, hash, StringComparison.Ordinal) || !canUpgradeInPlace(row.version) || row.version > ANALYSIS_VERSION)
                        needing.Add(id);
                }

                progress?.Invoke(total, total);

                // 可选清理：删除已不存在的条目（避免无限增长）。
                // 这里用 HashSet 做 membership 判断，避免每条都查库。
                HashSet<Guid> live = beatmapList.Select(b => b.id).ToHashSet();
                var toDelete = existing.Keys.Where(id => !live.Contains(id)).ToList();

                if (toDelete.Count > 0)
                {
                    using var connection = openConnection();
                    using var transaction = connection.BeginTransaction();
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM mania_analysis WHERE beatmap_id = $id;";

                    var idParam = cmd.CreateParameter();
                    idParam.ParameterName = "$id";
                    cmd.Parameters.Add(idParam);

                    foreach (var id in toDelete)
                    {
                        idParam.Value = id.ToString();
                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }

                return needing;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetBeatmapsNeedingRecompute failed.");
                return Array.Empty<Guid>();
            }
        }

        public string CreateSongsBranchDatabasePath(string rulesetShortName)
        {
            string safeRulesetName = string.IsNullOrWhiteSpace(rulesetShortName) ? "ruleset" : rulesetShortName.Trim();
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            return storage.GetFullPath(Path.Combine(SONGS_BRANCH_DATABASE_DIRECTORY, $"songs_{safeRulesetName}_{timestamp}.sqlite"), true);
        }

        public string CreateSongsBranchDatabasePath(SongsBranchMetadata metadata)
        {
            string safeCollectionName = createSafeBranchDisplayName(string.IsNullOrWhiteSpace(metadata.SourceCollectionName) ? "collection" : metadata.SourceCollectionName);
            string safeModsDisplay = createSafeBranchModsDisplay(metadata.ModsDisplay);
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            return storage.GetFullPath(Path.Combine(SONGS_BRANCH_DATABASE_DIRECTORY, $"songs_{safeCollectionName}_{safeModsDisplay}_{timestamp}.sqlite"), true);
        }

        public void StoreSongsBranch(string databasePath, SongsBranchMetadata metadata, IEnumerable<SongsBranchRow> rows, SourceCollectionSnapshot? sourceCollection = null)
        {
            if (!Enabled)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

            if (File.Exists(databasePath))
                File.Delete(databasePath);

            using var connection = openConnection(databasePath);

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA temp_store=MEMORY;
";
                cmd.ExecuteNonQuery();
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS xxy_sr_branch (
    beatmap_id TEXT PRIMARY KEY,
    beatmap_hash TEXT NOT NULL,
    beatmap_md5 TEXT NOT NULL,
    xxy_sr REAL NOT NULL
);

CREATE TABLE IF NOT EXISTS hidden_preexisting_beatmap (
    beatmap_id TEXT PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS source_collection_beatmap (
    beatmap_md5 TEXT PRIMARY KEY
);
";
                cmd.ExecuteNonQuery();
            }

            long sourceCollectionLastModified = sourceCollection?.LastModifiedUnixMilliseconds ?? metadata.SourceCollectionLastModifiedUnixMilliseconds;
            int sourceCollectionBeatmapCount = sourceCollection?.BeatmapMd5Hashes.Count ?? metadata.SourceCollectionBeatmapCount;

            setMeta(connection, "kind", xxy_sr_branch_kind);
            setMeta(connection, "schema_version", xxy_sr_branch_schema_version.ToString(CultureInfo.InvariantCulture));
            setMeta(connection, "analysis_version", ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));
            setMeta(connection, "ruleset_online_id", metadata.RulesetOnlineId.ToString(CultureInfo.InvariantCulture));
            setMeta(connection, "ruleset_short_name", metadata.RulesetShortName);
            setMeta(connection, "mods_fingerprint", metadata.ModsFingerprint);
            setMeta(connection, "mods_display", metadata.ModsDisplay);
            setMeta(connection, "beatmap_count", metadata.BeatmapCount.ToString(CultureInfo.InvariantCulture));
            setMeta(connection, "created_at", metadata.CreatedAtUnixMilliseconds.ToString(CultureInfo.InvariantCulture));
            setMeta(connection, "display_name", metadata.DisplayName);
            setMeta(connection, "mods_json", metadata.ModsJson);
            setMeta(connection, "hidden_applied", metadata.HiddenApplied ? "1" : "0");
            setMeta(connection, "source_collection_id", metadata.SourceCollectionId == Guid.Empty ? string.Empty : metadata.SourceCollectionId.ToString());
            setMeta(connection, "source_collection_name", metadata.SourceCollectionName);
            setMeta(connection, "source_collection_last_modified", sourceCollectionLastModified.ToString(CultureInfo.InvariantCulture));
            setMeta(connection, "source_collection_beatmap_count", sourceCollectionBeatmapCount.ToString(CultureInfo.InvariantCulture));

            using var transaction = connection.BeginTransaction();

            using (var deleteSourceCollection = connection.CreateCommand())
            {
                deleteSourceCollection.Transaction = transaction;
                deleteSourceCollection.CommandText = "DELETE FROM source_collection_beatmap;";
                deleteSourceCollection.ExecuteNonQuery();
            }

            if (sourceCollection is SourceCollectionSnapshot sourceCollectionSnapshot)
            {
                using var insertSourceCollection = connection.CreateCommand();
                insertSourceCollection.Transaction = transaction;
                insertSourceCollection.CommandText = @"
INSERT INTO source_collection_beatmap(beatmap_md5)
VALUES($md5)
ON CONFLICT(beatmap_md5) DO NOTHING;
";

                var sourceCollectionMd5Param = insertSourceCollection.CreateParameter();
                sourceCollectionMd5Param.ParameterName = "$md5";
                insertSourceCollection.Parameters.Add(sourceCollectionMd5Param);

                foreach (string beatmapMd5 in sourceCollectionSnapshot.BeatmapMd5Hashes.Where(hash => !string.IsNullOrWhiteSpace(hash)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    sourceCollectionMd5Param.Value = beatmapMd5;
                    insertSourceCollection.ExecuteNonQuery();
                }
            }

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
INSERT INTO xxy_sr_branch(
    beatmap_id,
    beatmap_hash,
    beatmap_md5,
    xxy_sr
)
VALUES(
    $id,
    $hash,
    $md5,
    $xxy_sr
)
ON CONFLICT(beatmap_id) DO UPDATE SET
    beatmap_hash = excluded.beatmap_hash,
    beatmap_md5 = excluded.beatmap_md5,
    xxy_sr = excluded.xxy_sr;
";

            var idParam = insert.CreateParameter();
            idParam.ParameterName = "$id";
            insert.Parameters.Add(idParam);

            var hashParam = insert.CreateParameter();
            hashParam.ParameterName = "$hash";
            insert.Parameters.Add(hashParam);

            var md5Param = insert.CreateParameter();
            md5Param.ParameterName = "$md5";
            insert.Parameters.Add(md5Param);

            var xxySrParam = insert.CreateParameter();
            xxySrParam.ParameterName = "$xxy_sr";
            insert.Parameters.Add(xxySrParam);

            foreach (var row in rows)
            {
                idParam.Value = row.BeatmapId.ToString();
                hashParam.Value = row.BeatmapHash;
                md5Param.Value = row.BeatmapMd5;
                xxySrParam.Value = row.XxySr;
                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        public IReadOnlyDictionary<Guid, double> GetSongsBranchValues(string databasePath, IEnumerable<BeatmapInfo> beatmaps)
        {
            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return empty_xxy_sr_values;

            try
            {
                var beatmapList = beatmaps.Distinct().ToList();

                if (beatmapList.Count == 0)
                    return empty_xxy_sr_values;

                using var connection = openConnection(databasePath);

                if (!isValidSongsBranchConnection(connection))
                    return empty_xxy_sr_values;

                var beatmapsById = beatmapList.ToDictionary(b => b.ID);
                var resolvedValues = new Dictionary<Guid, double>(beatmapList.Count);
                var idsToQuery = beatmapList.Select(b => b.ID).ToList();

                for (int offset = 0; offset < idsToQuery.Count; offset += 800)
                {
                    using var cmd = connection.CreateCommand();

                    int batchCount = Math.Min(800, idsToQuery.Count - offset);
                    var parameterNames = new List<string>(batchCount);

                    for (int i = 0; i < batchCount; i++)
                    {
                        string parameterName = $"$id{i}";
                        parameterNames.Add(parameterName);
                        cmd.Parameters.AddWithValue(parameterName, idsToQuery[offset + i].ToString());
                    }

                    cmd.CommandText = $@"
SELECT beatmap_id, beatmap_hash, beatmap_md5, xxy_sr
FROM xxy_sr_branch
WHERE beatmap_id IN ({string.Join(", ", parameterNames)});
";

                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        if (!Guid.TryParse(reader.GetString(0), out var beatmapId))
                            continue;

                        if (!beatmapsById.TryGetValue(beatmapId, out var beatmap))
                            continue;

                        string storedHash = reader.GetString(1);
                        string storedMd5 = reader.GetString(2);

                        if (!string.Equals(storedHash, beatmap.Hash, StringComparison.Ordinal))
                            continue;

                        if (!string.IsNullOrEmpty(storedMd5) && !string.Equals(storedMd5, beatmap.MD5Hash, StringComparison.Ordinal))
                            continue;

                        resolvedValues[beatmapId] = reader.IsDBNull(3) ? 0 : reader.GetDouble(3);
                    }
                }

                return resolvedValues;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetSongsBranchValues failed.", Ez2ConfigManager.LOGGER_NAME);
                return empty_xxy_sr_values;
            }
        }

        public IReadOnlySet<string> GetSongsBranchCollectionBeatmapMd5Hashes(string databasePath)
        {
            var beatmapMd5Hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return beatmapMd5Hashes;

            try
            {
                using var connection = openConnection(databasePath);

                if (!isValidSongsBranchConnection(connection))
                    return beatmapMd5Hashes;

                try
                {
                    using var collectionCommand = connection.CreateCommand();
                    collectionCommand.CommandText = @"
SELECT beatmap_md5
FROM source_collection_beatmap;
";

                    using var collectionReader = collectionCommand.ExecuteReader();

                    while (collectionReader.Read())
                    {
                        string beatmapMd5 = collectionReader.GetString(0);

                        if (!string.IsNullOrWhiteSpace(beatmapMd5))
                            beatmapMd5Hashes.Add(beatmapMd5);
                    }
                }
                catch (SqliteException)
                {
                }

                if (beatmapMd5Hashes.Count > 0)
                    return beatmapMd5Hashes;

                using var branchCommand = connection.CreateCommand();
                branchCommand.CommandText = @"
SELECT beatmap_md5
FROM xxy_sr_branch;
";

                using var branchReader = branchCommand.ExecuteReader();

                while (branchReader.Read())
                {
                    string beatmapMd5 = branchReader.GetString(0);

                    if (!string.IsNullOrWhiteSpace(beatmapMd5))
                        beatmapMd5Hashes.Add(beatmapMd5);
                }

                return beatmapMd5Hashes;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetSongsBranchCollectionBeatmapMd5Hashes failed.", Ez2ConfigManager.LOGGER_NAME);
                return beatmapMd5Hashes;
            }
        }

        public IReadOnlyList<SongsBranchDescriptor> GetAvailableSongsBranches()
        {
            if (!Enabled)
                return Array.Empty<SongsBranchDescriptor>();

            var descriptors = new List<SongsBranchDescriptor>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (storage.ExistsDirectory(SONGS_BRANCH_DATABASE_DIRECTORY))
            {
                foreach (string relativePath in storage.GetFiles(SONGS_BRANCH_DATABASE_DIRECTORY, "*.sqlite"))
                {
                    string absolutePath = storage.GetFullPath(relativePath);

                    if (!seenPaths.Add(absolutePath))
                        continue;

                    if (TryGetSongsBranchDescriptor(absolutePath, out var descriptor))
                        descriptors.Add(descriptor);
                }
            }

            return descriptors
                   .OrderByDescending(d => d.Metadata.CreatedAtUnixMilliseconds)
                   .ThenByDescending(d => d.RelativePath, StringComparer.Ordinal)
                   .ToList();
        }

        public bool TrySetSongsBranchHideState(string databasePath, bool hiddenApplied, IEnumerable<Guid> preexistingHiddenBeatmapIds)
        {
            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return false;

            try
            {
                using var connection = openConnection(databasePath);

                if (!isValidSongsBranchConnection(connection))
                    return false;

                ensureSongsBranchStateTables(connection);

                using var transaction = connection.BeginTransaction();

                setMeta(connection, "hidden_applied", hiddenApplied ? "1" : "0");

                using (var delete = connection.CreateCommand())
                {
                    delete.Transaction = transaction;
                    delete.CommandText = "DELETE FROM hidden_preexisting_beatmap;";
                    delete.ExecuteNonQuery();
                }

                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = @"
INSERT INTO hidden_preexisting_beatmap(beatmap_id)
VALUES($id)
ON CONFLICT(beatmap_id) DO NOTHING;
";

                var idParam = insert.CreateParameter();
                idParam.ParameterName = "$id";
                insert.Parameters.Add(idParam);

                foreach (Guid beatmapId in preexistingHiddenBeatmapIds.Distinct())
                {
                    idParam.Value = beatmapId.ToString();
                    insert.ExecuteNonQuery();
                }

                transaction.Commit();
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TrySetSongsBranchHideState failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        public bool TrySetCollectionHideState(Guid collectionId, bool hiddenApplied, IEnumerable<Guid> preexistingHiddenBeatmapIds, IEnumerable<string> beatmapMd5Hashes)
        {
            if (!Enabled || collectionId == Guid.Empty)
                return false;

            try
            {
                Initialise();

                using var connection = openConnection();
                ensureCollectionHideTables(connection);

                using var transaction = connection.BeginTransaction();

                using (var upsertState = connection.CreateCommand())
                {
                    upsertState.Transaction = transaction;
                    upsertState.CommandText = @"
INSERT INTO collection_hidden_state(collection_id, hidden_applied)
VALUES($collection_id, $hidden_applied)
ON CONFLICT(collection_id) DO UPDATE SET
    hidden_applied = excluded.hidden_applied;
";
                    upsertState.Parameters.AddWithValue("$collection_id", collectionId.ToString());
                    upsertState.Parameters.AddWithValue("$hidden_applied", hiddenApplied ? 1 : 0);
                    upsertState.ExecuteNonQuery();
                }

                using (var deletePreexisting = connection.CreateCommand())
                {
                    deletePreexisting.Transaction = transaction;
                    deletePreexisting.CommandText = "DELETE FROM collection_hidden_preexisting_beatmap WHERE collection_id = $collection_id;";
                    deletePreexisting.Parameters.AddWithValue("$collection_id", collectionId.ToString());
                    deletePreexisting.ExecuteNonQuery();
                }

                using (var deleteMd5 = connection.CreateCommand())
                {
                    deleteMd5.Transaction = transaction;
                    deleteMd5.CommandText = "DELETE FROM collection_hidden_beatmap_md5 WHERE collection_id = $collection_id;";
                    deleteMd5.Parameters.AddWithValue("$collection_id", collectionId.ToString());
                    deleteMd5.ExecuteNonQuery();
                }

                using (var insertPreexisting = connection.CreateCommand())
                {
                    insertPreexisting.Transaction = transaction;
                    insertPreexisting.CommandText = @"
INSERT INTO collection_hidden_preexisting_beatmap(collection_id, beatmap_id)
VALUES($collection_id, $beatmap_id)
ON CONFLICT(collection_id, beatmap_id) DO NOTHING;
";

                    insertPreexisting.Parameters.AddWithValue("$collection_id", collectionId.ToString());
                    var beatmapIdParam = insertPreexisting.CreateParameter();
                    beatmapIdParam.ParameterName = "$beatmap_id";
                    insertPreexisting.Parameters.Add(beatmapIdParam);

                    foreach (Guid beatmapId in preexistingHiddenBeatmapIds.Distinct())
                    {
                        beatmapIdParam.Value = beatmapId.ToString();
                        insertPreexisting.ExecuteNonQuery();
                    }
                }

                using (var insertMd5 = connection.CreateCommand())
                {
                    insertMd5.Transaction = transaction;
                    insertMd5.CommandText = @"
INSERT INTO collection_hidden_beatmap_md5(collection_id, beatmap_md5)
VALUES($collection_id, $beatmap_md5)
ON CONFLICT(collection_id, beatmap_md5) DO NOTHING;
";

                    insertMd5.Parameters.AddWithValue("$collection_id", collectionId.ToString());
                    var beatmapMd5Param = insertMd5.CreateParameter();
                    beatmapMd5Param.ParameterName = "$beatmap_md5";
                    insertMd5.Parameters.Add(beatmapMd5Param);

                    foreach (string beatmapMd5 in beatmapMd5Hashes.Where(hash => !string.IsNullOrWhiteSpace(hash)).Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        beatmapMd5Param.Value = beatmapMd5;
                        insertMd5.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TrySetCollectionHideState failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        public IReadOnlySet<Guid> GetHiddenCollectionIds()
        {
            var collectionIds = new HashSet<Guid>();

            if (!Enabled)
                return collectionIds;

            try
            {
                Initialise();

                using var connection = openConnection();
                ensureCollectionHideTables(connection);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
SELECT collection_id
FROM collection_hidden_state
WHERE hidden_applied = 1;
";

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (Guid.TryParse(reader.GetString(0), out Guid collectionId))
                        collectionIds.Add(collectionId);
                }

                return collectionIds;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetHiddenCollectionIds failed.", Ez2ConfigManager.LOGGER_NAME);
                return collectionIds;
            }
        }

        public IReadOnlySet<Guid> GetCollectionPreexistingHiddenBeatmapIds(Guid collectionId)
        {
            var beatmapIds = new HashSet<Guid>();

            if (!Enabled || collectionId == Guid.Empty)
                return beatmapIds;

            try
            {
                Initialise();

                using var connection = openConnection();
                ensureCollectionHideTables(connection);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
SELECT beatmap_id
FROM collection_hidden_preexisting_beatmap
WHERE collection_id = $collection_id;
";
                cmd.Parameters.AddWithValue("$collection_id", collectionId.ToString());

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (Guid.TryParse(reader.GetString(0), out Guid beatmapId))
                        beatmapIds.Add(beatmapId);
                }

                return beatmapIds;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetCollectionPreexistingHiddenBeatmapIds failed.", Ez2ConfigManager.LOGGER_NAME);
                return beatmapIds;
            }
        }

        public IReadOnlySet<string> GetHiddenCollectionBeatmapMd5Hashes(Guid collectionId)
        {
            var beatmapMd5Hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!Enabled || collectionId == Guid.Empty)
                return beatmapMd5Hashes;

            try
            {
                Initialise();

                using var connection = openConnection();
                ensureCollectionHideTables(connection);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
SELECT beatmap_md5
FROM collection_hidden_beatmap_md5
WHERE collection_id = $collection_id;
";
                cmd.Parameters.AddWithValue("$collection_id", collectionId.ToString());

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string beatmapMd5 = reader.GetString(0);

                    if (!string.IsNullOrWhiteSpace(beatmapMd5))
                        beatmapMd5Hashes.Add(beatmapMd5);
                }

                return beatmapMd5Hashes;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetHiddenCollectionBeatmapMd5Hashes failed.", Ez2ConfigManager.LOGGER_NAME);
                return beatmapMd5Hashes;
            }
        }

        public IReadOnlySet<Guid> GetSongsBranchPreexistingHiddenBeatmapIds(string databasePath)
        {
            var beatmapIds = new HashSet<Guid>();

            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return beatmapIds;

            try
            {
                using var connection = openConnection(databasePath);

                if (!isValidSongsBranchConnection(connection))
                    return beatmapIds;

                ensureSongsBranchStateTables(connection);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
SELECT beatmap_id
FROM hidden_preexisting_beatmap;
";

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (Guid.TryParse(reader.GetString(0), out Guid beatmapId))
                        beatmapIds.Add(beatmapId);
                }

                return beatmapIds;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetSongsBranchPreexistingHiddenBeatmapIds failed.", Ez2ConfigManager.LOGGER_NAME);
                return beatmapIds;
            }
        }

        public bool DeleteSongsBranch(string databasePath)
        {
            if (!Enabled || string.IsNullOrEmpty(databasePath))
                return false;

            try
            {
                string fullPath = Path.GetFullPath(databasePath);

                if (!File.Exists(fullPath))
                    return false;

                SqliteConnection.ClearAllPools();
                File.Delete(fullPath);
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore DeleteSongsBranch failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        public bool TryGetSongsBranchDescriptor(string databasePath, out SongsBranchDescriptor descriptor)
        {
            descriptor = default;

            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return false;

            if (!isCurrentSongsBranchPath(databasePath))
                return false;

            try
            {
                using var connection = openConnection(databasePath);

                if (!isValidSongsBranchConnection(connection))
                    return false;

                if (!tryReadSongsBranchMetadata(connection, out var metadata))
                    return false;

                descriptor = createSongsBranchDescriptor(databasePath, metadata);
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TryGetSongsBranchDescriptor failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        private SqliteConnection openConnection()
            => openConnection(dbPath);

        private static SqliteConnection openConnection(string databasePath)
        {
            // 这里每次操作打开一个连接，避免跨线程复用连接导致的问题。
            var connection = new SqliteConnection($"Data Source={databasePath};Cache=Shared;Mode=ReadWriteCreate");
            connection.Open();
            return connection;
        }

        private static void setMeta(SqliteConnection connection, string key, string value)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO meta(key, value)
VALUES($k, $v)
ON CONFLICT(key) DO UPDATE SET value = excluded.value;
";
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$v", value);
            cmd.ExecuteNonQuery();
        }

        private static string? tryGetMeta(SqliteConnection connection, string key)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT value
FROM meta
WHERE key = $key
LIMIT 1;
";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar() as string;
        }

        private static bool isValidSongsBranchConnection(SqliteConnection connection)
        {
            string? kind = tryGetMeta(connection, "kind");
            string? schemaVersionText = tryGetMeta(connection, "schema_version");
            string? analysisVersionText = tryGetMeta(connection, "analysis_version");

            if (!string.Equals(kind, xxy_sr_branch_kind, StringComparison.Ordinal))
                return false;

            if (!int.TryParse(schemaVersionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int schemaVersion) || schemaVersion != xxy_sr_branch_schema_version)
                return false;

            if (!int.TryParse(analysisVersionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int analysisVersion) || analysisVersion != ANALYSIS_VERSION)
                return false;

            return true;
        }

        private static bool tryReadSongsBranchMetadata(SqliteConnection connection, out SongsBranchMetadata metadata)
        {
            metadata = default;

            string? rulesetOnlineIdText = tryGetMeta(connection, "ruleset_online_id");
            string? rulesetShortName = tryGetMeta(connection, "ruleset_short_name");
            string? modsFingerprint = tryGetMeta(connection, "mods_fingerprint");
            string? modsDisplay = tryGetMeta(connection, "mods_display");
            string? beatmapCountText = tryGetMeta(connection, "beatmap_count");
            string? createdAtText = tryGetMeta(connection, "created_at");
            string? displayName = tryGetMeta(connection, "display_name");
            string? modsJson = tryGetMeta(connection, "mods_json");
            string? hiddenAppliedText = tryGetMeta(connection, "hidden_applied");
            string? sourceCollectionIdText = tryGetMeta(connection, "source_collection_id");
            string? sourceCollectionName = tryGetMeta(connection, "source_collection_name");
            string? sourceCollectionLastModifiedText = tryGetMeta(connection, "source_collection_last_modified");
            string? sourceCollectionBeatmapCountText = tryGetMeta(connection, "source_collection_beatmap_count");

            if (!int.TryParse(rulesetOnlineIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rulesetOnlineId))
                return false;

            if (!int.TryParse(beatmapCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int beatmapCount))
                beatmapCount = 0;

            if (!long.TryParse(createdAtText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long createdAtUnixMilliseconds))
                createdAtUnixMilliseconds = 0;

            rulesetShortName ??= "ruleset";
            modsFingerprint ??= string.Empty;
            modsDisplay ??= "NoMod";
            sourceCollectionName ??= string.Empty;
            displayName ??= string.IsNullOrWhiteSpace(sourceCollectionName)
                ? $"songs | {modsDisplay}"
                : $"{sourceCollectionName} | {modsDisplay}";
            modsJson ??= string.Empty;
            bool hiddenApplied = string.Equals(hiddenAppliedText, "1", StringComparison.Ordinal);
            Guid sourceCollectionId = Guid.TryParse(sourceCollectionIdText, out Guid parsedSourceCollectionId) ? parsedSourceCollectionId : Guid.Empty;
            long sourceCollectionLastModifiedUnixMilliseconds = long.TryParse(sourceCollectionLastModifiedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedSourceCollectionLastModified)
                ? parsedSourceCollectionLastModified
                : 0;
            int sourceCollectionBeatmapCount = int.TryParse(sourceCollectionBeatmapCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSourceCollectionBeatmapCount)
                ? parsedSourceCollectionBeatmapCount
                : 0;

            metadata = new SongsBranchMetadata(
                rulesetOnlineId,
                rulesetShortName,
                modsFingerprint,
                modsDisplay,
                beatmapCount,
                createdAtUnixMilliseconds,
                displayName,
                modsJson,
                hiddenApplied,
                sourceCollectionId,
                sourceCollectionName,
                sourceCollectionLastModifiedUnixMilliseconds,
                sourceCollectionBeatmapCount);
            return true;
        }

        private static void ensureSongsBranchStateTables(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS hidden_preexisting_beatmap (
    beatmap_id TEXT PRIMARY KEY
);
";
            cmd.ExecuteNonQuery();
        }

        private static void ensureCollectionHideTables(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS collection_hidden_state (
    collection_id TEXT PRIMARY KEY,
    hidden_applied INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS collection_hidden_preexisting_beatmap (
    collection_id TEXT NOT NULL,
    beatmap_id TEXT NOT NULL,
    PRIMARY KEY(collection_id, beatmap_id)
);

CREATE TABLE IF NOT EXISTS collection_hidden_beatmap_md5 (
    collection_id TEXT NOT NULL,
    beatmap_md5 TEXT NOT NULL,
    PRIMARY KEY(collection_id, beatmap_md5)
);
";
            cmd.ExecuteNonQuery();
        }

        private bool isCurrentSongsBranchPath(string databasePath)
        {
            string fullPath = Path.GetFullPath(databasePath);
            string branchRoot = storage.GetFullPath(SONGS_BRANCH_DATABASE_DIRECTORY, true);
            string relativePath = Path.GetRelativePath(branchRoot, fullPath);

            if (string.IsNullOrEmpty(relativePath))
                return false;

            return !relativePath.StartsWith("..", StringComparison.Ordinal)
                   && !Path.IsPathRooted(relativePath);
        }

        private SongsBranchDescriptor createSongsBranchDescriptor(string databasePath, SongsBranchMetadata metadata)
        {
            string fullPath = Path.GetFullPath(databasePath);
            string rootPath = storage.GetFullPath(string.Empty, true);
            string relativePath = Path.GetRelativePath(rootPath, fullPath).Replace('\\', '/');

            return new SongsBranchDescriptor(fullPath, relativePath, metadata);
        }

        private static string createSafeBranchModsDisplay(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NoMod";

            var builder = new StringBuilder(value.Length);

            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                }
                else if (c == '+')
                {
                    builder.Append('+');
                }
            }

            string result = builder.ToString().Trim('+');

            while (result.Contains("++", StringComparison.Ordinal))
                result = result.Replace("++", "+", StringComparison.Ordinal);

            return string.IsNullOrEmpty(result) ? "NoMod" : result;
        }

        private static string createSafeBranchDisplayName(string value)
        {
            string trimmed = value.Trim();

            if (string.IsNullOrEmpty(trimmed))
                return "songs_branch";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(trimmed.Length);

            foreach (char c in trimmed)
            {
                if (Array.IndexOf(invalidChars, c) >= 0)
                    continue;

                builder.Append(c);
            }

            string result = builder.ToString().Trim().TrimEnd('.');
            return string.IsNullOrEmpty(result) ? "songs_branch" : result;
        }

        private static bool canUpgradeInPlace(int storedVersion) => storedVersion >= min_inplace_upgrade_version && storedVersion <= ANALYSIS_VERSION;

        private void tryClonePreviousDatabaseIfMissing()
        {
            if (string.IsNullOrEmpty(dbPath))
                return;

            if (File.Exists(dbPath))
                return;

            string? dir = Path.GetDirectoryName(dbPath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return;

            // Find the latest previous DB file (by version suffix) which we can potentially upgrade from.
            // Even if it contains older rows, we will still validate per-row and decide upgrade vs recompute.
            string? bestCandidate = null;
            int bestVersion = -1;

            foreach (string file in Directory.EnumerateFiles(dir, "mania-analysis_v*.sqlite", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(file, dbPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!tryParseDatabaseVersion(file, out int version))
                    continue;

                if (version >= ANALYSIS_VERSION)
                    continue;

                if (version > bestVersion)
                {
                    bestVersion = version;
                    bestCandidate = file;
                }
            }

            if (bestCandidate == null)
                return;

            try
            {
                File.Copy(bestCandidate, dbPath);
                Logger.Log($"[EzManiaAnalysisPersistentStore] Cloned DB from v{bestVersion} to v{ANALYSIS_VERSION}: {Path.GetFileName(bestCandidate)} -> {Path.GetFileName(dbPath)}",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
            }
            catch (Exception e)
            {
                // If cloning fails, we simply fall back to creating a fresh DB and recomputing as needed.
                Logger.Error(e, "[EzManiaAnalysisPersistentStore] Failed to clone previous DB; falling back to fresh database.", Ez2ConfigManager.LOGGER_NAME);
            }
        }

        private static bool tryParseDatabaseVersion(string filePath, out int version)
        {
            version = 0;

            string name = Path.GetFileName(filePath);
            const string prefix = "mania-analysis_v";
            const string suffix = ".sqlite";

            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return false;

            string number = name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length);
            return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out version);
        }

        private static void writeUpgradedRow(SqliteConnection connection,
                                             BeatmapInfo beatmap,
                                             double averageKps,
                                             double maxKps,
                                             IReadOnlyList<double> kpsList,
                                             double? xxySr,
                                             IReadOnlyDictionary<int, int> columnCounts,
                                             IReadOnlyDictionary<int, int> holdNoteCounts)
        {
            string kpsListJson = JsonSerializer.Serialize(kpsList);
            string columnCountsJson = JsonSerializer.Serialize(columnCounts);
            string holdNoteCountsJson = JsonSerializer.Serialize(holdNoteCounts);

            using var update = connection.CreateCommand();
            update.CommandText = @"
UPDATE mania_analysis
SET beatmap_md5 = $md5,
    analysis_version = $version,
    kps_list_json = $kps_list_json,
    xxy_sr = $xxy_sr,
    column_counts_json = $column_counts_json,
    hold_note_counts_json = $hold_note_counts_json
WHERE beatmap_id = $id;
";
            update.Parameters.AddWithValue("$id", beatmap.ID.ToString());
            update.Parameters.AddWithValue("$md5", beatmap.MD5Hash);
            update.Parameters.AddWithValue("$version", ANALYSIS_VERSION);
            update.Parameters.AddWithValue("$kps_list_json", kpsListJson);
            update.Parameters.AddWithValue("$xxy_sr", xxySr is null ? DBNull.Value : xxySr.Value);
            update.Parameters.AddWithValue("$column_counts_json", columnCountsJson);
            update.Parameters.AddWithValue("$hold_note_counts_json", holdNoteCountsJson);

            update.ExecuteNonQuery();
        }

        private void startBackgroundWriter()
        {
            if (backgroundWriterTask != null)
                return;

            writeCts = new CancellationTokenSource();
            backgroundWriterTask = Task.Run(() => backgroundWriterLoop(writeCts.Token));
        }

        private async Task backgroundWriterLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(500, token).ConfigureAwait(false);

                    if (pendingWrites.IsEmpty)
                        continue;

                    var batch = pendingWrites.ToArray();

                    try
                    {
                        using var connection = openConnection();
                        using var transaction = connection.BeginTransaction();

                        foreach (var kv in batch)
                        {
                            var id = kv.Key;
                            var pw = kv.Value;

                            try
                            {
                                writePendingEntryToConnection(connection, pw.Beatmap, pw.Analysis, transaction);

                                // Only remove if the pending entry we wrote is still the latest.
                                if (pendingWrites.TryGetValue(id, out var latest) && latest.Timestamp == pw.Timestamp)
                                    pendingWrites.TryRemove(id, out _);
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e, "EzManiaAnalysisPersistentStore background write failed for entry.");
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception e)
                    {
                        // Log and continue; writer will retry on next loop.
                        Logger.Error(e, "EzManiaAnalysisPersistentStore background batch write failed.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected on cancellation
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore background writer crashed.");
            }
        }

        private void writePendingEntryToConnection(SqliteConnection connection, BeatmapInfo beatmap, EzAnalysisResult analysis, SqliteTransaction? transaction = null)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;

            var commonAttributes = analysis.CommonAttributes;
            var maniaAttributes = analysis.ManiaAttributes;

            string kpsListJson = JsonSerializer.Serialize(commonAttributes?.KpsList ?? Array.Empty<double>());
            string columnCountsJson = JsonSerializer.Serialize(maniaAttributes?.ColumnCounts ?? new Dictionary<int, int>());
            string holdNoteCountsJson = JsonSerializer.Serialize(maniaAttributes?.HoldNoteCounts ?? new Dictionary<int, int>());

            cmd.CommandText = @"
INSERT INTO mania_analysis(
    beatmap_id,
    beatmap_hash,
    beatmap_md5,
    analysis_version,
    average_kps,
    max_kps,
    kps_list_json,
    xxy_sr,
    column_counts_json,
    hold_note_counts_json
)
VALUES(
    $id,
    $hash,
    $md5,
    $version,
    $avg,
    $max,
    $kps,
    $xxy,
    $cols,
    $holds
)
ON CONFLICT(beatmap_id) DO UPDATE SET
    beatmap_hash = excluded.beatmap_hash,
    beatmap_md5 = excluded.beatmap_md5,
    analysis_version = excluded.analysis_version,
    average_kps = excluded.average_kps,
    max_kps = excluded.max_kps,
    kps_list_json = excluded.kps_list_json,
    xxy_sr = excluded.xxy_sr,
    column_counts_json = excluded.column_counts_json,
    hold_note_counts_json = excluded.hold_note_counts_json;
";

            cmd.Parameters.AddWithValue("$id", beatmap.ID.ToString());
            cmd.Parameters.AddWithValue("$hash", beatmap.Hash);
            cmd.Parameters.AddWithValue("$md5", beatmap.MD5Hash);
            cmd.Parameters.AddWithValue("$version", ANALYSIS_VERSION);
            cmd.Parameters.AddWithValue("$avg", analysis.AverageKps);
            cmd.Parameters.AddWithValue("$max", analysis.MaxKps);
            cmd.Parameters.AddWithValue("$kps", kpsListJson);

            if (maniaAttributes?.XxySr is double xxySr)
                cmd.Parameters.AddWithValue("$xxy", xxySr);
            else
                cmd.Parameters.AddWithValue("$xxy", DBNull.Value);

            cmd.Parameters.AddWithValue("$cols", columnCountsJson);
            cmd.Parameters.AddWithValue("$holds", holdNoteCountsJson);

            cmd.ExecuteNonQuery();

            // Update last_updated if column exists
            try
            {
                if (hasColumn(connection, "mania_analysis", "last_updated"))
                {
                    using var upd = connection.CreateCommand();
                    upd.Transaction = transaction;
                    upd.CommandText = "UPDATE mania_analysis SET last_updated = $ts WHERE beatmap_id = $id";
                    upd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    upd.Parameters.AddWithValue("$id", beatmap.ID.ToString());
                    upd.ExecuteNonQuery();
                }
            }
            catch
            {
                // ignore last_updated failures
            }
        }

        private void cleanupUnrecognizedColumns(SqliteConnection connection)
        {
            var existingColumns = getTableColumns(connection, "mania_analysis");
            var unrecognizedColumns = existingColumns.Where(c => !allowed_columns.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();

            if (unrecognizedColumns.Count == 0)
                return;

            // 重建表，删除不识别的列
            Logger.Log($"[EzManiaAnalysisPersistentStore] Found unrecognized columns: {string.Join(", ", unrecognizedColumns)}; rebuilding table.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Verbose);

            rebuildTableWithoutUnrecognizedColumns(connection, unrecognizedColumns);
        }

        private List<string> getTableColumns(SqliteConnection connection, string tableName)
        {
            var columns = new List<string>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({tableName});";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                columns.Add(reader.GetString(1)); // name
            }

            return columns;
        }

        private void rebuildTableWithoutUnrecognizedColumns(SqliteConnection connection, List<string> unrecognizedColumns)
        {
            // 创建临时表，只包含允许的列
            using var createTempCmd = connection.CreateCommand();
            createTempCmd.CommandText = @"
CREATE TABLE mania_analysis_temp (
    beatmap_id TEXT PRIMARY KEY,
    beatmap_hash TEXT NOT NULL,
    beatmap_md5 TEXT NOT NULL,
    analysis_version INTEGER NOT NULL,
    average_kps REAL NOT NULL,
    max_kps REAL NOT NULL,
    kps_list_json TEXT NOT NULL,
    xxy_sr REAL NULL,
    column_counts_json TEXT NOT NULL,
    hold_note_counts_json TEXT NOT NULL
);
";
            createTempCmd.ExecuteNonQuery();

            // 复制数据，只复制允许的列
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
INSERT INTO mania_analysis_temp (beatmap_id, beatmap_hash, beatmap_md5, analysis_version, average_kps, max_kps, kps_list_json, xxy_sr, column_counts_json, hold_note_counts_json)
SELECT beatmap_id, beatmap_hash, beatmap_md5, analysis_version, average_kps, max_kps, kps_list_json, xxy_sr, column_counts_json, hold_note_counts_json
FROM mania_analysis;
";
            insertCmd.ExecuteNonQuery();

            // 删除旧表
            using var dropCmd = connection.CreateCommand();
            dropCmd.CommandText = "DROP TABLE mania_analysis;";
            dropCmd.ExecuteNonQuery();

            // 重命名临时表
            using var renameCmd = connection.CreateCommand();
            renameCmd.CommandText = "ALTER TABLE mania_analysis_temp RENAME TO mania_analysis;";
            renameCmd.ExecuteNonQuery();

            // 重新创建索引
            using var indexCmd = connection.CreateCommand();
            indexCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_mania_analysis_version ON mania_analysis(analysis_version);";
            indexCmd.ExecuteNonQuery();

            // 清理数据库文件大小
            using var vacuumCmd = connection.CreateCommand();
            vacuumCmd.CommandText = "VACUUM;";
            vacuumCmd.ExecuteNonQuery();
        }

        private bool hasColumn(SqliteConnection connection, string tableName, string columnName)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({tableName});";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string name = reader.GetString(1);
                if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Validates that the analysis result contains reasonable values.
        /// </summary>
        private static bool isValidAnalysisResult(EzAnalysisResult result)
        {
            if (result.ManiaAttributes?.XxySr is double xxySr && (double.IsNaN(xxySr) || double.IsInfinity(xxySr)))
                return false;

            return true;
        }
    }
}
