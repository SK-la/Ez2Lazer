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
    public class EzAnalysisPersistentStore : IDisposable
    {
        [Flags]
        internal enum MissingDataKind
        {
            None = 0,
            Common = 1 << 0,
            Mania = 1 << 1,
        }

        public readonly record struct SongsBranchRow(Guid BeatmapId, string BeatmapHash, string BeatmapMd5, double XxySr, double? Pp);

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

        public const string LEGACY_DATABASE_FILENAME_PREFIX = "ez-analysis_v";

        public const string SONGS_BRANCH_DATABASE_DIRECTORY = "EzData";

        private const string songs_branch_kind = "songs_branch";
        private const string legacy_xxy_sr_branch_kind = "xxy_sr_branch";
        private const int songs_branch_schema_version = 3;

        /// <summary>
        /// 主分析库文件版本。仅 kps / KPC 表结构或 kps 算法变更时递增；v7 自 v6 继承 kps 数据并移除 legacy 列。
        /// 分支库 schema v1 迁移后需全量重算 xxy/PP；v2 及以后迁移时保留既有版本 meta 并复用结果。
        /// </summary>
        public const int ANALYSIS_VERSION = 7;

        public static string DatabaseFilename => $"{LEGACY_DATABASE_FILENAME_PREFIX}{ANALYSIS_VERSION}.sqlite";

        /// <summary>
        /// 旧版稳定文件名（中间分支）；v7 启动时会从此文件迁移一次。
        /// </summary>
        public const string LEGACY_STABLE_DATABASE_FILENAME = "ez-analysis.sqlite";

        // 列定义（songs branch 快照库）
        private const string col_beatmap_id = "beatmap_id";
        private const string col_beatmap_hash = "beatmap_hash";
        private const string col_beatmap_md5 = "beatmap_md5";
        private const string col_xxy_sr = "xxy_sr";
        private const string col_pp = "pp";

        private readonly Storage storage;
        private readonly object initLock = new object();
        private static readonly IReadOnlyDictionary<Guid, double> empty_xxy_sr_values = new Dictionary<Guid, double>();
        private static readonly IReadOnlyDictionary<Guid, double> empty_pp_values = new Dictionary<Guid, double>();

        private bool initialised;
        private string dbPath = string.Empty;

        private record PendingWrite(BeatmapInfo Beatmap, EzAnalysisResult Analysis, long Timestamp);

        private readonly ConcurrentDictionary<Guid, PendingWrite> pendingWrites = new ConcurrentDictionary<Guid, PendingWrite>();
        private CancellationTokenSource? writeCts;
        private Task? backgroundWriterTask;
        private bool isDisposed;

        // 常量化的表名与 meta key，避免在代码中散落硬编码字符串。
        private const string meta_key_requires_post_migration_refresh = "requires_post_migration_refresh";
        private const string meta_key_xxy_sr_version = "xxy_sr_version";
        private const string meta_key_pp_version = "pp_version";
        private const string meta_key_analysis_version = "analysis_version";
        private const string meta_key_kind = "kind";
        private const string meta_key_schema_version = "schema_version";
        private const string meta_key_ruleset_online_id = "ruleset_online_id";
        private const string meta_key_ruleset_short_name = "ruleset_short_name";
        private const string meta_key_mods_fingerprint = "mods_fingerprint";
        private const string meta_key_mods_display = "mods_display";
        private const string meta_key_beatmap_count = "beatmap_count";
        private const string meta_key_created_at = "created_at";
        private const string meta_key_display_name = "display_name";
        private const string meta_key_mods_json = "mods_json";
        private const string meta_key_hidden_applied = "hidden_applied";
        private const string meta_key_source_collection_id = "source_collection_id";
        private const string meta_key_source_collection_name = "source_collection_name";
        private const string meta_key_source_collection_last_modified = "source_collection_last_modified";
        private const string meta_key_source_collection_beatmap_count = "source_collection_beatmap_count";

        // songs branch tables
        private const string table_songs_branch_entry = "songs_branch_entry";
        private const string legacy_table_xxy_sr_branch = "xxy_sr_branch";
        private const string table_songs_branch_hidden_preexisting = "hidden_preexisting_beatmap";
        private const string table_songs_branch_source_collection = "source_collection_beatmap";

        // Meta 表的创建由 ensureMetaTableExists 方法负责，避免在字符串中多处复制 SQL。

        private static bool getMetaBool(SqliteConnection connection, string key)
        {
            string? v = tryGetMeta(connection, key);
            return string.Equals(v, "1", StringComparison.Ordinal);
        }

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

                dbPath = storage.GetFullPath(DatabaseFilename, true);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

                    Logger.Log($"EzManiaAnalysisPersistentStore path: {dbPath}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

                    EzAnalysisSchemaManager.TryMigrateFromPreviousMainDatabase(dbPath);

                    using var connection = openConnection();

                    EzAnalysisSchemaManager.InitializeMainDatabase(connection);

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

                pendingWrites.TryGetValue(beatmap.ID, out var pending);

                using var connection = openConnection();

                if (!tryGetRawData(connection, beatmap, out var storedAnalysis))
                {
                    if (pending is not null && string.Equals(pending.Beatmap.Hash, beatmap.Hash, StringComparison.Ordinal))
                    {
                        result = pending.Analysis;
                        return true;
                    }

                    return false;
                }

                result = pending is not null && string.Equals(pending.Beatmap.Hash, beatmap.Hash, StringComparison.Ordinal)
                    ? mergeAnalysisResult(storedAnalysis, pending.Analysis)
                    : storedAnalysis;

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

        /// <summary>
        /// NoMod 基线 xxy：从 <see cref="BeatmapInfo.XxyStarRating"/> 聚合（非 SQLite）。
        /// </summary>
        public IReadOnlyDictionary<Guid, double> GetBaselineXxySrFromRealm(IEnumerable<BeatmapInfo> beatmaps)
        {
            var beatmapList = beatmaps.Distinct().ToList();

            if (beatmapList.Count == 0)
                return empty_xxy_sr_values;

            var resolvedValues = new Dictionary<Guid, double>(beatmapList.Count);

            foreach (var beatmap in beatmapList)
            {
                if (beatmap.XxyStarRating >= 0)
                    resolvedValues[beatmap.ID] = beatmap.XxyStarRating;
            }

            return resolvedValues;
        }

        /// <summary>
        /// 对比新计算结果和 SQLite 中的旧数据，如果有差异则更新。
        /// 主要场景：KPS / mania 列统计变化（算法修复等）。基线 xxy / PP 已迁到 Realm，不在此比较。
        /// </summary>
        public void StoreIfDifferent(BeatmapInfo beatmap, EzAnalysisResult analysis)
        {
            if (!Enabled)
                return;

            var commonSummary = analysis.CommonSummary;

            // Validate the analysis result before storing
            if (!isValidAnalysisResult(analysis))
            {
                Logger.Log($"[EzManiaAnalysisPersistentStore] Refusing to store invalid analysis result for {beatmap.ID}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                return;
            }

            // 跳过失败的分析结果
            if (commonSummary == null)
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
                MissingDataKind filledMissingData = GetMissingData(storedAnalysis, beatmap.Ruleset.OnlineID) &
                                                    ~GetMissingData(analysis, beatmap.Ruleset.OnlineID);

                if (filledMissingData != MissingDataKind.None || hasDifference(storedAnalysis, analysis))
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
                pendingWrites.TryGetValue(beatmap.ID, out var pending);

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $@"
SELECT entry.{EzAnalysisSchemaManager.COL_BEATMAP_HASH},
       entry.{EzAnalysisSchemaManager.COL_BEATMAP_MD5},
       entry.{EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID},
       entry.{EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT},
       entry.{EzAnalysisSchemaManager.COL_AVERAGE_KPS},
       entry.{EzAnalysisSchemaManager.COL_MAX_KPS},
       entry.{EzAnalysisSchemaManager.COL_KPS_LIST_JSON},
       mania.{EzAnalysisSchemaManager.COL_UPDATED_AT},
       mania.{EzAnalysisSchemaManager.COL_COLUMN_COUNTS_JSON},
       mania.{EzAnalysisSchemaManager.COL_HOLD_NOTE_COUNTS_JSON}
FROM {EzAnalysisSchemaManager.TABLE_ENTRY} entry
LEFT JOIN {EzAnalysisSchemaManager.TABLE_MANIA} mania
    ON mania.{EzAnalysisSchemaManager.COL_BEATMAP_ID} = entry.{EzAnalysisSchemaManager.COL_BEATMAP_ID}
WHERE entry.{EzAnalysisSchemaManager.COL_BEATMAP_ID} = $id
LIMIT 1;
";
                    cmd.Parameters.AddWithValue("$id", beatmap.ID.ToString());

                    using var reader = cmd.ExecuteReader();

                    if (!reader.Read())
                    {
                        if (pending is not null && string.Equals(pending.Beatmap.Hash, beatmap.Hash, StringComparison.Ordinal))
                        {
                            result = pending.Analysis;
                            return true;
                        }

                        return false;
                    }

                    string storedHash = reader.GetString(0);
                    string storedMd5 = reader.GetString(1);
                    int storedRulesetOnlineId = reader.GetInt32(2);
                    long commonUpdatedAt = reader.GetInt64(3);

                    if (!string.Equals(storedHash, beatmap.Hash, StringComparison.Ordinal))
                        return false;

                    if (!string.IsNullOrEmpty(storedMd5) && !string.Equals(storedMd5, beatmap.MD5Hash, StringComparison.Ordinal))
                        return false;

                    if (storedRulesetOnlineId != beatmap.Ruleset.OnlineID)
                        return false;

                    if (commonUpdatedAt <= 0)
                        return false;

                    double averageKps = reader.GetDouble(4);
                    double maxKps = reader.GetDouble(5);
                    string kpsListJson = reader.GetString(6);
                    // 主库仅持久化 kps/KPC；xxy/PP 基线由 Realm 字段提供，不在此 DTO 中注入。
                    long maniaUpdatedAt = reader.IsDBNull(7) ? 0 : reader.GetInt64(7);

                    string columnCountsJson = reader.IsDBNull(8) ? "{}" : reader.GetString(8);
                    string holdNoteCountsJson = reader.IsDBNull(9) ? "{}" : reader.GetString(9);

                    var columnCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(columnCountsJson) ?? new Dictionary<int, int>();
                    var holdNoteCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(holdNoteCountsJson) ?? new Dictionary<int, int>();
                    var kpsList = JsonSerializer.Deserialize<List<double>>(kpsListJson) ?? new List<double>();

                    EzManiaSummary? maniaSummary = storedRulesetOnlineId == 3 && maniaUpdatedAt > 0
                        ? new EzManiaSummary(columnCounts, holdNoteCounts, xxySr: null)
                        : null;

                    result = new EzAnalysisResult(new KpsSummary(averageKps, maxKps, kpsList), pp: null, maniaSummary);

                    if (pending is not null && string.Equals(pending.Beatmap.Hash, beatmap.Hash, StringComparison.Ordinal))
                        result = mergeAnalysisResult(result, pending.Analysis);

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 比较两个分析结果是否有差异（仅 kps / KPC；xxy / PP 来自 Realm，不在此比较）。
        /// </summary>
        private bool hasDifference(EzAnalysisResult stored, EzAnalysisResult computed)
        {
            var storedCommonSummary = stored.CommonSummary;
            var computedCommonSummary = computed.CommonSummary;
            var storedManiaSummary = stored.ManiaSummary;
            var computedManiaSummary = computed.ManiaSummary;

            // 检查 KPS 相关数据
            if (!stored.AverageKps.Equals(computed.AverageKps) || !stored.MaxKps.Equals(computed.MaxKps))
                return true;

            if (!(storedCommonSummary?.KpsList ?? Array.Empty<double>()).SequenceEqual(computedCommonSummary?.KpsList ?? Array.Empty<double>()))
                return true;

            var storedColumnCounts = storedManiaSummary?.ColumnCounts ?? new Dictionary<int, int>();
            var computedColumnCounts = computedManiaSummary?.ColumnCounts ?? new Dictionary<int, int>();

            // 检查列统计
            if (storedColumnCounts.Count != computedColumnCounts.Count)
                return true;

            foreach (var kvp in computedColumnCounts)
            {
                if (!storedColumnCounts.TryGetValue(kvp.Key, out int storedCount) || storedCount != kvp.Value)
                    return true;
            }

            var storedHoldNoteCounts = storedManiaSummary?.HoldNoteCounts ?? new Dictionary<int, int>();
            var computedHoldNoteCounts = computedManiaSummary?.HoldNoteCounts ?? new Dictionary<int, int>();

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

            var commonSummary = analysis.CommonSummary;

            // Validate the analysis result before storing
            if (!isValidAnalysisResult(analysis))
            {
                Logger.Log($"[EzManiaAnalysisPersistentStore] Refusing to store invalid analysis result for {beatmap.ID}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                return;
            }

            // 跳过无效的分析结果（commonSummary 为 null）。
            // 允许保存 0-note 谱面的分析结果（KpsList 为空），避免启动时重复预热。
            if (commonSummary == null)
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

        public bool NeedsOnDemandBackfill(BeatmapInfo beatmap)
        {
            if (!Enabled)
                return false;

            EzAnalysisResult? storedAnalysis = TryGet(beatmap, out var existingAnalysis)
                ? existingAnalysis
                : null;

            return GetMissingData(storedAnalysis, beatmap.Ruleset.OnlineID) != MissingDataKind.None;
        }

        public IReadOnlyList<Guid> GetBeatmapsNeedingRecompute(IEnumerable<(Guid id, string hash, int rulesetOnlineId)> beatmaps) => GetBeatmapsNeedingRecompute(beatmaps, progress: null);

        public IReadOnlyList<Guid> GetBeatmapsNeedingRecompute(IEnumerable<(Guid id, string hash, int rulesetOnlineId)> beatmaps, Action<int, int>? progress)
        {
            if (!Enabled)
                return Array.Empty<Guid>();

            try
            {
                Initialise();

                var beatmapList = beatmaps as IList<(Guid id, string hash, int rulesetOnlineId)> ?? beatmaps.ToList();

                var existing = new Dictionary<Guid, (string hash, int rulesetOnlineId, long commonUpdatedAt)>();
                var maniaUpdated = new HashSet<Guid>();

                using (var connection = openConnection())
                {
                    using (var entryCommand = connection.CreateCommand())
                    {
                        entryCommand.CommandText = $@"
SELECT {EzAnalysisSchemaManager.COL_BEATMAP_ID},
       {EzAnalysisSchemaManager.COL_BEATMAP_HASH},
       {EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID},
       {EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT}
FROM {EzAnalysisSchemaManager.TABLE_ENTRY};
";

                        using var reader = entryCommand.ExecuteReader();

                        while (reader.Read())
                        {
                            if (!Guid.TryParse(reader.GetString(0), out var id))
                                continue;

                            existing[id] = (reader.GetString(1), reader.GetInt32(2), reader.GetInt64(3));
                        }
                    }

                    using (var maniaCommand = connection.CreateCommand())
                    {
                        maniaCommand.CommandText = $@"
SELECT {EzAnalysisSchemaManager.COL_BEATMAP_ID}
FROM {EzAnalysisSchemaManager.TABLE_MANIA}
WHERE {EzAnalysisSchemaManager.COL_UPDATED_AT} > 0;
";

                        using var maniaReader = maniaCommand.ExecuteReader();

                        while (maniaReader.Read())
                        {
                            if (Guid.TryParse(maniaReader.GetString(0), out var id))
                                maniaUpdated.Add(id);
                        }
                    }
                }

                List<Guid> needing = new List<Guid>();

                int processed = 0;
                int total = beatmapList.Count;

                foreach (var (id, hash, rulesetOnlineId) in beatmapList)
                {
                    processed++;

                    if (processed == 1 || processed % 200 == 0)
                        progress?.Invoke(processed, total);

                    if (!existing.TryGetValue(id, out var row))
                    {
                        needing.Add(id);
                        continue;
                    }

                    MissingDataKind missingData = getMissingData(row.commonUpdatedAt, maniaUpdated.Contains(id), rulesetOnlineId);

                    if (!string.Equals(row.hash, hash, StringComparison.Ordinal)
                        || row.rulesetOnlineId != rulesetOnlineId
                        || missingData != MissingDataKind.None)
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
                    using var deleteMania = connection.CreateCommand();
                    deleteMania.Transaction = transaction;
                    deleteMania.CommandText = $"DELETE FROM {EzAnalysisSchemaManager.TABLE_MANIA} WHERE {EzAnalysisSchemaManager.COL_BEATMAP_ID} = $id;";

                    using var deleteEntry = connection.CreateCommand();
                    deleteEntry.Transaction = transaction;
                    deleteEntry.CommandText = $"DELETE FROM {EzAnalysisSchemaManager.TABLE_ENTRY} WHERE {EzAnalysisSchemaManager.COL_BEATMAP_ID} = $id;";

                    var idParam = deleteEntry.CreateParameter();
                    idParam.ParameterName = "$id";
                    deleteEntry.Parameters.Add(idParam);

                    var maniaIdParam = deleteMania.CreateParameter();
                    maniaIdParam.ParameterName = "$id";
                    deleteMania.Parameters.Add(maniaIdParam);

                    foreach (var id in toDelete)
                    {
                        idParam.Value = id.ToString();
                        maniaIdParam.Value = id.ToString();
                        deleteMania.ExecuteNonQuery();
                        deleteEntry.ExecuteNonQuery();
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
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            string uniqueSuffix = Guid.NewGuid().ToString("N")[..8];

            return storage.GetFullPath(Path.Combine(SONGS_BRANCH_DATABASE_DIRECTORY, $"songs_{safeRulesetName}_{timestamp}_{uniqueSuffix}.sqlite"), true);
        }

        public string CreateSongsBranchDatabasePath(SongsBranchMetadata metadata)
        {
            string safeCollectionName = createSafeBranchDisplayName(string.IsNullOrWhiteSpace(metadata.SourceCollectionName) ? "collection" : metadata.SourceCollectionName);
            string safeModsDisplay = createSafeBranchModsDisplay(metadata.ModsDisplay);
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            string uniqueSuffix = Guid.NewGuid().ToString("N")[..8];

            return storage.GetFullPath(Path.Combine(SONGS_BRANCH_DATABASE_DIRECTORY, $"songs_{safeCollectionName}_{safeModsDisplay}_{timestamp}_{uniqueSuffix}.sqlite"), true);
        }

        public void StoreSongsBranch(string databasePath, SongsBranchMetadata metadata, IEnumerable<SongsBranchRow> rows, SourceCollectionSnapshot? sourceCollection = null,
                                     int xxySrAlgorithmVersion = 0, int ppAlgorithmVersion = 0, CancellationToken cancellationToken = default)
        {
            if (!Enabled)
                return;

            string fullPath = Path.GetFullPath(databasePath);
            bool success = false;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                // songs branch 是独立导出的快照库，这里允许整库重建；主分析库补写仍然按 beatmap 增量 upsert。
                if (File.Exists(fullPath))
                    File.Delete(fullPath);

                using var connection = openConnection(fullPath);

                cancellationToken.ThrowIfCancellationRequested();

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
                    ensureMetaTableExists(connection);

                    cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS {table_songs_branch_entry} (
    {col_beatmap_id} TEXT PRIMARY KEY,
    {col_beatmap_hash} TEXT NOT NULL,
    {col_beatmap_md5} TEXT NOT NULL,
    {col_xxy_sr} REAL NOT NULL,
    {col_pp} REAL NULL
);

CREATE TABLE IF NOT EXISTS {table_songs_branch_hidden_preexisting} (
    {col_beatmap_id} TEXT PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS {table_songs_branch_source_collection} (
    {col_beatmap_md5} TEXT PRIMARY KEY
);
";
                    cmd.ExecuteNonQuery();
                }

                long sourceCollectionLastModified = sourceCollection?.LastModifiedUnixMilliseconds ?? metadata.SourceCollectionLastModifiedUnixMilliseconds;
                int sourceCollectionBeatmapCount = sourceCollection?.BeatmapMd5Hashes.Count ?? metadata.SourceCollectionBeatmapCount;

                setMeta(connection, meta_key_kind, songs_branch_kind);
                setMeta(connection, meta_key_schema_version, songs_branch_schema_version.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_key_analysis_version, ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));

                if (xxySrAlgorithmVersion > 0)
                    setMeta(connection, meta_key_xxy_sr_version, xxySrAlgorithmVersion.ToString(CultureInfo.InvariantCulture));

                if (ppAlgorithmVersion > 0)
                    setMeta(connection, meta_key_pp_version, ppAlgorithmVersion.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_key_ruleset_online_id, metadata.RulesetOnlineId.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_key_ruleset_short_name, metadata.RulesetShortName);
                setMeta(connection, meta_key_mods_fingerprint, metadata.ModsFingerprint);
                setMeta(connection, meta_key_mods_display, metadata.ModsDisplay);
                setMeta(connection, meta_key_beatmap_count, metadata.BeatmapCount.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_key_created_at, metadata.CreatedAtUnixMilliseconds.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_key_display_name, metadata.DisplayName);
                setMeta(connection, meta_key_mods_json, metadata.ModsJson);
                setMeta(connection, meta_key_hidden_applied, metadata.HiddenApplied ? "1" : "0");
                setMeta(connection, meta_key_source_collection_id, metadata.SourceCollectionId == Guid.Empty ? string.Empty : metadata.SourceCollectionId.ToString());
                setMeta(connection, meta_key_source_collection_name, metadata.SourceCollectionName);
                setMeta(connection, meta_key_source_collection_last_modified, sourceCollectionLastModified.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_key_source_collection_beatmap_count, sourceCollectionBeatmapCount.ToString(CultureInfo.InvariantCulture));

                using var transaction = connection.BeginTransaction();

                if (sourceCollection is SourceCollectionSnapshot sourceCollectionSnapshot)
                {
                    using var insertSourceCollection = connection.CreateCommand();
                    insertSourceCollection.Transaction = transaction;
                    insertSourceCollection.CommandText = $@"
INSERT INTO {table_songs_branch_source_collection}({col_beatmap_md5})
VALUES($md5)
ON CONFLICT({col_beatmap_md5}) DO NOTHING;
";

                    var sourceCollectionMd5Param = insertSourceCollection.CreateParameter();
                    sourceCollectionMd5Param.ParameterName = "$md5";
                    insertSourceCollection.Parameters.Add(sourceCollectionMd5Param);

                    foreach (string beatmapMd5 in sourceCollectionSnapshot.BeatmapMd5Hashes.Where(hash => !string.IsNullOrWhiteSpace(hash)).Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        sourceCollectionMd5Param.Value = beatmapMd5;
                        insertSourceCollection.ExecuteNonQuery();
                    }
                }

                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = $@"
INSERT INTO {table_songs_branch_entry}(
    {col_beatmap_id},
    {col_beatmap_hash},
    {col_beatmap_md5},
    {col_xxy_sr},
    {col_pp}
)
VALUES(
    $id,
    $hash,
    $md5,
    $xxy_sr,
    $pp
)
ON CONFLICT({col_beatmap_id}) DO UPDATE SET
    {col_beatmap_hash} = excluded.{col_beatmap_hash},
    {col_beatmap_md5} = excluded.{col_beatmap_md5},
    {col_xxy_sr} = excluded.{col_xxy_sr},
    {col_pp} = excluded.{col_pp};
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

                var ppParam = insert.CreateParameter();
                ppParam.ParameterName = "$pp";
                insert.Parameters.Add(ppParam);

                foreach (var row in rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    idParam.Value = row.BeatmapId.ToString();
                    hashParam.Value = row.BeatmapHash;
                    md5Param.Value = row.BeatmapMd5;
                    xxySrParam.Value = row.XxySr;
                    ppParam.Value = row.Pp is double pp ? pp : DBNull.Value;
                    insert.ExecuteNonQuery();
                }

                cancellationToken.ThrowIfCancellationRequested();
                transaction.Commit();
                success = true;
            }
            finally
            {
                if (!success)
                {
                    try
                    {
                        SqliteConnection.ClearAllPools();

                        if (File.Exists(fullPath))
                            File.Delete(fullPath);
                    }
                    catch
                    {
                    }
                }
            }
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

                if (!prepareSongsBranchConnection(connection))
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
FROM {table_songs_branch_entry}
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

        public IReadOnlyDictionary<Guid, double> GetSongsBranchPpValues(string databasePath, IEnumerable<BeatmapInfo> beatmaps)
        {
            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return empty_pp_values;

            try
            {
                var beatmapList = beatmaps.Distinct().ToList();

                if (beatmapList.Count == 0)
                    return empty_pp_values;

                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return empty_pp_values;

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
SELECT beatmap_id, beatmap_hash, beatmap_md5, {col_pp}
FROM {table_songs_branch_entry}
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

                        if (!reader.IsDBNull(3))
                            resolvedValues[beatmapId] = reader.GetDouble(3);
                    }
                }

                return resolvedValues;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetSongsBranchPpValues failed.", Ez2ConfigManager.LOGGER_NAME);
                return empty_pp_values;
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

                if (!prepareSongsBranchConnection(connection))
                    return beatmapMd5Hashes;

                try
                {
                    using var collectionCommand = connection.CreateCommand();
                    collectionCommand.CommandText = $@"
SELECT beatmap_md5
FROM {table_songs_branch_source_collection};
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
                branchCommand.CommandText = $@"
SELECT beatmap_md5
FROM {table_songs_branch_entry};
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

        public IReadOnlySet<string> GetSongsBranchCollectionMatchingMd5Hashes(string databasePath, IEnumerable<string> candidateMd5Hashes)
        {
            var matchedMd5Hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return matchedMd5Hashes;

            var candidates = candidateMd5Hashes.Where(hash => !string.IsNullOrWhiteSpace(hash))
                                               .Distinct(StringComparer.OrdinalIgnoreCase)
                                               .ToList();

            if (candidates.Count == 0)
                return matchedMd5Hashes;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return matchedMd5Hashes;

                bool queriedSourceCollection = false;

                try
                {
                    queryMatchedMd5Hashes(connection, table_songs_branch_source_collection, candidates, matchedMd5Hashes);
                    queriedSourceCollection = true;
                }
                catch (SqliteException)
                {
                }

                if (!queriedSourceCollection || matchedMd5Hashes.Count == 0)
                    queryMatchedMd5Hashes(connection, table_songs_branch_entry, candidates, matchedMd5Hashes);

                return matchedMd5Hashes;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetSongsBranchCollectionMatchingMd5Hashes failed.", Ez2ConfigManager.LOGGER_NAME);
                return matchedMd5Hashes;
            }
        }

        private static void queryMatchedMd5Hashes(SqliteConnection connection, string tableName, IReadOnlyList<string> candidates, HashSet<string> output)
        {
            for (int offset = 0; offset < candidates.Count; offset += 800)
            {
                using var command = connection.CreateCommand();

                int batchCount = Math.Min(800, candidates.Count - offset);
                var parameterNames = new List<string>(batchCount);

                for (int i = 0; i < batchCount; i++)
                {
                    string parameterName = $"$md5{i}";
                    parameterNames.Add(parameterName);
                    command.Parameters.AddWithValue(parameterName, candidates[offset + i]);
                }

                command.CommandText = $@"
SELECT {col_beatmap_md5}
FROM {tableName}
WHERE {col_beatmap_md5} IN ({string.Join(", ", parameterNames)});
";

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string beatmapMd5 = reader.GetString(0);

                    if (!string.IsNullOrWhiteSpace(beatmapMd5))
                        output.Add(beatmapMd5);
                }
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

        /// <summary>
        /// 读取分支库 meta 中的 xxy 算法版本；缺失时返回 0（表示 legacy，默认视为当前算法）。
        /// </summary>
        public bool TryGetSongsBranchStoredXxyVersion(string databasePath, out int storedVersion)
        {
            storedVersion = 0;

            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return false;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return false;

                if (!int.TryParse(tryGetMeta(connection, meta_key_xxy_sr_version), NumberStyles.Integer, CultureInfo.InvariantCulture, out storedVersion))
                    storedVersion = 0;

                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TryGetSongsBranchStoredXxyVersion failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        /// <summary>
        /// 为 legacy 分支库补写 xxy / PP 算法版本 meta（不触发重算）。已有版本且落后时不修改。
        /// </summary>
        public void EnsureSongsBranchVersionMeta(string databasePath, int currentXxyVersion, int currentPpVersion)
        {
            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return;

            if (currentXxyVersion <= 0 && currentPpVersion <= 0)
                return;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return;

                if (currentXxyVersion > 0 && string.IsNullOrEmpty(tryGetMeta(connection, meta_key_xxy_sr_version)))
                    setMeta(connection, meta_key_xxy_sr_version, currentXxyVersion.ToString(CultureInfo.InvariantCulture));

                if (currentPpVersion > 0 && string.IsNullOrEmpty(tryGetMeta(connection, meta_key_pp_version)))
                    setMeta(connection, meta_key_pp_version, currentPpVersion.ToString(CultureInfo.InvariantCulture));

                setMeta(connection, meta_key_analysis_version, ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore EnsureSongsBranchVersionMeta failed.", Ez2ConfigManager.LOGGER_NAME);
            }
        }

        public void EnsureSongsBranchXxyVersionMeta(string databasePath, int currentXxyVersion)
            => EnsureSongsBranchVersionMeta(databasePath, currentXxyVersion, 0);

        public void EnsureSongsBranchPpVersionMeta(string databasePath, int currentPpVersion)
            => EnsureSongsBranchVersionMeta(databasePath, 0, currentPpVersion);

        public bool SongsBranchNeedsXxyRefresh(int storedXxyVersion, int currentXxyVersion)
        {
            if (currentXxyVersion <= 0)
                return false;

            // legacy 分支库（无 meta）默认按当前算法生成，首次打开会 stamp 而不重算（v1 迁移除外，见 requires_post_migration_refresh）
            if (storedXxyVersion <= 0)
                return false;

            return storedXxyVersion < currentXxyVersion;
        }

        public bool TryGetSongsBranchRequiresPostMigrationRefresh(string databasePath, out bool requiresRefresh)
        {
            requiresRefresh = false;

            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return false;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return false;

                requiresRefresh = getMetaBool(connection, meta_key_requires_post_migration_refresh);
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TryGetSongsBranchRequiresPostMigrationRefresh failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        public void ClearSongsBranchPostMigrationRefresh(string databasePath)
        {
            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return;

                deleteMeta(connection, meta_key_requires_post_migration_refresh);
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore ClearSongsBranchPostMigrationRefresh failed.", Ez2ConfigManager.LOGGER_NAME);
            }
        }

        public IReadOnlyList<SongsBranchRow> ReadAllSongsBranchRows(string databasePath)
        {
            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return Array.Empty<SongsBranchRow>();

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return Array.Empty<SongsBranchRow>();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = $@"
SELECT {col_beatmap_id}, {col_beatmap_hash}, {col_beatmap_md5}, {col_xxy_sr}, {col_pp}
FROM {table_songs_branch_entry};";

                var rows = new List<SongsBranchRow>();

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (!Guid.TryParse(reader.GetString(0), out Guid beatmapId))
                        continue;

                    rows.Add(new SongsBranchRow(
                        beatmapId,
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                        reader.IsDBNull(4) ? null : reader.GetDouble(4)));
                }

                return rows;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore ReadAllSongsBranchRows failed.", Ez2ConfigManager.LOGGER_NAME);
                return Array.Empty<SongsBranchRow>();
            }
        }

        public bool SongsBranchNeedsPpRefresh(int storedPpVersion, int currentPpVersion)
        {
            if (currentPpVersion <= 0)
                return false;

            if (storedPpVersion <= 0)
                return false;

            return storedPpVersion < currentPpVersion;
        }

        public bool TryGetSongsBranchStoredPpVersion(string databasePath, out int storedVersion)
        {
            storedVersion = 0;

            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return false;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return false;

                if (!int.TryParse(tryGetMeta(connection, meta_key_pp_version), NumberStyles.Integer, CultureInfo.InvariantCulture, out storedVersion))
                    storedVersion = 0;

                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TryGetSongsBranchStoredPpVersion failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        public bool UpdateSongsBranchRows(string databasePath, IEnumerable<SongsBranchRow> rows, int? newXxySrAlgorithmVersion, int? newPpAlgorithmVersion)
        {
            if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return false;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return false;

                using var transaction = connection.BeginTransaction();

                using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = $@"
UPDATE {table_songs_branch_entry}
SET {col_xxy_sr} = $xxy,
    {col_pp} = $pp
WHERE {col_beatmap_id} = $id;
";

                var idParam = update.CreateParameter();
                idParam.ParameterName = "$id";
                update.Parameters.Add(idParam);

                var xxyParam = update.CreateParameter();
                xxyParam.ParameterName = "$xxy";
                update.Parameters.Add(xxyParam);

                var ppParam = update.CreateParameter();
                ppParam.ParameterName = "$pp";
                update.Parameters.Add(ppParam);

                foreach (var row in rows)
                {
                    idParam.Value = row.BeatmapId.ToString();
                    xxyParam.Value = row.XxySr;
                    ppParam.Value = row.Pp.HasValue ? row.Pp.Value : DBNull.Value;
                    update.ExecuteNonQuery();
                }

                transaction.Commit();

                if (newXxySrAlgorithmVersion is > 0)
                    setMeta(connection, meta_key_xxy_sr_version, newXxySrAlgorithmVersion.Value.ToString(CultureInfo.InvariantCulture));

                if (newPpAlgorithmVersion is > 0)
                    setMeta(connection, meta_key_pp_version, newPpAlgorithmVersion.Value.ToString(CultureInfo.InvariantCulture));

                setMeta(connection, meta_key_analysis_version, ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore UpdateSongsBranchRows failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        // 备份分支库隐藏用法
//         public bool TrySetSongsBranchHideState(string databasePath, bool hiddenApplied, IEnumerable<Guid> preexistingHiddenBeatmapIds)
//         {
//             if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
//                 return false;
//
//             try
//             {
//                 using var connection = openConnection(databasePath);
//
//                 if (!prepareSongsBranchConnection(connection))
//                     return false;
//
//                 ensureSongsBranchStateTables(connection);
//
//                 using var transaction = connection.BeginTransaction();
//
//                 setMeta(connection, "hidden_applied", hiddenApplied ? "1" : "0");
//
//                 using (var delete = connection.CreateCommand())
//                 {
//                     delete.Transaction = transaction;
//                     delete.CommandText = "DELETE FROM hidden_preexisting_beatmap;";
//                     delete.ExecuteNonQuery();
//                 }
//
//                 using var insert = connection.CreateCommand();
//                 insert.Transaction = transaction;
//                 insert.CommandText = @"
// INSERT INTO hidden_preexisting_beatmap(beatmap_id)
// VALUES($id)
// ON CONFLICT(beatmap_id) DO NOTHING;
// ";
//
//                 var idParam = insert.CreateParameter();
//                 idParam.ParameterName = "$id";
//                 insert.Parameters.Add(idParam);
//
//                 foreach (Guid beatmapId in preexistingHiddenBeatmapIds.Distinct())
//                 {
//                     idParam.Value = beatmapId.ToString();
//                     insert.ExecuteNonQuery();
//                 }
//
//                 transaction.Commit();
//                 return true;
//             }
//             catch (Exception e)
//             {
//                 Logger.Error(e, "EzManiaAnalysisPersistentStore TrySetSongsBranchHideState failed.", Ez2ConfigManager.LOGGER_NAME);
//                 return false;
//             }
//         }

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

        /// <summary>
        /// 强制使全部已缓存 kps / KPC 失效（开发/维护用）。
        /// </summary>
        public bool TrySetForceRecompute(bool force)
        {
            if (!Enabled)
                return false;

            try
            {
                Initialise();

                using var connection = openConnection();

                if (force)
                    EzAnalysisSchemaManager.InvalidateAllCachedAnalysisTimestamps(connection);

                EzAnalysisSchemaManager.SetMeta(connection, EzAnalysisSchemaManager.META_KEY_FORCE_RECOMPUTE, "0");
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TrySetForceRecompute failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        /// <summary>
        /// 读取当前（主）数据库中是否启用了强制重建开关。
        /// </summary>
        public bool IsForceRecomputeEnabled()
        {
            try
            {
                Initialise();
                using var connection = openConnection();
                return EzAnalysisSchemaManager.GetMetaBool(connection, EzAnalysisSchemaManager.META_KEY_FORCE_RECOMPUTE);
            }
            catch
            {
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

                if (!prepareSongsBranchConnection(connection))
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

                if (!prepareSongsBranchConnection(connection))
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

            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys=ON;";
            pragma.ExecuteNonQuery();

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

        private static void deleteMeta(SqliteConnection connection, string key)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM meta WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
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

        private static void ensureMetaTableExists(SqliteConnection connection)
        {
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);";
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // ignore failures; callers will handle missing meta via tryGetMeta/setMeta error paths
            }
        }

        private static bool prepareSongsBranchConnection(SqliteConnection connection)
        {
            ensureMetaTableExists(connection);

            if (!looksLikeSongsBranchDatabase(connection))
                return false;

            ensureSongsBranchSchemaCurrent(connection);
            return isValidSongsBranchConnection(connection);
        }

        private static bool looksLikeSongsBranchDatabase(SqliteConnection connection)
        {
            string? kind = tryGetMeta(connection, meta_key_kind);

            if (string.Equals(kind, songs_branch_kind, StringComparison.Ordinal)
                || string.Equals(kind, legacy_xxy_sr_branch_kind, StringComparison.Ordinal))
                return true;

            return songsBranchEntryTableExists(connection, legacy_table_xxy_sr_branch)
                   || songsBranchEntryTableExists(connection, table_songs_branch_entry);
        }

        private static void ensureSongsBranchSchemaCurrent(SqliteConnection connection)
        {
            if (!looksLikeSongsBranchDatabase(connection))
                return;

            if (isCurrentSongsBranchSchema(connection))
                return;

            int legacySchemaVersion = readLegacySongsBranchSchemaVersion(connection);
            bool requiresFullRecompute = legacySongsBranchRequiresFullRecompute(connection, legacySchemaVersion);

            using var transaction = connection.BeginTransaction();

            if (songsBranchEntryTableExists(connection, legacy_table_xxy_sr_branch))
            {
                if (songsBranchEntryTableExists(connection, table_songs_branch_entry))
                {
                    bool legacyHasPp = songsBranchEntryColumnExists(connection, legacy_table_xxy_sr_branch, col_pp);
                    string legacyPpSelect = legacyHasPp ? col_pp : "NULL";

                    using (var copy = connection.CreateCommand())
                    {
                        copy.Transaction = transaction;
                        copy.CommandText = $@"
INSERT OR REPLACE INTO {table_songs_branch_entry}(
    {col_beatmap_id},
    {col_beatmap_hash},
    {col_beatmap_md5},
    {col_xxy_sr},
    {col_pp}
)
SELECT
    {col_beatmap_id},
    {col_beatmap_hash},
    {col_beatmap_md5},
    {col_xxy_sr},
    {legacyPpSelect}
FROM {legacy_table_xxy_sr_branch};
";
                        copy.ExecuteNonQuery();
                    }

                    using (var drop = connection.CreateCommand())
                    {
                        drop.Transaction = transaction;
                        drop.CommandText = $"DROP TABLE {legacy_table_xxy_sr_branch};";
                        drop.ExecuteNonQuery();
                    }
                }
                else
                {
                    using var rename = connection.CreateCommand();
                    rename.Transaction = transaction;
                    rename.CommandText = $"ALTER TABLE {legacy_table_xxy_sr_branch} RENAME TO {table_songs_branch_entry};";
                    rename.ExecuteNonQuery();
                }
            }

            if (songsBranchEntryTableExists(connection, table_songs_branch_entry)
                && !songsBranchEntryColumnExists(connection, table_songs_branch_entry, col_pp))
            {
                using var addPp = connection.CreateCommand();
                addPp.Transaction = transaction;
                addPp.CommandText = $"ALTER TABLE {table_songs_branch_entry} ADD COLUMN {col_pp} REAL NULL;";
                addPp.ExecuteNonQuery();
            }

            using (var ensureHidden = connection.CreateCommand())
            {
                ensureHidden.Transaction = transaction;
                ensureHidden.CommandText = $@"
CREATE TABLE IF NOT EXISTS {table_songs_branch_hidden_preexisting} (
    {col_beatmap_id} TEXT PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS {table_songs_branch_source_collection} (
    {col_beatmap_md5} TEXT PRIMARY KEY
);
";
                ensureHidden.ExecuteNonQuery();
            }

            setMeta(connection, meta_key_kind, songs_branch_kind);
            setMeta(connection, meta_key_schema_version, songs_branch_schema_version.ToString(CultureInfo.InvariantCulture));

            if (requiresFullRecompute)
            {
                deleteMeta(connection, meta_key_xxy_sr_version);
                deleteMeta(connection, meta_key_pp_version);
                setMeta(connection, meta_key_requires_post_migration_refresh, "1");
            }
            else
            {
                deleteMeta(connection, meta_key_requires_post_migration_refresh);
            }

            transaction.Commit();

            Logger.Log(
                requiresFullRecompute
                    ? $"[EzAnalysisPersistentStore] Migrated legacy songs branch schema v{legacySchemaVersion} to kind={songs_branch_kind}, schema v{songs_branch_schema_version}; scheduled full xxy/PP refresh."
                    : $"[EzAnalysisPersistentStore] Migrated legacy songs branch schema v{legacySchemaVersion} to kind={songs_branch_kind}, schema v{songs_branch_schema_version}; reusing cached xxy/PP.",
                Ez2ConfigManager.LOGGER_NAME,
                LogLevel.Important);
        }

        private static int readLegacySongsBranchSchemaVersion(SqliteConnection connection)
        {
            if (int.TryParse(tryGetMeta(connection, meta_key_schema_version), NumberStyles.Integer, CultureInfo.InvariantCulture, out int schemaVersion))
                return schemaVersion;

            return 0;
        }

        /// <summary>
        /// legacy schema v1（或无 pp 列的旧库）迁移后需全量重算；v2 及以后复用既有结果。
        /// </summary>
        private static bool legacySongsBranchRequiresFullRecompute(SqliteConnection connection, int legacySchemaVersion)
        {
            if (legacySchemaVersion >= 2)
                return false;

            if (legacySchemaVersion == 1)
                return true;

            if (songsBranchEntryTableExists(connection, legacy_table_xxy_sr_branch))
                return !songsBranchEntryColumnExists(connection, legacy_table_xxy_sr_branch, col_pp);

            if (songsBranchEntryTableExists(connection, table_songs_branch_entry))
                return !songsBranchEntryColumnExists(connection, table_songs_branch_entry, col_pp);

            return true;
        }

        private static bool isCurrentSongsBranchSchema(SqliteConnection connection)
        {
            string? kind = tryGetMeta(connection, meta_key_kind);

            if (!string.Equals(kind, songs_branch_kind, StringComparison.Ordinal))
                return false;

            if (!int.TryParse(tryGetMeta(connection, meta_key_schema_version), NumberStyles.Integer, CultureInfo.InvariantCulture, out int schemaVersion)
                || schemaVersion != songs_branch_schema_version)
                return false;

            return songsBranchEntryTableExists(connection, table_songs_branch_entry)
                   && !songsBranchEntryTableExists(connection, legacy_table_xxy_sr_branch);
        }

        private static bool isValidSongsBranchConnection(SqliteConnection connection)
            => isCurrentSongsBranchSchema(connection);

        private static bool songsBranchEntryTableExists(SqliteConnection connection, string tableName)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
            cmd.Parameters.AddWithValue("$name", tableName);
            return cmd.ExecuteScalar() != null;
        }

        private static bool songsBranchEntryColumnExists(SqliteConnection connection, string tableName, string columnName)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({tableName});";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool tryReadSongsBranchMetadata(SqliteConnection connection, out SongsBranchMetadata metadata)
        {
            metadata = default;

            string? rulesetOnlineIdText = tryGetMeta(connection, meta_key_ruleset_online_id);
            string? rulesetShortName = tryGetMeta(connection, meta_key_ruleset_short_name);
            string? modsFingerprint = tryGetMeta(connection, meta_key_mods_fingerprint);
            string? modsDisplay = tryGetMeta(connection, meta_key_mods_display);
            string? beatmapCountText = tryGetMeta(connection, meta_key_beatmap_count);
            string? createdAtText = tryGetMeta(connection, meta_key_created_at);
            string? displayName = tryGetMeta(connection, meta_key_display_name);
            string? modsJson = tryGetMeta(connection, meta_key_mods_json);
            string? hiddenAppliedText = tryGetMeta(connection, meta_key_hidden_applied);
            string? sourceCollectionIdText = tryGetMeta(connection, meta_key_source_collection_id);
            string? sourceCollectionName = tryGetMeta(connection, meta_key_source_collection_name);
            string? sourceCollectionLastModifiedText = tryGetMeta(connection, meta_key_source_collection_last_modified);
            string? sourceCollectionBeatmapCountText = tryGetMeta(connection, meta_key_source_collection_beatmap_count);

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
            cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS {table_songs_branch_hidden_preexisting} (
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
                        token.ThrowIfCancellationRequested();

                        using var connection = openConnection();
                        using var transaction = connection.BeginTransaction();

                        foreach (var kv in batch)
                        {
                            token.ThrowIfCancellationRequested();

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

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            var cancellationSource = writeCts;
            var writerTask = backgroundWriterTask;

            writeCts = null;
            backgroundWriterTask = null;

            cancellationSource?.Cancel();

            if (writerTask != null)
            {
                try
                {
                    writerTask.Wait(1000);
                }
                catch
                {
                }
            }

            cancellationSource?.Dispose();
            pendingWrites.Clear();
        }

        private void writePendingEntryToConnection(SqliteConnection connection, BeatmapInfo beatmap, EzAnalysisResult analysis, SqliteTransaction? transaction = null)
        {
            var commonSummary = analysis.CommonSummary;
            var maniaSummary = analysis.ManiaSummary;

            if (commonSummary == null)
                return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string kpsListJson = JsonSerializer.Serialize(commonSummary.Value.KpsList);

            using (var entry = connection.CreateCommand())
            {
                entry.Transaction = transaction;
                entry.CommandText = $@"
INSERT INTO {EzAnalysisSchemaManager.TABLE_ENTRY}(
    {EzAnalysisSchemaManager.COL_BEATMAP_ID},
    {EzAnalysisSchemaManager.COL_BEATMAP_HASH},
    {EzAnalysisSchemaManager.COL_BEATMAP_MD5},
    {EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID},
    {EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT},
    {EzAnalysisSchemaManager.COL_AVERAGE_KPS},
    {EzAnalysisSchemaManager.COL_MAX_KPS},
    {EzAnalysisSchemaManager.COL_KPS_LIST_JSON}
)
VALUES(
    $id,
    $hash,
    $md5,
    $ruleset,
    $common_updated_at,
    $avg,
    $max,
    $kps
)
ON CONFLICT({EzAnalysisSchemaManager.COL_BEATMAP_ID}) DO UPDATE SET
    {EzAnalysisSchemaManager.COL_BEATMAP_HASH} = excluded.{EzAnalysisSchemaManager.COL_BEATMAP_HASH},
    {EzAnalysisSchemaManager.COL_BEATMAP_MD5} = excluded.{EzAnalysisSchemaManager.COL_BEATMAP_MD5},
    {EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID} = excluded.{EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID},
    {EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT} = excluded.{EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT},
    {EzAnalysisSchemaManager.COL_AVERAGE_KPS} = excluded.{EzAnalysisSchemaManager.COL_AVERAGE_KPS},
    {EzAnalysisSchemaManager.COL_MAX_KPS} = excluded.{EzAnalysisSchemaManager.COL_MAX_KPS},
    {EzAnalysisSchemaManager.COL_KPS_LIST_JSON} = excluded.{EzAnalysisSchemaManager.COL_KPS_LIST_JSON};
";
                entry.Parameters.AddWithValue("$id", beatmap.ID.ToString());
                entry.Parameters.AddWithValue("$hash", beatmap.Hash);
                entry.Parameters.AddWithValue("$md5", beatmap.MD5Hash);
                entry.Parameters.AddWithValue("$ruleset", beatmap.Ruleset.OnlineID);
                entry.Parameters.AddWithValue("$common_updated_at", now);
                entry.Parameters.AddWithValue("$avg", analysis.AverageKps);
                entry.Parameters.AddWithValue("$max", analysis.MaxKps);
                entry.Parameters.AddWithValue("$kps", kpsListJson);
                entry.ExecuteNonQuery();
            }

            using (var mania = connection.CreateCommand())
            {
                mania.Transaction = transaction;

                if (maniaSummary == null)
                {
                    mania.CommandText = $@"
DELETE FROM {EzAnalysisSchemaManager.TABLE_MANIA}
WHERE {EzAnalysisSchemaManager.COL_BEATMAP_ID} = $id;
";
                    mania.Parameters.AddWithValue("$id", beatmap.ID.ToString());
                    mania.ExecuteNonQuery();
                    return;
                }

                string columnCountsJson = JsonSerializer.Serialize(maniaSummary.Value.ColumnCounts);
                string holdNoteCountsJson = JsonSerializer.Serialize(maniaSummary.Value.HoldNoteCounts);

                mania.CommandText = $@"
INSERT INTO {EzAnalysisSchemaManager.TABLE_MANIA}(
    {EzAnalysisSchemaManager.COL_BEATMAP_ID},
    {EzAnalysisSchemaManager.COL_UPDATED_AT},
    {EzAnalysisSchemaManager.COL_COLUMN_COUNTS_JSON},
    {EzAnalysisSchemaManager.COL_HOLD_NOTE_COUNTS_JSON}
)
VALUES(
    $id,
    $updated_at,
    $column_counts_json,
    $hold_note_counts_json
)
ON CONFLICT({EzAnalysisSchemaManager.COL_BEATMAP_ID}) DO UPDATE SET
    {EzAnalysisSchemaManager.COL_UPDATED_AT} = excluded.{EzAnalysisSchemaManager.COL_UPDATED_AT},
    {EzAnalysisSchemaManager.COL_COLUMN_COUNTS_JSON} = excluded.{EzAnalysisSchemaManager.COL_COLUMN_COUNTS_JSON},
    {EzAnalysisSchemaManager.COL_HOLD_NOTE_COUNTS_JSON} = excluded.{EzAnalysisSchemaManager.COL_HOLD_NOTE_COUNTS_JSON};
";
                mania.Parameters.AddWithValue("$id", beatmap.ID.ToString());
                mania.Parameters.AddWithValue("$updated_at", now);
                mania.Parameters.AddWithValue("$column_counts_json", columnCountsJson);
                mania.Parameters.AddWithValue("$hold_note_counts_json", holdNoteCountsJson);
                mania.ExecuteNonQuery();
            }
        }

        private static EzAnalysisResult mergeAnalysisResult(EzAnalysisResult storedAnalysis, EzAnalysisResult computedAnalysis)
            => computedAnalysis;

        internal static MissingDataKind GetMissingData(EzAnalysisResult? storedAnalysis, int rulesetOnlineId)
        {
            if (!storedAnalysis.HasValue)
                return getRequiredDataMask(rulesetOnlineId);

            MissingDataKind missingData = MissingDataKind.None;
            EzAnalysisResult analysis = storedAnalysis.Value;

            if (analysis.CommonSummary == null)
                missingData |= MissingDataKind.Common;

            if (rulesetOnlineId == 3 && analysis.ManiaSummary == null)
                missingData |= MissingDataKind.Mania;

            return missingData;
        }

        internal static bool RequiresAnalysisComputation(MissingDataKind missingData)
            => (missingData & (MissingDataKind.Common | MissingDataKind.Mania)) != MissingDataKind.None;

        private static MissingDataKind getMissingData(long commonUpdatedAt, bool hasManiaData, int rulesetOnlineId)
        {
            MissingDataKind missingData = MissingDataKind.None;

            if (commonUpdatedAt <= 0)
                missingData |= MissingDataKind.Common;

            if (rulesetOnlineId == 3 && !hasManiaData)
                missingData |= MissingDataKind.Mania;

            return missingData;
        }

        private static MissingDataKind getRequiredDataMask(int rulesetOnlineId)
        {
            MissingDataKind requiredData = MissingDataKind.Common;

            if (rulesetOnlineId == 3)
                requiredData |= MissingDataKind.Mania;

            return requiredData;
        }

        /// <summary>
        /// Validates that the analysis result contains reasonable values.
        /// </summary>
        private static bool isValidAnalysisResult(EzAnalysisResult result)
        {
            if (result.ManiaSummary?.XxySr is double xxySr && (double.IsNaN(xxySr) || double.IsInfinity(xxySr)))
                return false;

            return true;
        }
    }
}
