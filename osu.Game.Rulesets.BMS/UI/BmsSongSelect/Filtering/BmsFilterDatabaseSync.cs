// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Data.Sqlite;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Scoring.Lamp.Persistence;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering
{
    public sealed class BmsFilterDatabaseSync
    {
        private readonly string databasePath;
        private readonly object writeLock = new object();

        public BmsFilterDatabaseSync(string databasePath)
        {
            this.databasePath = databasePath;
        }

        public bool DatabaseExists => File.Exists(databasePath);

        public void Rebuild(
            BMSBeatmapManager beatmapManager,
            BmsLampSqliteRepository lampRepository,
            RealmAccess realm,
            BmsAnalyticsSqliteRepository? analytics = null,
            CancellationToken cancellationToken = default)
        {
            var lamps = lampRepository.LoadAll().ToDictionary(r => r.BeatmapId, r => r);
            int songCount = 0;

            lock (writeLock)
            {
                using var connection = openConnection();
                using var transaction = connection.BeginTransaction();

                executeNonQuery(connection, transaction, "DROP TABLE IF EXISTS scorelog;");
                executeNonQuery(connection, transaction, "DROP TABLE IF EXISTS score;");
                executeNonQuery(connection, transaction, "DROP TABLE IF EXISTS information;");
                executeNonQuery(connection, transaction, "DROP TABLE IF EXISTS song;");

                executeNonQuery(connection, transaction, BmsFilterSchema.CREATE_SONG);
                executeNonQuery(connection, transaction, BmsFilterSchema.CREATE_SCORE);
                executeNonQuery(connection, transaction, BmsFilterSchema.CREATE_SCORELOG);
                executeNonQuery(connection, transaction, BmsFilterSchema.CREATE_INFORMATION);

                using var songCmd = createInsertSongCommand(connection, transaction);
                using var scoreCmd = createInsertScoreCommand(connection, transaction);
                using var scoreLogCmd = createInsertScoreLogCommand(connection, transaction);
                using var infoCmd = createInsertInformationCommand(connection, transaction);

                const int page_size = 256;

                for (int offset = 0; ; offset += page_size)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    IReadOnlyList<BMSChartCache> charts = beatmapManager.GetChartPage(offset, page_size);

                    if (charts.Count == 0)
                        break;

                    var (songs, scores, informations) = BmsScoreSchemaBuilder.Build(charts, lamps, realm, analytics);
                    insertSongs(songCmd, songs);
                    insertScores(scoreCmd, scores);
                    insertScoreLogs(scoreLogCmd, scores);
                    insertInformation(infoCmd, informations);
                    songCount += songs.Count;

                    if (charts.Count < page_size)
                        break;
                }

                transaction.Commit();
            }

            beatmapManager.MarkFilterSynchronizedToCurrent();
            Logger.Log($"[BMS] Raja filter DB rebuilt: {songCount} songs.", LoggingTarget.Database);
        }

        public int ApplyPendingDelta(
            BMSBeatmapManager beatmapManager,
            BmsLampSqliteRepository lampRepository,
            RealmAccess realm,
            BmsAnalyticsSqliteRepository? analytics = null,
            CancellationToken cancellationToken = default)
        {
            if (!DatabaseExists)
            {
                Rebuild(beatmapManager, lampRepository, realm, analytics, cancellationToken);
                return beatmapManager.ChartCount;
            }

            int applied = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<BmsRealmSyncChange> changes = beatmapManager.GetPendingFilterSyncChanges(256);

                if (changes.Count == 0)
                    break;

                applied += applyChanges(beatmapManager, lampRepository, realm, analytics, changes, cancellationToken);
                beatmapManager.AcknowledgeFilterSyncChanges(changes.Select(change => change.Revision).ToList());
            }

            if (applied > 0)
                Logger.Log($"[BMS] Raja filter DB delta applied: {applied} charts.", LoggingTarget.Database);

            return applied;
        }

        private int applyChanges(
            BMSBeatmapManager beatmapManager,
            BmsLampSqliteRepository lampRepository,
            RealmAccess realm,
            BmsAnalyticsSqliteRepository? analytics,
            IReadOnlyList<BmsRealmSyncChange> changes,
            CancellationToken cancellationToken)
        {
            var upsertCharts = new List<BMSChartCache>();
            var deleteKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lampIds = new HashSet<Guid>();

            foreach (BmsRealmSyncChange change in changes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (change.Kind == BmsRealmSyncChangeKind.Delete)
                {
                    if (beatmapManager.TryGetSourceReference(change.BeatmapId, out BMSSourceReference reference))
                    {
                        if (!string.IsNullOrEmpty(reference.ContentSha256))
                            deleteKeys.Add(reference.ContentSha256);
                        deleteKeys.Add(reference.Md5Hash);
                    }
                    else if (!string.IsNullOrEmpty(change.ChartPath))
                        deleteKeys.Add(BmsPathKeys.ComputeChartPathKey(change.ChartPath));
                    continue;
                }

                if (!beatmapManager.TryGetChart(change.BeatmapId, out BMSChartCache chart))
                    continue;

                upsertCharts.Add(chart);
                lampIds.Add(change.BeatmapId);

                string pathKey = string.IsNullOrEmpty(chart.Md5Hash)
                    ? BmsPathKeys.ComputeChartPathKey(chart.FullPath)
                    : chart.Md5Hash;
                deleteKeys.Remove(pathKey);

                if (!string.IsNullOrEmpty(chart.ContentSha256))
                    deleteKeys.Remove(chart.ContentSha256);
            }

            var lamps = lampIds.Count == 0
                ? new Dictionary<Guid, BmsLampRecord>()
                : lampRepository.LoadAll().Where(record => lampIds.Contains(record.BeatmapId)).ToDictionary(record => record.BeatmapId);

            lock (writeLock)
            {
                using var connection = openConnection();
                ensureSchema(connection);
                using var transaction = connection.BeginTransaction();

                using var deleteSong = connection.CreateCommand();
                deleteSong.Transaction = transaction;
                deleteSong.CommandText = "DELETE FROM song WHERE sha256 = $sha;";
                deleteSong.Parameters.Add("$sha", SqliteType.Text);

                using var deleteScore = connection.CreateCommand();
                deleteScore.Transaction = transaction;
                deleteScore.CommandText = "DELETE FROM score WHERE sha256 = $sha;";
                deleteScore.Parameters.Add("$sha", SqliteType.Text);

                using var deleteScoreLog = connection.CreateCommand();
                deleteScoreLog.Transaction = transaction;
                deleteScoreLog.CommandText = "DELETE FROM scorelog WHERE sha256 = $sha;";
                deleteScoreLog.Parameters.Add("$sha", SqliteType.Text);

                using var deleteInfo = connection.CreateCommand();
                deleteInfo.Transaction = transaction;
                deleteInfo.CommandText = "DELETE FROM information WHERE sha256 = $sha;";
                deleteInfo.Parameters.Add("$sha", SqliteType.Text);

                foreach (string pathKey in deleteKeys)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    deleteSong.Parameters["$sha"].Value = pathKey;
                    deleteScore.Parameters["$sha"].Value = pathKey;
                    deleteScoreLog.Parameters["$sha"].Value = pathKey;
                    deleteInfo.Parameters["$sha"].Value = pathKey;
                    deleteSong.ExecuteNonQuery();
                    deleteScore.ExecuteNonQuery();
                    deleteScoreLog.ExecuteNonQuery();
                    deleteInfo.ExecuteNonQuery();
                }

                if (upsertCharts.Count > 0)
                {
                    foreach (BMSChartCache chart in upsertCharts)
                    {
                        string pathKey = string.IsNullOrEmpty(chart.Md5Hash)
                            ? BmsPathKeys.ComputeChartPathKey(chart.FullPath)
                            : chart.Md5Hash;
                        string contentSha = string.IsNullOrEmpty(chart.ContentSha256) ? pathKey : chart.ContentSha256;

                        foreach (string key in new[] { contentSha, pathKey }.Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            deleteSong.Parameters["$sha"].Value = key;
                            deleteScore.Parameters["$sha"].Value = key;
                            deleteScoreLog.Parameters["$sha"].Value = key;
                            deleteInfo.Parameters["$sha"].Value = key;
                            deleteSong.ExecuteNonQuery();
                            deleteScore.ExecuteNonQuery();
                            deleteScoreLog.ExecuteNonQuery();
                            deleteInfo.ExecuteNonQuery();
                        }
                    }

                    var (songs, scores, informations) = BmsScoreSchemaBuilder.Build(upsertCharts, lamps, realm, analytics);
                    using var songCmd = createInsertSongCommand(connection, transaction);
                    using var scoreCmd = createInsertScoreCommand(connection, transaction);
                    using var scoreLogCmd = createInsertScoreLogCommand(connection, transaction);
                    using var infoCmd = createInsertInformationCommand(connection, transaction);
                    insertSongs(songCmd, songs);
                    insertScores(scoreCmd, scores);
                    insertScoreLogs(scoreLogCmd, scores);
                    insertInformation(infoCmd, informations);
                }

                transaction.Commit();
            }

            return upsertCharts.Count + deleteKeys.Count;
        }

        private void ensureSchema(SqliteConnection connection)
        {
            executeNonQuery(connection, null, BmsFilterSchema.CREATE_SONG);
            executeNonQuery(connection, null, BmsFilterSchema.CREATE_SCORE);
            executeNonQuery(connection, null, BmsFilterSchema.CREATE_SCORELOG);
            executeNonQuery(connection, null, BmsFilterSchema.CREATE_INFORMATION);
        }

        private static SqliteCommand createInsertSongCommand(SqliteConnection connection, SqliteTransaction transaction)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
INSERT INTO song (md5, sha256, title, subtitle, genre, artist, subartist, path, folder, parent, level, difficulty, mode, notes, favorite, maxbpm, minbpm, length, date, adddate)
VALUES ($md5, $sha256, $title, $subtitle, $genre, $artist, $subartist, $path, $folder, $parent, $level, $difficulty, $mode, $notes, $favorite, $maxbpm, $minbpm, $length, 0, 0);";
            cmd.Parameters.Add("$md5", SqliteType.Text);
            cmd.Parameters.Add("$sha256", SqliteType.Text);
            cmd.Parameters.Add("$title", SqliteType.Text);
            cmd.Parameters.Add("$subtitle", SqliteType.Text);
            cmd.Parameters.Add("$genre", SqliteType.Text);
            cmd.Parameters.Add("$artist", SqliteType.Text);
            cmd.Parameters.Add("$subartist", SqliteType.Text);
            cmd.Parameters.Add("$path", SqliteType.Text);
            cmd.Parameters.Add("$folder", SqliteType.Text);
            cmd.Parameters.Add("$parent", SqliteType.Text);
            cmd.Parameters.Add("$level", SqliteType.Integer);
            cmd.Parameters.Add("$difficulty", SqliteType.Integer);
            cmd.Parameters.Add("$mode", SqliteType.Integer);
            cmd.Parameters.Add("$notes", SqliteType.Integer);
            cmd.Parameters.Add("$favorite", SqliteType.Integer);
            cmd.Parameters.Add("$maxbpm", SqliteType.Integer);
            cmd.Parameters.Add("$minbpm", SqliteType.Integer);
            cmd.Parameters.Add("$length", SqliteType.Integer);
            cmd.Prepare();
            return cmd;
        }

        private static SqliteCommand createInsertScoreCommand(SqliteConnection connection, SqliteTransaction transaction)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
INSERT INTO score (sha256, mode, clear, playcount, clearcount, epg, lpg, egr, lgr, egd, lgd, ebd, lbd, epr, lpr, ems, lms, notes, combo, minbp, avgjudge, date)
VALUES ($sha256, $mode, $clear, $playcount, $clearcount, $epg, $lpg, $egr, $lgr, 0, 0, 0, 0, 0, 0, 0, 0, $notes, $combo, $minbp, 2147483647, 0);";
            addScoreParameters(cmd);
            cmd.Prepare();
            return cmd;
        }

        private static SqliteCommand createInsertScoreLogCommand(SqliteConnection connection, SqliteTransaction transaction)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
INSERT INTO scorelog (sha256, mode, clear, playcount, clearcount, epg, lpg, egr, lgr, notes, combo, minbp, date)
VALUES ($sha256, $mode, $clear, $playcount, $clearcount, $epg, $lpg, $egr, $lgr, $notes, $combo, $minbp, 0);";
            addScoreParameters(cmd);
            cmd.Prepare();
            return cmd;
        }

        private static SqliteCommand createInsertInformationCommand(SqliteConnection connection, SqliteTransaction transaction)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
INSERT INTO information (sha256, n, ln, s, ls, total, density, peakdensity, enddensity, mainbpm)
VALUES ($sha256, $n, $ln, $s, $ls, $total, $density, $peakdensity, $enddensity, $mainbpm);";
            cmd.Parameters.Add("$sha256", SqliteType.Text);
            cmd.Parameters.Add("$n", SqliteType.Integer);
            cmd.Parameters.Add("$ln", SqliteType.Integer);
            cmd.Parameters.Add("$s", SqliteType.Integer);
            cmd.Parameters.Add("$ls", SqliteType.Integer);
            cmd.Parameters.Add("$total", SqliteType.Real);
            cmd.Parameters.Add("$density", SqliteType.Real);
            cmd.Parameters.Add("$peakdensity", SqliteType.Real);
            cmd.Parameters.Add("$enddensity", SqliteType.Real);
            cmd.Parameters.Add("$mainbpm", SqliteType.Real);
            cmd.Prepare();
            return cmd;
        }

        private static void insertSongs(SqliteCommand cmd, IReadOnlyList<BmsSongRow> songs)
        {
            foreach (var song in songs)
            {
                cmd.Parameters["$md5"].Value = song.Md5;
                cmd.Parameters["$sha256"].Value = song.Sha256;
                cmd.Parameters["$title"].Value = song.Title;
                cmd.Parameters["$subtitle"].Value = song.Subtitle;
                cmd.Parameters["$genre"].Value = song.Genre;
                cmd.Parameters["$artist"].Value = song.Artist;
                cmd.Parameters["$subartist"].Value = song.Subartist;
                cmd.Parameters["$path"].Value = song.Path;
                cmd.Parameters["$folder"].Value = song.Folder;
                cmd.Parameters["$parent"].Value = BmsPathCrc.Compute(song.Folder);
                cmd.Parameters["$level"].Value = song.Level;
                cmd.Parameters["$difficulty"].Value = song.Difficulty;
                cmd.Parameters["$mode"].Value = song.Mode;
                cmd.Parameters["$notes"].Value = song.Notes;
                cmd.Parameters["$favorite"].Value = song.Favorite;
                cmd.Parameters["$maxbpm"].Value = song.MaxBpm;
                cmd.Parameters["$minbpm"].Value = song.MinBpm;
                cmd.Parameters["$length"].Value = song.Length;
                cmd.ExecuteNonQuery();
            }
        }

        private static void insertScores(SqliteCommand cmd, IReadOnlyList<BmsScoreRow> scores)
        {
            foreach (var score in scores)
            {
                setScoreParameters(cmd, score);
                cmd.ExecuteNonQuery();
            }
        }

        private static void insertScoreLogs(SqliteCommand cmd, IReadOnlyList<BmsScoreRow> scores)
        {
            foreach (var score in scores)
            {
                setScoreParameters(cmd, score);
                cmd.ExecuteNonQuery();
            }
        }

        private static void addScoreParameters(SqliteCommand cmd)
        {
            cmd.Parameters.Add("$sha256", SqliteType.Text);
            cmd.Parameters.Add("$mode", SqliteType.Integer);
            cmd.Parameters.Add("$clear", SqliteType.Integer);
            cmd.Parameters.Add("$playcount", SqliteType.Integer);
            cmd.Parameters.Add("$clearcount", SqliteType.Integer);
            cmd.Parameters.Add("$epg", SqliteType.Integer);
            cmd.Parameters.Add("$lpg", SqliteType.Integer);
            cmd.Parameters.Add("$egr", SqliteType.Integer);
            cmd.Parameters.Add("$lgr", SqliteType.Integer);
            cmd.Parameters.Add("$notes", SqliteType.Integer);
            cmd.Parameters.Add("$combo", SqliteType.Integer);
            cmd.Parameters.Add("$minbp", SqliteType.Integer);
        }

        private static void setScoreParameters(SqliteCommand cmd, BmsScoreRow score)
        {
            cmd.Parameters["$sha256"].Value = score.Sha256;
            cmd.Parameters["$mode"].Value = score.Mode;
            cmd.Parameters["$clear"].Value = score.Clear;
            cmd.Parameters["$playcount"].Value = score.Playcount;
            cmd.Parameters["$clearcount"].Value = score.Clearcount;
            cmd.Parameters["$epg"].Value = score.Epg;
            cmd.Parameters["$lpg"].Value = score.Lpg;
            cmd.Parameters["$egr"].Value = score.Egr;
            cmd.Parameters["$lgr"].Value = score.Lgr;
            cmd.Parameters["$notes"].Value = score.Notes;
            cmd.Parameters["$combo"].Value = score.Combo;
            cmd.Parameters["$minbp"].Value = score.Minbp;
        }

        private static void insertInformation(SqliteCommand cmd, IReadOnlyList<BmsInformationRow> rows)
        {
            foreach (var row in rows)
            {
                cmd.Parameters["$sha256"].Value = row.Sha256;
                cmd.Parameters["$n"].Value = row.N;
                cmd.Parameters["$ln"].Value = row.Ln;
                cmd.Parameters["$s"].Value = row.S;
                cmd.Parameters["$ls"].Value = row.Ls;
                cmd.Parameters["$total"].Value = row.Total;
                cmd.Parameters["$density"].Value = row.Density;
                cmd.Parameters["$peakdensity"].Value = row.PeakDensity;
                cmd.Parameters["$enddensity"].Value = row.EndDensity;
                cmd.Parameters["$mainbpm"].Value = row.MainBpm;
                cmd.ExecuteNonQuery();
            }
        }

        private static void executeNonQuery(SqliteConnection connection, SqliteTransaction? transaction, string sql)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private SqliteConnection openConnection()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            pragma.ExecuteNonQuery();
            return connection;
        }
    }
}
