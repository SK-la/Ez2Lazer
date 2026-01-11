// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;

namespace osu.Game.LAsEzExtensions.Analysis.Persistence
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
    public class EzManiaAnalysisPersistentStore
    {
        /// <summary>
        /// 持久化总开关（默认关闭）：未来考虑是否允许用户通过配置关闭此功能以避免额外的磁盘读写。
        /// </summary>
        public static bool Enabled = true;

        public static readonly string DATABASE_FILENAME = $@"mania-analysis_v{ANALYSIS_VERSION}.sqlite";

        // 手动维护：算法/序列化格式变更时递增。版本发生变化时，会强制重算所有已存条目。
        // 注意：此版本号与 osu! 官方服务器端的版本号无关，仅用于本地持久化存储的失效控制。
        // 注意：更新版本号后，务必通过注释保存旧版本的变更记录，方便日后排查问题。
        // v2: 初始版本，包含 kps_list_json, column_counts_json
        // v3: 添加 hold_note_counts_json 字段，分离普通note和长按note统计
        // v4: 添加 beatmap_md5 校验字段；kps_list_json 仅保存用于 UI 的下采样曲线（<=256 点）。
        // v5: 删除scratchText存储，改为动态计算。数据库可兼容，不升版。
        public const int ANALYSIS_VERSION = 5;

        private static readonly string[] ALLOWED_COLUMNS = {
            "beatmap_id",
            "beatmap_hash",
            "beatmap_md5",
            "analysis_version",
            "average_kps",
            "max_kps",
            "kps_list_json",
            "xxy_sr",
            "column_counts_json",
            "hold_note_counts_json"
        };

        private readonly Storage storage;
        private readonly object initLock = new object();

        private bool initialised;
        private string dbPath = string.Empty;

        // Old versions earlier than v3 may not have sufficient data to safely upgrade without recomputation.
        // v3 introduced hold note counts, which are relied upon by parts of the UI.
        private const int min_inplace_upgrade_version = 3;

        public EzManiaAnalysisPersistentStore(Storage storage)
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

                    Logger.Log($"EzManiaAnalysisPersistentStore path: {dbPath}", "mania_analysis", LogLevel.Important);

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

                    // 检查并清理不需要的列（处理版本升级时删除的字段）
                    cleanupUnrecognizedColumns(connection);

                    initialised = true;
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

        public bool TryGet(BeatmapInfo beatmap, bool requireXxySr, out ManiaBeatmapAnalysisResult result, out bool missingRequiredXxySr)
        {
            result = ManiaBeatmapAnalysisDefaults.EMPTY;
            missingRequiredXxySr = false;

            if (!Enabled)
                return false;

            try
            {
                Initialise();

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
                    Logger.Log($"[EzManiaAnalysisPersistentStore] stored_hash mismatch for {beatmap.ID}: stored={storedHash} runtime={beatmap.Hash}", "mania_analysis", LogLevel.Debug);
                    return false;
                }

                // md5 validation:
                // - If stored md5 is empty (older versions), accept hash match and upgrade in-place.
                // - If stored md5 is present, require it to match.
                if (!string.IsNullOrEmpty(storedMd5) && !string.Equals(storedMd5, beatmap.MD5Hash, StringComparison.Ordinal))
                {
                    Logger.Log($"[EzManiaAnalysisPersistentStore] stored_md5 mismatch for {beatmap.ID}: stored={storedMd5} runtime={beatmap.MD5Hash}", "mania_analysis", LogLevel.Debug);
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

                    if (mutated || storedVersion < ANALYSIS_VERSION)
                    {
                        // Persist the upgraded row (without recomputing analysis).
                        writeUpgradedRow(connection, beatmap, averageKps, maxKps, kpsList, xxySr, columnCounts, holdNoteCounts);
                    }
                }

                // Compute scratchText since it's not stored
                int keyCount = columnCounts.Count;
                string computedScratchText = EzBeatmapCalculator.GetScratchFromPrecomputed(columnCounts, maxKps, kpsList, keyCount);

                result = new ManiaBeatmapAnalysisResult(
                    averageKps,
                    maxKps,
                    kpsList,
                    columnCounts,
                    holdNoteCounts,
                    computedScratchText,
                    xxySr);

                missingRequiredXxySr = requireXxySr && xxySr == null;

                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TryGet failed.");
                return false;
            }
        }

        public void Store(BeatmapInfo beatmap, ManiaBeatmapAnalysisResult analysis)
        {
            if (!Enabled)
                return;

            try
            {
                Initialise();

                using var connection = openConnection();
                using var cmd = connection.CreateCommand();

                string kpsListJson = JsonSerializer.Serialize(analysis.KpsList);
                string columnCountsJson = JsonSerializer.Serialize(analysis.ColumnCounts);
                string holdNoteCountsJson = JsonSerializer.Serialize(analysis.HoldNoteCounts);

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

                if (analysis.XxySr.HasValue)
                    cmd.Parameters.AddWithValue("$xxy", analysis.XxySr.Value);
                else
                    cmd.Parameters.AddWithValue("$xxy", DBNull.Value);

                cmd.Parameters.AddWithValue("$cols", columnCountsJson);
                cmd.Parameters.AddWithValue("$holds", holdNoteCountsJson);

                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore Store failed.");
            }
        }

        public IReadOnlyList<Guid> GetBeatmapsNeedingRecompute(IEnumerable<(Guid id, string hash)> beatmaps)
            => GetBeatmapsNeedingRecompute(beatmaps, progress: null);

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

        private SqliteConnection openConnection()
        {
            // 这里每次操作打开一个连接，避免跨线程复用连接导致的问题。
            var connection = new SqliteConnection($"Data Source={dbPath};Cache=Shared;Mode=ReadWriteCreate");
            connection.Open();
            return connection;
        }

        private void setMeta(SqliteConnection connection, string key, string value)
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

        private static bool canUpgradeInPlace(int storedVersion)
            => storedVersion >= min_inplace_upgrade_version && storedVersion <= ANALYSIS_VERSION;

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
                Logger.Log($"[EzManiaAnalysisPersistentStore] Cloned DB from v{bestVersion} to v{ANALYSIS_VERSION}: {Path.GetFileName(bestCandidate)} -> {Path.GetFileName(dbPath)}", LoggingTarget.Database);
            }
            catch (Exception e)
            {
                // If cloning fails, we simply fall back to creating a fresh DB and recomputing as needed.
                Logger.Error(e, "[EzManiaAnalysisPersistentStore] Failed to clone previous DB; falling back to fresh database.");
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
            update.Parameters.AddWithValue("$md5", beatmap.MD5Hash ?? string.Empty);
            update.Parameters.AddWithValue("$version", ANALYSIS_VERSION);
            update.Parameters.AddWithValue("$kps_list_json", kpsListJson);
            update.Parameters.AddWithValue("$xxy_sr", xxySr is null ? DBNull.Value : xxySr.Value);
            update.Parameters.AddWithValue("$column_counts_json", columnCountsJson);
            update.Parameters.AddWithValue("$hold_note_counts_json", holdNoteCountsJson);

            update.ExecuteNonQuery();
        }

        private void cleanupUnrecognizedColumns(SqliteConnection connection)
        {
            var existingColumns = getTableColumns(connection, "mania_analysis");
            var unrecognizedColumns = existingColumns.Where(c => !ALLOWED_COLUMNS.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();

            if (unrecognizedColumns.Count == 0)
                return;

            // 重建表，删除不识别的列
            Logger.Log($"[EzManiaAnalysisPersistentStore] Found unrecognized columns: {string.Join(", ", unrecognizedColumns)}; rebuilding table.", LoggingTarget.Database);

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
                // PRAGMA table_info: cid, name, type, notnull, dflt_value, pk
                string name = reader.GetString(1);
                if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
