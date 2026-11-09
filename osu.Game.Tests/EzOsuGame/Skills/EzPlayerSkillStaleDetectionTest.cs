// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzPlayerSkillStaleDetectionTest : RealmTest
    {
        [Test]
        public void Superseded_algorithm_version_reports_stale_in_status_readout()
        {
            RunTestWithRealm((_, storage) =>
            {
                ensureLocalProfileSchema(storage);

                int current = EzAnalysisSystem.Mina.CurrentVersion();
                int obsolete = Math.Max(1, current - 1);

                if (obsolete >= current)
                    Assert.Inconclusive("Mina engine version has no prior revision to supersede.");

                insertSkillRow(storage, "superseded-user", obsolete, analyzedPlays: 3);

                var store = new EzPlayerSkillStore(storage);

                Assert.That(store.GetStalePlayerSkillUsernames(), Does.Contain("superseded-user"));
            });
        }

        [Test]
        public void Ledger_ahead_of_analyzed_plays_reports_stale()
        {
            RunTestWithRealm((_, storage) =>
            {
                var store = new EzPlayerSkillStore(storage);
                store.WritePlayerSsr("ledger-user", 4, new EzMinaSkillAxisVector(10, 1, 2, 3, 4, 5, 6, 7), analyzedPlays: 1);

                for (int i = 0; i < 3; i++)
                    insertDrillRow(storage, "ledger-user", keyCount: 4);

                Assert.That(store.GetStalePlayerSkillUsernames(), Does.Contain("ledger-user"));
            });
        }

        private static void ensureLocalProfileSchema(Storage storage)
            => new EzPlayerSkillStore(storage).GetStalePlayerSkillUsernames();

        private static void insertSkillRow(Storage storage, string username, int algorithmVersion, int analyzedPlays)
        {
            string path = storage.GetFullPath(EzLocalProfileStore.DATABASE_FILENAME, true);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            using var connection = new SqliteConnection($"Data Source={path};Mode=ReadWriteCreate");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO player_skill_value
                                  (username, key_count, system_id, ruleset_online_id, skill_id, value, state, analyzed_plays,
                                   provisional, algorithm_version, computed_at_ms)
                              VALUES
                                  ($username, 4, $system, $ruleset, 'overall', 10, 0, $plays, 0, $version, $at);
                              """;
            cmd.Parameters.AddWithValue("$username", username);
            cmd.Parameters.AddWithValue("$system", EzAnalysisSystem.Mina.ToStorageId());
            cmd.Parameters.AddWithValue("$ruleset", EzLocalProfileConstants.MANIA_RULESET_ID);
            cmd.Parameters.AddWithValue("$plays", analyzedPlays);
            cmd.Parameters.AddWithValue("$version", algorithmVersion);
            cmd.Parameters.AddWithValue("$at", now);
            cmd.ExecuteNonQuery();

            SqliteConnection.ClearAllPools();
        }

        private static void insertDrillRow(Storage storage, string username, int keyCount)
        {
            string path = storage.GetFullPath(EzLocalProfileStore.DATABASE_FILENAME, true);
            var scoreId = Guid.NewGuid();

            using var connection = new SqliteConnection($"Data Source={path};Mode=ReadWriteCreate");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO drill_scores (
                                  score_id, score_hash, username, ruleset_id, rank, pp_resolved, accuracy,
                                  max_combo, max_achievable_combo, total_score, mods_json, total_keys,
                                  beatmap_hash, beatmap_id, title, artist, difficulty_name,
                                  mapper_username, beatmap_status, star_rating, ssr_star_rating, map_performance_points,
                                  kps_avg, kps_max, kps_list_json, column_counts_json, hold_counts_json,
                                  has_video, has_storyboard, date_ms, bpm, key_count, is_convert, rate, mod_acronyms_json)
                              VALUES (
                                  $score_id, 'hash', $username, $ruleset, 4, 1.0, 1.0,
                                  0, 0, 0, '[]', 0,
                                  'beatmap', $beatmap_id, 'title', 'artist', 'diff',
                                  'mapper', 0, 1.0, -1, 0,
                                  0, 0, '[]', '{}', '{}',
                                  0, 0, 0, 0, $key_count, 0, 1, '[]');
                              """;
            cmd.Parameters.AddWithValue("$score_id", scoreId.ToString());
            cmd.Parameters.AddWithValue("$username", username);
            cmd.Parameters.AddWithValue("$ruleset", EzLocalProfileConstants.MANIA_RULESET_ID);
            cmd.Parameters.AddWithValue("$beatmap_id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$key_count", keyCount);
            cmd.ExecuteNonQuery();

            SqliteConnection.ClearAllPools();
        }
    }
}
