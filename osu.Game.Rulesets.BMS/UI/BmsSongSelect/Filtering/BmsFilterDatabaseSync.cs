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

        public void Rebuild(
            BMSBeatmapManager beatmapManager,
            BmsLampSqliteRepository lampRepository,
            RealmAccess realm,
            BmsAnalyticsSqliteRepository? analytics = null)
        {
            var charts = beatmapManager.GetAllCharts().ToList();
            var lamps = lampRepository.LoadAll().ToDictionary(r => r.BeatmapId, r => r);

            var (songs, scores, informations) = BmsScoreSchemaBuilder.Build(charts, lamps, realm, analytics);

            lock (writeLock)
            {
                using var connection = openConnection();
                using var transaction = connection.BeginTransaction();

                executeNonQuery(connection, "DROP TABLE IF EXISTS scorelog;");
                executeNonQuery(connection, "DROP TABLE IF EXISTS score;");
                executeNonQuery(connection, "DROP TABLE IF EXISTS information;");
                executeNonQuery(connection, "DROP TABLE IF EXISTS song;");

                executeNonQuery(connection, BmsFilterSchema.CREATE_SONG);
                executeNonQuery(connection, BmsFilterSchema.CREATE_SCORE);
                executeNonQuery(connection, BmsFilterSchema.CREATE_SCORELOG);
                executeNonQuery(connection, BmsFilterSchema.CREATE_INFORMATION);

                insertSongs(connection, songs);
                insertScores(connection, scores);
                insertScoreLogs(connection, scores);
                insertInformation(connection, informations);

                transaction.Commit();
            }

            Logger.Log($"[BMS] Raja filter DB rebuilt: {songs.Count} songs.", LoggingTarget.Database);
        }

        private static void insertSongs(SqliteConnection connection, IReadOnlyList<BmsSongRow> songs)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO song (md5, sha256, title, subtitle, genre, artist, subartist, path, folder, parent, level, difficulty, mode, notes, favorite, maxbpm, minbpm, length, date, adddate)
VALUES ($md5, $sha256, $title, $subtitle, $genre, $artist, $subartist, $path, $folder, $parent, $level, $difficulty, $mode, $notes, $favorite, $maxbpm, $minbpm, $length, 0, 0);";

            foreach (var song in songs)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$md5", song.Md5);
                cmd.Parameters.AddWithValue("$sha256", song.Sha256);
                cmd.Parameters.AddWithValue("$title", song.Title);
                cmd.Parameters.AddWithValue("$subtitle", song.Subtitle);
                cmd.Parameters.AddWithValue("$genre", song.Genre);
                cmd.Parameters.AddWithValue("$artist", song.Artist);
                cmd.Parameters.AddWithValue("$subartist", song.Subartist);
                cmd.Parameters.AddWithValue("$path", song.Path);
                cmd.Parameters.AddWithValue("$folder", song.Folder);
                cmd.Parameters.AddWithValue("$parent", BmsPathCrc.Compute(song.Folder));
                cmd.Parameters.AddWithValue("$level", song.Level);
                cmd.Parameters.AddWithValue("$difficulty", song.Difficulty);
                cmd.Parameters.AddWithValue("$mode", song.Mode);
                cmd.Parameters.AddWithValue("$notes", song.Notes);
                cmd.Parameters.AddWithValue("$favorite", song.Favorite);
                cmd.Parameters.AddWithValue("$maxbpm", song.MaxBpm);
                cmd.Parameters.AddWithValue("$minbpm", song.MinBpm);
                cmd.Parameters.AddWithValue("$length", song.Length);
                cmd.ExecuteNonQuery();
            }
        }

        private static void insertScores(SqliteConnection connection, IReadOnlyList<BmsScoreRow> scores)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO score (sha256, mode, clear, playcount, clearcount, epg, lpg, egr, lgr, egd, lgd, ebd, lbd, epr, lpr, ems, lms, notes, combo, minbp, avgjudge, date)
VALUES ($sha256, $mode, $clear, $playcount, $clearcount, $epg, $lpg, $egr, $lgr, 0, 0, 0, 0, 0, 0, 0, 0, $notes, $combo, $minbp, 2147483647, 0);";

            foreach (var score in scores)
            {
                cmd.Parameters.Clear();
                addScoreParameters(cmd, score);
                cmd.ExecuteNonQuery();
            }
        }

        private static void insertScoreLogs(SqliteConnection connection, IReadOnlyList<BmsScoreRow> scores)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO scorelog (sha256, mode, clear, playcount, clearcount, epg, lpg, egr, lgr, notes, combo, minbp, date)
VALUES ($sha256, $mode, $clear, $playcount, $clearcount, $epg, $lpg, $egr, $lgr, $notes, $combo, $minbp, 0);";

            foreach (var score in scores)
            {
                cmd.Parameters.Clear();
                addScoreParameters(cmd, score);
                cmd.ExecuteNonQuery();
            }
        }

        private static void addScoreParameters(SqliteCommand cmd, BmsScoreRow score)
        {
            cmd.Parameters.AddWithValue("$sha256", score.Sha256);
            cmd.Parameters.AddWithValue("$mode", score.Mode);
            cmd.Parameters.AddWithValue("$clear", score.Clear);
            cmd.Parameters.AddWithValue("$playcount", score.Playcount);
            cmd.Parameters.AddWithValue("$clearcount", score.Clearcount);
            cmd.Parameters.AddWithValue("$epg", score.Epg);
            cmd.Parameters.AddWithValue("$lpg", score.Lpg);
            cmd.Parameters.AddWithValue("$egr", score.Egr);
            cmd.Parameters.AddWithValue("$lgr", score.Lgr);
            cmd.Parameters.AddWithValue("$notes", score.Notes);
            cmd.Parameters.AddWithValue("$combo", score.Combo);
            cmd.Parameters.AddWithValue("$minbp", score.Minbp);
        }

        private static void insertInformation(SqliteConnection connection, IReadOnlyList<BmsInformationRow> rows)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO information (sha256, n, ln, s, ls, total, density, peakdensity, enddensity, mainbpm)
VALUES ($sha256, $n, $ln, $s, $ls, $total, $density, $peakdensity, $enddensity, $mainbpm);";

            foreach (var row in rows)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$sha256", row.Sha256);
                cmd.Parameters.AddWithValue("$n", row.N);
                cmd.Parameters.AddWithValue("$ln", row.Ln);
                cmd.Parameters.AddWithValue("$s", row.S);
                cmd.Parameters.AddWithValue("$ls", row.Ls);
                cmd.Parameters.AddWithValue("$total", row.Total);
                cmd.Parameters.AddWithValue("$density", row.Density);
                cmd.Parameters.AddWithValue("$peakdensity", row.PeakDensity);
                cmd.Parameters.AddWithValue("$enddensity", row.EndDensity);
                cmd.Parameters.AddWithValue("$mainbpm", row.MainBpm);
                cmd.ExecuteNonQuery();
            }
        }

        private static void executeNonQuery(SqliteConnection connection, string sql)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private SqliteConnection openConnection()
        {
            var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            pragma.ExecuteNonQuery();
            return connection;
        }
    }
}
