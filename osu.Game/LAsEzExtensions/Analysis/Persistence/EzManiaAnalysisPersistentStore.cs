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
        /// 持久化总开关（默认关闭）：
        /// - 你在调试 xxySR 算法时建议保持 false，确保每次都走实时计算，不会被旧的持久化结果“遮住”。
        /// - 等算法稳定后再改成 true，即可启用 SQLite 持久化 + 版本号增量预热。
        /// </summary>
        public static bool Enabled = false;

        public const string DATABASE_FILENAME = @"mania-analysis.sqlite";

        // 手动维护：算法/序列化格式变更时递增。
        public const int ANALYSIS_VERSION = 2;

        private readonly Storage storage;
        private readonly object initLock = new object();

        private bool initialised;
        private string dbPath = string.Empty;

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
    analysis_version INTEGER NOT NULL,
    average_kps REAL NOT NULL,
    max_kps REAL NOT NULL,
    kps_list_json TEXT NOT NULL,
    scratch_text TEXT NOT NULL,
    xxy_sr REAL NULL,
    column_counts_json TEXT NOT NULL
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

                    // Store the current analysis version as meta (informational).
                    setMeta(connection, "analysis_version", ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));

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

        public bool TryGet(BeatmapInfo beatmap, out ManiaBeatmapAnalysisResult result)
        {
            result = ManiaBeatmapAnalysisDefaults.EMPTY;

            if (!Enabled)
                return false;

            try
            {
                Initialise();

                using var connection = openConnection();
                using var cmd = connection.CreateCommand();

                cmd.CommandText = @"
SELECT beatmap_hash, analysis_version, average_kps, max_kps, kps_list_json, scratch_text, xxy_sr, column_counts_json
FROM mania_analysis
WHERE beatmap_id = $id
LIMIT 1;
";
                cmd.Parameters.AddWithValue("$id", beatmap.ID.ToString());

                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                    return false;

                string storedHash = reader.GetString(0);
                int storedVersion = reader.GetInt32(1);

                if (!string.Equals(storedHash, beatmap.Hash, StringComparison.Ordinal) || storedVersion != ANALYSIS_VERSION)
                    return false;

                double averageKps = reader.GetDouble(2);
                double maxKps = reader.GetDouble(3);
                string kpsListJson = reader.GetString(4);
                string scratchText = reader.GetString(5);

                double? xxySr = reader.IsDBNull(6) ? null : reader.GetDouble(6);
                string columnCountsJson = reader.GetString(7);

                var columnCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(columnCountsJson) ?? new Dictionary<int, int>();

                var kpsList = JsonSerializer.Deserialize<List<double>>(kpsListJson) ?? new List<double>();

                result = new ManiaBeatmapAnalysisResult(
                    averageKps,
                    maxKps,
                    kpsList,
                    columnCounts,
                    scratchText,
                    xxySr);

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

                cmd.CommandText = @"
INSERT INTO mania_analysis(
    beatmap_id,
    beatmap_hash,
    analysis_version,
    average_kps,
    max_kps,
    kps_list_json,
    scratch_text,
    xxy_sr,
    column_counts_json
)
VALUES(
    $id,
    $hash,
    $version,
    $avg,
    $max,
    $kps,
    $scratch,
    $xxy,
    $cols
)
ON CONFLICT(beatmap_id) DO UPDATE SET
    beatmap_hash = excluded.beatmap_hash,
    analysis_version = excluded.analysis_version,
    average_kps = excluded.average_kps,
    max_kps = excluded.max_kps,
    kps_list_json = excluded.kps_list_json,
    scratch_text = excluded.scratch_text,
    xxy_sr = excluded.xxy_sr,
    column_counts_json = excluded.column_counts_json;
";

                cmd.Parameters.AddWithValue("$id", beatmap.ID.ToString());
                cmd.Parameters.AddWithValue("$hash", beatmap.Hash);
                cmd.Parameters.AddWithValue("$version", ANALYSIS_VERSION);
                cmd.Parameters.AddWithValue("$avg", analysis.AverageKps);
                cmd.Parameters.AddWithValue("$max", analysis.MaxKps);
                cmd.Parameters.AddWithValue("$kps", kpsListJson);
                cmd.Parameters.AddWithValue("$scratch", analysis.ScratchText ?? string.Empty);

                if (analysis.XxySr.HasValue)
                    cmd.Parameters.AddWithValue("$xxy", analysis.XxySr.Value);
                else
                    cmd.Parameters.AddWithValue("$xxy", DBNull.Value);

                cmd.Parameters.AddWithValue("$cols", columnCountsJson);

                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore Store failed.");
            }
        }

        public IReadOnlyList<Guid> GetBeatmapsNeedingRecompute(IEnumerable<(Guid id, string hash)> beatmaps)
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

                foreach (var (id, hash) in beatmapList)
                {
                    if (!existing.TryGetValue(id, out var row))
                    {
                        needing.Add(id);
                        continue;
                    }

                    if (row.version != ANALYSIS_VERSION || !string.Equals(row.hash, hash, StringComparison.Ordinal))
                        needing.Add(id);
                }

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
