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
            Pp = 1 << 1,
            Tag = 1 << 2,
            Mania = 1 << 3,
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

        public static readonly string DATABASE_FILENAME = $@"ez-analysis_v{ANALYSIS_VERSION}.sqlite";

        public const string SONGS_BRANCH_DATABASE_DIRECTORY = "EzData";
        private const string xxy_sr_branch_kind = "xxy_sr_branch";
        private const int xxy_sr_branch_schema_version = 2;

        // 兼容策略：v3 及以前版本不考虑就地兼容。只有 v4（及以后）才考虑就地升级以避免重算。
        private const int min_inplace_upgrade_version = 4;

        // 手动维护：算法/序列化格式变更时递增。版本发生变化时，会强制重算所有已存条目。
        // 注意：此版本号与 osu! 官方服务器端的版本号无关，仅用于本地持久化存储的失效控制。
        // 注意：更新版本号后，务必通过注释保存旧版本的变更记录，方便日后排查问题。
        // v2: 初始版本，包含 kps_list_json, column_counts_json
        // v3: 添加 hold_note_counts_json 字段，分离普通note和长按note统计
        // v4: 添加 beatmap_md5 校验字段；kps_list_json 仅保存用于 UI 的下采样曲线（<=256 点）。
        // v5: 删除scratchText存储，改为动态计算。数据库可兼容，不升版。
        // v6: 重建为主体主表 + mania 扩展表 + tag group 的切片结构，修改LN因子数值为5.5。
        // v6.1: 增加PP字段，追加数据，不升版。
        public const int ANALYSIS_VERSION = 6;

        // 列定义集中管理：避免在代码中到处硬写列名，便于审计与迁移。
        private record ColumnInfo(string Name, bool Required = true);

        private const string col_beatmap_id = "beatmap_id";
        private const string col_beatmap_hash = "beatmap_hash";
        private const string col_beatmap_md5 = "beatmap_md5";
        private const string col_analysis_version = "analysis_version";
        private const string col_average_kps = "kps_avg";
        private const string col_max_kps = "kps_max";
        private const string col_kps_list_json = "kps_list_json";
        private const string col_xxy_sr = "xxy_sr";
        private const string col_pp = "pp";
        private const string col_column_counts_json = "column_counts_json";
        private const string col_hold_note_counts_json = "hold_note_counts_json";
        private const string col_last_updated = "last_updated";

        private static readonly ColumnInfo[] allowed_columns_info = new[]
        {
            new ColumnInfo(col_beatmap_id),
            new ColumnInfo(col_beatmap_hash),
            new ColumnInfo(col_beatmap_md5),
            new ColumnInfo(col_analysis_version),
            new ColumnInfo(col_average_kps),
            new ColumnInfo(col_max_kps),
            new ColumnInfo(col_kps_list_json),
            new ColumnInfo(col_xxy_sr),
            new ColumnInfo(col_pp),
            new ColumnInfo(col_column_counts_json),
            new ColumnInfo(col_hold_note_counts_json),
            new ColumnInfo(col_last_updated)
        };

        // 为方便快速比较，也保留名字数组供现有逻辑使用。
        private static readonly string[] allowed_column_names = allowed_columns_info.Select(c => c.Name).ToArray();

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
        private const string table_mania_analysis = "mania_analysis";
        private const string meta_key_force_recompute = "force_recompute";
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
        private const string table_xxy_sr_branch = "xxy_sr_branch";
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

                dbPath = storage.GetFullPath(DATABASE_FILENAME, true);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

                    Logger.Log($"EzManiaAnalysisPersistentStore path: {dbPath}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

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

                if (EzAnalysisSchemaManager.GetMetaBool(connection, EzAnalysisSchemaManager.META_KEY_FORCE_RECOMPUTE))
                    return false;

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
                        if (pending.Analysis.ManiaSummary?.XxySr is double pendingXxySr)
                            resolvedValues[beatmap.ID] = pendingXxySr;

                        continue;
                    }

                    idsNeedingDatabaseLookup.Add(beatmap.ID);
                }

                if (idsNeedingDatabaseLookup.Count == 0)
                    return resolvedValues;

                using var connection = openConnection();

                if (EzAnalysisSchemaManager.GetMetaBool(connection, EzAnalysisSchemaManager.META_KEY_FORCE_RECOMPUTE))
                    return resolvedValues.Count == 0 ? empty_xxy_sr_values : resolvedValues;

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
SELECT entry.{EzAnalysisSchemaManager.COL_BEATMAP_ID},
       entry.{EzAnalysisSchemaManager.COL_BEATMAP_HASH},
       entry.{EzAnalysisSchemaManager.COL_BEATMAP_MD5},
       mania.{EzAnalysisSchemaManager.COL_XXY_SR}
FROM {EzAnalysisSchemaManager.TABLE_ENTRY} entry
JOIN {EzAnalysisSchemaManager.TABLE_MANIA} mania
    ON mania.{EzAnalysisSchemaManager.COL_BEATMAP_ID} = entry.{EzAnalysisSchemaManager.COL_BEATMAP_ID}
WHERE entry.{EzAnalysisSchemaManager.COL_BEATMAP_ID} IN ({string.Join(", ", parameterNames)})
    AND mania.{EzAnalysisSchemaManager.COL_UPDATED_AT} > 0
    AND mania.{EzAnalysisSchemaManager.COL_XXY_SR} IS NOT NULL;
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

                        resolvedValues[beatmapId] = reader.GetDouble(3);
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

        public IReadOnlyDictionary<Guid, double> GetStoredPpValues(IEnumerable<BeatmapInfo> beatmaps)
        {
            if (!Enabled)
                return empty_pp_values;

            try
            {
                Initialise();

                var beatmapList = beatmaps.Distinct().ToList();

                if (beatmapList.Count == 0)
                    return empty_pp_values;

                var beatmapsById = beatmapList.ToDictionary(b => b.ID);
                var resolvedValues = new Dictionary<Guid, double>(beatmapList.Count);
                var idsNeedingDatabaseLookup = new List<Guid>(beatmapList.Count);

                foreach (var beatmap in beatmapList)
                {
                    if (pendingWrites.TryGetValue(beatmap.ID, out var pending)
                        && string.Equals(pending.Beatmap.Hash, beatmap.Hash, StringComparison.Ordinal))
                    {
                        if (pending.Analysis.Pp is double pendingPp)
                            resolvedValues[beatmap.ID] = pendingPp;

                        continue;
                    }

                    idsNeedingDatabaseLookup.Add(beatmap.ID);
                }

                if (idsNeedingDatabaseLookup.Count == 0)
                    return resolvedValues;

                using var connection = openConnection();

                if (EzAnalysisSchemaManager.GetMetaBool(connection, EzAnalysisSchemaManager.META_KEY_FORCE_RECOMPUTE))
                    return resolvedValues.Count == 0 ? empty_pp_values : resolvedValues;

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
SELECT entry.{EzAnalysisSchemaManager.COL_BEATMAP_ID},
       entry.{EzAnalysisSchemaManager.COL_BEATMAP_HASH},
       entry.{EzAnalysisSchemaManager.COL_BEATMAP_MD5},
       entry.{EzAnalysisSchemaManager.COL_PP}
FROM {EzAnalysisSchemaManager.TABLE_ENTRY} entry
WHERE entry.{EzAnalysisSchemaManager.COL_BEATMAP_ID} IN ({string.Join(", ", parameterNames)})
    AND entry.{EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT} > 0
    AND entry.{EzAnalysisSchemaManager.COL_PP} IS NOT NULL;
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

                        resolvedValues[beatmapId] = reader.GetDouble(3);
                    }
                }

                return resolvedValues;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetStoredPpValues failed.", Ez2ConfigManager.LOGGER_NAME);
                return empty_pp_values;
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

                if (EzAnalysisSchemaManager.GetMetaBool(connection, EzAnalysisSchemaManager.META_KEY_FORCE_RECOMPUTE))
                {
                    Store(beatmap, analysis);
                    return;
                }

                // 尝试从 SQLite 读取旧数据
                if (!tryGetRawData(connection, beatmap, out var storedAnalysis))
                {
                    // 缓存不存在，直接存储
                    Store(beatmap, analysis);
                    return;
                }

                if (analysis.TagSummary == null && storedAnalysis.TagSummary != null)
                    analysis = analysis.WithTagSummary(storedAnalysis.TagSummary);

                // 对比两个结果是否有差异
                MissingDataKind filledMissingData = GetMissingData(storedAnalysis, beatmap.Ruleset.OnlineID, requireTagData: true) &
                                                    ~GetMissingData(analysis, beatmap.Ruleset.OnlineID, requireTagData: true);

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
    entry.{EzAnalysisSchemaManager.COL_PP},
       entry.{EzAnalysisSchemaManager.COL_TAG_UPDATED_AT},
       entry.{EzAnalysisSchemaManager.COL_TAG_PAYLOAD_JSON},
       mania.{EzAnalysisSchemaManager.COL_UPDATED_AT},
       mania.{EzAnalysisSchemaManager.COL_XXY_SR},
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
                    double? pp = reader.IsDBNull(7) ? null : reader.GetDouble(7);
                    long tagUpdatedAt = reader.GetInt64(8);
                    string tagPayloadJson = reader.GetString(9);
                    long maniaUpdatedAt = reader.IsDBNull(10) ? 0 : reader.GetInt64(10);
                    double? xxySr = reader.IsDBNull(11) ? null : reader.GetDouble(11);
                    string columnCountsJson = reader.IsDBNull(12) ? "{}" : reader.GetString(12);
                    string holdNoteCountsJson = reader.IsDBNull(13) ? "{}" : reader.GetString(13);

                    var columnCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(columnCountsJson) ?? new Dictionary<int, int>();
                    var holdNoteCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(holdNoteCountsJson) ?? new Dictionary<int, int>();
                    var kpsList = JsonSerializer.Deserialize<List<double>>(kpsListJson) ?? new List<double>();

                    EzManiaSummary? maniaSummary = storedRulesetOnlineId == 3 && maniaUpdatedAt > 0
                        ? new EzManiaSummary(columnCounts, holdNoteCounts, xxySr)
                        : null;

                    EzBeatmapTagSummary? tagSummary = deserializeTagSummary(tagPayloadJson, tagUpdatedAt);

                    result = new EzAnalysisResult(new KpsSummary(averageKps, maxKps, kpsList), pp, maniaSummary, tagSummary);

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
        /// 比较两个分析结果是否有差异。
        /// 关键字段：xxysr, averageKps, maxKps, ColumnCounts, HoldNoteCounts
        /// </summary>
        private bool hasDifference(EzAnalysisResult stored, EzAnalysisResult computed)
        {
            var storedCommonSummary = stored.CommonSummary;
            var computedCommonSummary = computed.CommonSummary;
            var storedManiaSummary = stored.ManiaSummary;
            var computedManiaSummary = computed.ManiaSummary;
            double? storedXxySr = storedManiaSummary?.XxySr;
            double? computedXxySr = computedManiaSummary?.XxySr;
            double? storedPp = stored.Pp;
            double? computedPp = computed.Pp;

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

            if (!storedPp.HasValue && computedPp.HasValue)
                return true;

            if (storedPp.HasValue && computedPp.HasValue)
            {
                if (!storedPp.Value.Equals(computedPp.Value))
                    return true;
            }

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

            if (stored.TagSummary != computed.TagSummary)
                return true;

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

            return GetMissingData(storedAnalysis, beatmap.Ruleset.OnlineID, requireTagData: true) != MissingDataKind.None;
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

                var existing = new Dictionary<Guid, (string hash, int rulesetOnlineId, long commonUpdatedAt, long tagUpdatedAt, bool hasPp)>();
                var maniaUpdated = new HashSet<Guid>();

                bool forceRecompute;

                using (var connection = openConnection())
                {
                    using (var entryCommand = connection.CreateCommand())
                    {
                        entryCommand.CommandText = $@"
SELECT {EzAnalysisSchemaManager.COL_BEATMAP_ID},
       {EzAnalysisSchemaManager.COL_BEATMAP_HASH},
       {EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID},
       {EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT},
       {EzAnalysisSchemaManager.COL_TAG_UPDATED_AT},
       {EzAnalysisSchemaManager.COL_PP}
FROM {EzAnalysisSchemaManager.TABLE_ENTRY};
";

                        using var reader = entryCommand.ExecuteReader();

                        while (reader.Read())
                        {
                            if (!Guid.TryParse(reader.GetString(0), out var id))
                                continue;

                            existing[id] = (reader.GetString(1), reader.GetInt32(2), reader.GetInt64(3), reader.GetInt64(4), !reader.IsDBNull(5));
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

                    forceRecompute = EzAnalysisSchemaManager.GetMetaBool(connection, EzAnalysisSchemaManager.META_KEY_FORCE_RECOMPUTE);
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

                    MissingDataKind missingData = getMissingData(row.commonUpdatedAt, row.tagUpdatedAt, row.hasPp, maniaUpdated.Contains(id), rulesetOnlineId, requireTagData: true);

                    if (!string.Equals(row.hash, hash, StringComparison.Ordinal)
                        || row.rulesetOnlineId != rulesetOnlineId
                        || missingData != MissingDataKind.None
                        || forceRecompute)
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
                                     CancellationToken cancellationToken = default)
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
CREATE TABLE IF NOT EXISTS {table_xxy_sr_branch} (
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

                setMeta(connection, meta_key_kind, xxy_sr_branch_kind);
                setMeta(connection, meta_key_schema_version, xxy_sr_branch_schema_version.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_key_analysis_version, ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));
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
INSERT INTO {table_xxy_sr_branch}(
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
FROM {table_xxy_sr_branch}
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

                if (!isValidSongsBranchConnection(connection))
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
FROM {table_xxy_sr_branch}
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

                if (!isValidSongsBranchConnection(connection))
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
FROM {table_xxy_sr_branch};
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

                if (!isValidSongsBranchConnection(connection))
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
                    queryMatchedMd5Hashes(connection, table_xxy_sr_branch, candidates, matchedMd5Hashes);

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
//                 if (!isValidSongsBranchConnection(connection))
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
        /// 在当前（主）数据库中设置或清除强制重建开关（meta.force_recompute = "1" / "0"）。
        /// 用途：当某个版本需要强制重建全部数据时，可手动开启，下一次确认兼容后再关闭。
        /// </summary>
        public bool TrySetForceRecompute(bool force)
        {
            if (!Enabled)
                return false;

            try
            {
                Initialise();

                using var connection = openConnection();
                EzAnalysisSchemaManager.SetMeta(connection, EzAnalysisSchemaManager.META_KEY_FORCE_RECOMPUTE, force ? "1" : "0");
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

        private static bool isValidSongsBranchConnection(SqliteConnection connection)
        {
            string? kind = tryGetMeta(connection, meta_key_kind);
            string? schemaVersionText = tryGetMeta(connection, meta_key_schema_version);
            string? analysisVersionText = tryGetMeta(connection, meta_key_analysis_version);

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

        /// <summary>
        /// 就地升级判断：仅在满足版本阈值（v4 及以后）且表结构与期待列一致时允许就地升级。
        /// </summary>
        private static bool canUpgradeInPlace(int storedVersion, bool schemaCompatible)
        {
            if (storedVersion > ANALYSIS_VERSION)
                return false;

            if (storedVersion < min_inplace_upgrade_version)
                return false;

            return schemaCompatible;
        }

        /// <summary>
        /// 比对数据库表的列名是否与预期完全一致（忽略大小写与顺序）。
        /// 如果表不存在或查询失败，返回 false。
        /// </summary>
        private static bool isTableSchemaCompatible(SqliteConnection connection, string tableName)
        {
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"PRAGMA table_info({tableName});";
                using var reader = cmd.ExecuteReader();

                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                while (reader.Read())
                {
                    // PRAGMA table_info returns: cid, name, type, notnull, dflt_value, pk
                    existing.Add(reader.GetString(1));
                }

                var expected = new HashSet<string>(allowed_column_names, StringComparer.OrdinalIgnoreCase);
                return existing.SetEquals(expected);
            }
            catch
            {
                return false;
            }
        }

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
            update.CommandText = $@"
UPDATE {table_mania_analysis}
SET {col_beatmap_md5} = $md5,
    {col_analysis_version} = $version,
    {col_kps_list_json} = $kps_list_json,
    {col_xxy_sr} = $xxy_sr,
    {col_column_counts_json} = $column_counts_json,
    {col_hold_note_counts_json} = $hold_note_counts_json
WHERE {col_beatmap_id} = $id;
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
            string tagPayloadJson = serializeTagSummary(analysis.TagSummary);

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
    {EzAnalysisSchemaManager.COL_KPS_LIST_JSON},
    {EzAnalysisSchemaManager.COL_PP},
    {EzAnalysisSchemaManager.COL_TAG_UPDATED_AT},
    {EzAnalysisSchemaManager.COL_TAG_PAYLOAD_JSON}
)
VALUES(
    $id,
    $hash,
    $md5,
    $ruleset,
    $common_updated_at,
    $avg,
    $max,
    $kps,
    $pp,
    $tag_updated_at,
    $tag_payload_json
)
ON CONFLICT({EzAnalysisSchemaManager.COL_BEATMAP_ID}) DO UPDATE SET
    {EzAnalysisSchemaManager.COL_BEATMAP_HASH} = excluded.{EzAnalysisSchemaManager.COL_BEATMAP_HASH},
    {EzAnalysisSchemaManager.COL_BEATMAP_MD5} = excluded.{EzAnalysisSchemaManager.COL_BEATMAP_MD5},
    {EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID} = excluded.{EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID},
    {EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT} = excluded.{EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT},
    {EzAnalysisSchemaManager.COL_AVERAGE_KPS} = excluded.{EzAnalysisSchemaManager.COL_AVERAGE_KPS},
    {EzAnalysisSchemaManager.COL_MAX_KPS} = excluded.{EzAnalysisSchemaManager.COL_MAX_KPS},
    {EzAnalysisSchemaManager.COL_KPS_LIST_JSON} = excluded.{EzAnalysisSchemaManager.COL_KPS_LIST_JSON},
    {EzAnalysisSchemaManager.COL_PP} = excluded.{EzAnalysisSchemaManager.COL_PP},
    {EzAnalysisSchemaManager.COL_TAG_UPDATED_AT} = excluded.{EzAnalysisSchemaManager.COL_TAG_UPDATED_AT},
    {EzAnalysisSchemaManager.COL_TAG_PAYLOAD_JSON} = excluded.{EzAnalysisSchemaManager.COL_TAG_PAYLOAD_JSON};
";
                entry.Parameters.AddWithValue("$id", beatmap.ID.ToString());
                entry.Parameters.AddWithValue("$hash", beatmap.Hash);
                entry.Parameters.AddWithValue("$md5", beatmap.MD5Hash);
                entry.Parameters.AddWithValue("$ruleset", beatmap.Ruleset.OnlineID);
                entry.Parameters.AddWithValue("$common_updated_at", now);
                entry.Parameters.AddWithValue("$avg", analysis.AverageKps);
                entry.Parameters.AddWithValue("$max", analysis.MaxKps);
                entry.Parameters.AddWithValue("$kps", kpsListJson);
                entry.Parameters.AddWithValue("$pp", analysis.Pp is double pp ? pp : DBNull.Value);
                entry.Parameters.AddWithValue("$tag_updated_at", analysis.TagSummary != null ? now : 0);
                entry.Parameters.AddWithValue("$tag_payload_json", tagPayloadJson);
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
    {EzAnalysisSchemaManager.COL_XXY_SR},
    {EzAnalysisSchemaManager.COL_COLUMN_COUNTS_JSON},
    {EzAnalysisSchemaManager.COL_HOLD_NOTE_COUNTS_JSON}
)
VALUES(
    $id,
    $updated_at,
    $xxy,
    $column_counts_json,
    $hold_note_counts_json
)
ON CONFLICT({EzAnalysisSchemaManager.COL_BEATMAP_ID}) DO UPDATE SET
    {EzAnalysisSchemaManager.COL_UPDATED_AT} = excluded.{EzAnalysisSchemaManager.COL_UPDATED_AT},
    {EzAnalysisSchemaManager.COL_XXY_SR} = excluded.{EzAnalysisSchemaManager.COL_XXY_SR},
    {EzAnalysisSchemaManager.COL_COLUMN_COUNTS_JSON} = excluded.{EzAnalysisSchemaManager.COL_COLUMN_COUNTS_JSON},
    {EzAnalysisSchemaManager.COL_HOLD_NOTE_COUNTS_JSON} = excluded.{EzAnalysisSchemaManager.COL_HOLD_NOTE_COUNTS_JSON};
";
                mania.Parameters.AddWithValue("$id", beatmap.ID.ToString());
                mania.Parameters.AddWithValue("$updated_at", now);
                mania.Parameters.AddWithValue("$xxy", maniaSummary.Value.XxySr is double xxySr ? xxySr : DBNull.Value);
                mania.Parameters.AddWithValue("$column_counts_json", columnCountsJson);
                mania.Parameters.AddWithValue("$hold_note_counts_json", holdNoteCountsJson);
                mania.ExecuteNonQuery();
            }
        }

        private static EzAnalysisResult mergeAnalysisResult(EzAnalysisResult storedAnalysis, EzAnalysisResult computedAnalysis)
            => computedAnalysis.TagSummary == null
                ? computedAnalysis.WithTagSummary(storedAnalysis.TagSummary)
                : computedAnalysis;

        internal static MissingDataKind GetMissingData(EzAnalysisResult? storedAnalysis, int rulesetOnlineId, bool requireTagData)
        {
            if (!storedAnalysis.HasValue)
                return getRequiredDataMask(rulesetOnlineId, requireTagData);

            MissingDataKind missingData = MissingDataKind.None;
            EzAnalysisResult analysis = storedAnalysis.Value;

            if (analysis.CommonSummary == null)
                missingData |= MissingDataKind.Common;

            if (analysis.Pp == null)
                missingData |= MissingDataKind.Pp;

            if (requireTagData && analysis.TagSummary == null)
                missingData |= MissingDataKind.Tag;

            if (rulesetOnlineId == 3 && analysis.ManiaSummary == null)
                missingData |= MissingDataKind.Mania;

            return missingData;
        }

        internal static bool RequiresAnalysisComputation(MissingDataKind missingData)
            => (missingData & (MissingDataKind.Common | MissingDataKind.Pp | MissingDataKind.Mania)) != MissingDataKind.None;

        private static MissingDataKind getMissingData(long commonUpdatedAt, long tagUpdatedAt, bool hasPp, bool hasManiaData, int rulesetOnlineId, bool requireTagData)
        {
            MissingDataKind missingData = MissingDataKind.None;

            if (commonUpdatedAt <= 0)
                missingData |= MissingDataKind.Common;

            if (!hasPp)
                missingData |= MissingDataKind.Pp;

            if (requireTagData && tagUpdatedAt <= 0)
                missingData |= MissingDataKind.Tag;

            if (rulesetOnlineId == 3 && !hasManiaData)
                missingData |= MissingDataKind.Mania;

            return missingData;
        }

        private static MissingDataKind getRequiredDataMask(int rulesetOnlineId, bool requireTagData)
        {
            MissingDataKind requiredData = MissingDataKind.Common | MissingDataKind.Pp;

            if (requireTagData)
                requiredData |= MissingDataKind.Tag;

            if (rulesetOnlineId == 3)
                requiredData |= MissingDataKind.Mania;

            return requiredData;
        }

        private static string serializeTagSummary(EzBeatmapTagSummary? tagSummary)
            => tagSummary is EzBeatmapTagSummary value
                ? JsonSerializer.Serialize(value)
                : string.Empty;

        private static EzBeatmapTagSummary? deserializeTagSummary(string tagPayloadJson, long tagUpdatedAt)
        {
            if (tagUpdatedAt <= 0)
                return null;

            if (string.IsNullOrWhiteSpace(tagPayloadJson))
                return EzBeatmapTagSummary.EMPTY;

            return JsonSerializer.Deserialize<EzBeatmapTagSummary>(tagPayloadJson);
        }

        private void cleanupUnrecognizedColumns(SqliteConnection connection)
        {
            var existingColumns = getTableColumns(connection, table_mania_analysis);
            var unrecognizedColumns = existingColumns.Where(c => !allowed_column_names.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();

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
            createTempCmd.CommandText = $@"
CREATE TABLE {table_mania_analysis}_temp (
    {col_beatmap_id} TEXT PRIMARY KEY,
    {col_beatmap_hash} TEXT NOT NULL,
    {col_beatmap_md5} TEXT NOT NULL,
    {col_analysis_version} INTEGER NOT NULL,
    {col_average_kps} REAL NOT NULL,
    {col_max_kps} REAL NOT NULL,
    {col_kps_list_json} TEXT NOT NULL,
    {col_xxy_sr} REAL NULL,
    {col_column_counts_json} TEXT NOT NULL,
    {col_hold_note_counts_json} TEXT NOT NULL
);
";
            createTempCmd.ExecuteNonQuery();

            // 复制数据，只复制允许的列
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = $@"
INSERT INTO {table_mania_analysis}_temp ({col_beatmap_id}, {col_beatmap_hash}, {col_beatmap_md5}, {col_analysis_version}, {col_average_kps}, {col_max_kps}, {col_kps_list_json}, {col_xxy_sr}, {col_column_counts_json}, {col_hold_note_counts_json})
SELECT {col_beatmap_id}, {col_beatmap_hash}, {col_beatmap_md5}, {col_analysis_version}, {col_average_kps}, {col_max_kps}, {col_kps_list_json}, {col_xxy_sr}, {col_column_counts_json}, {col_hold_note_counts_json}
FROM {table_mania_analysis};
";
            insertCmd.ExecuteNonQuery();

            // 删除旧表
            using var dropCmd = connection.CreateCommand();
            dropCmd.CommandText = $"DROP TABLE {table_mania_analysis};";
            dropCmd.ExecuteNonQuery();

            // 重命名临时表
            using var renameCmd = connection.CreateCommand();
            renameCmd.CommandText = $"ALTER TABLE {table_mania_analysis}_temp RENAME TO {table_mania_analysis};";
            renameCmd.ExecuteNonQuery();

            // 重新创建索引
            using var indexCmd = connection.CreateCommand();
            indexCmd.CommandText = $"CREATE INDEX IF NOT EXISTS idx_mania_analysis_version ON {table_mania_analysis}({col_analysis_version});";
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
            if (result.ManiaSummary?.XxySr is double xxySr && (double.IsNaN(xxySr) || double.IsInfinity(xxySr)))
                return false;

            return true;
        }
    }
}
