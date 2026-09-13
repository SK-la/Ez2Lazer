// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Scoring;

namespace osu.Game.Tests.EzOsuGame.LocalProfile
{
    /// <summary>
    /// "Delete player data" is an exclusion, not a delete: the player's own data stays readable and is cheap to
    /// bring back, but it stops contributing to the archive-wide totals and to <c>All</c>.
    /// </summary>
    [TestFixture]
    public class EzLocalProfileExcludeUsernameTest
    {
        [Test]
        public void Exclude_fences_only_the_target_player()
        {
            using var storage = new TemporaryNativeStorage($"ez-exclude-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            seed(store, aggregation("alpha", 0, pp: 100, drills: 3), aggregation("beta", 0, pp: 50, drills: 2));

            Assert.That(store.LoadIncludedUsernames(), Is.EquivalentTo(new[] { "alpha", "beta" }));
            Assert.That(store.LoadDrillScores(0), Has.Count.EqualTo(5));

            bool excluded = store.ExcludeUsernames("alpha", Array.Empty<EzLocalProfileOnlineScoreContribution>(), new HashSet<long>());

            Assert.That(excluded, Is.True);
            Assert.That(store.LoadIncludedUsernames(), Is.EquivalentTo(new[] { "beta" }));

            // Nothing is deleted: the fenced player's drill rows and slice survive so re-including is cheap.
            Assert.That(store.LoadDrillScores(0), Has.Count.EqualTo(5));
            Assert.That(store.TryLoadPartitionPayload("alpha"), Is.Not.Null);
            Assert.That(store.TryLoadPartitionPayload("beta"), Is.Not.Null);
        }

        [Test]
        public void Exclude_rebuilds_archive_totals_from_remaining_partitions()
        {
            using var storage = new TemporaryNativeStorage($"ez-exclude-archive-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            seed(store, aggregation("alpha", 0, pp: 100, drills: 1), aggregation("beta", 0, pp: 40, drills: 1));

            Assert.That(store.LoadSnapshot().RulesetStats.Single(s => s.RulesetId == 0).ScoreCount, Is.EqualTo(2));

            store.ExcludeUsernames("alpha", Array.Empty<EzLocalProfileOnlineScoreContribution>(), new HashSet<long>());

            var after = store.LoadSnapshot().RulesetStats.Single(s => s.RulesetId == 0);

            Assert.That(after.ScoreCount, Is.EqualTo(1));
            Assert.That(after.TotalPp, Is.EqualTo(40).Within(0.001));
        }

        [Test]
        public void Exclude_keeps_other_players_evidence_rows()
        {
            using var storage = new TemporaryNativeStorage($"ez-exclude-evidence-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            seed(store, aggregation("alpha", 0, pp: 100, drills: 1), aggregation("beta", 0, pp: 40, drills: 1));

            store.ReplaceDanClears("alpha", new[] { danClear("alpha", 12) });
            store.ReplaceDanClears("beta", new[] { danClear("beta", 7) });
            store.ReplaceAxisPlays("beta", new[] { axisPlay("beta", 3.5) });

            Assert.That(store.GetDanClears("beta"), Has.Count.EqualTo(1));

            // An exclusion must not take the shared full-clear path: that would wipe beta's evidence too,
            // and evidence is only rewritten by a full skills pass (which an exclusion does not run).
            store.ExcludeUsernames("alpha", Array.Empty<EzLocalProfileOnlineScoreContribution>(), new HashSet<long>());

            Assert.That(store.GetDanClears("beta"), Has.Count.EqualTo(1));
            Assert.That(store.GetAxisPlays("beta"), Has.Count.EqualTo(1));

            // The fenced player's own evidence is kept too — exclusion is not a delete.
            Assert.That(store.GetDanClears("alpha"), Has.Count.EqualTo(1));
        }

        [Test]
        public void Exclude_does_not_stamp_content_version()
        {
            using var storage = new TemporaryNativeStorage($"ez-exclude-content-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            // Partitions written directly (no archive rebuild) leave content_version unset, i.e. stale.
            store.SavePartitionPayload("alpha", EzLocalProfilePartitionPayload.FromAggregation(aggregation("alpha", 0, pp: 100, drills: 1)));
            store.SavePartitionPayload("beta", EzLocalProfilePartitionPayload.FromAggregation(aggregation("beta", 0, pp: 40, drills: 1)));

            Assert.That(store.NeedsRecompute(), Is.True);

            store.ExcludeUsernames("alpha", Array.Empty<EzLocalProfileOnlineScoreContribution>(), new HashSet<long>());

            // beta's slice was not re-analysed, so the archive must not claim to be current: a later
            // incremental compute has to trust only the slices it actually rebuilt.
            Assert.That(store.NeedsRecompute(), Is.True);
            Assert.That(store.LoadIncludedUsernames(), Is.EquivalentTo(new[] { "beta" }));
        }

        [Test]
        public void Exclude_unknown_player_is_a_noop()
        {
            using var storage = new TemporaryNativeStorage($"ez-exclude-noop-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            seed(store, aggregation("beta", 0, pp: 40, drills: 1));

            Assert.That(store.ExcludeUsernames("ghost", Array.Empty<EzLocalProfileOnlineScoreContribution>(), new HashSet<long>()), Is.False);
            Assert.That(store.LoadIncludedUsernames(), Is.EquivalentTo(new[] { "beta" }));
        }

        [Test]
        public void Exclude_is_idempotent()
        {
            using var storage = new TemporaryNativeStorage($"ez-exclude-idempotent-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            seed(store, aggregation("alpha", 0, pp: 100, drills: 1), aggregation("beta", 0, pp: 40, drills: 1));

            Assert.That(store.ExcludeUsernames("alpha", Array.Empty<EzLocalProfileOnlineScoreContribution>(), new HashSet<long>()), Is.True);
            Assert.That(store.ExcludeUsernames("alpha", Array.Empty<EzLocalProfileOnlineScoreContribution>(), new HashSet<long>()), Is.False);
            Assert.That(store.LoadIncludedUsernames(), Is.EquivalentTo(new[] { "beta" }));
            Assert.That(store.LoadSnapshot().RulesetStats.Single(s => s.RulesetId == 0).ScoreCount, Is.EqualTo(1));
        }

        [Test]
        public void Exclude_drops_the_players_attributed_online_contributions()
        {
            using var storage = new TemporaryNativeStorage($"ez-exclude-online-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            seed(store, aggregation("alpha", 0, pp: 100, drills: 1), aggregation("beta", 0, pp: 40, drills: 1));

            var online = new List<EzLocalProfileOnlineScoreContribution>
            {
                new EzLocalProfileOnlineScoreContribution(1, 0, ScoreRank.S, 5, 4, 9, 500, 30, 60000, "alpha"),
                new EzLocalProfileOnlineScoreContribution(2, 0, ScoreRank.S, 5, 4, 9, 500, 10, 60000, "beta"),
                // Legacy row from before contributions carried an owner: still counted until the next pull attributes it.
                new EzLocalProfileOnlineScoreContribution(3, 0, ScoreRank.S, 5, 4, 9, 500, 5, 60000),
            };

            store.ApplyUsernamePartitions(
                new Dictionary<string, EzLocalProfileAggregationResult>(StringComparer.Ordinal),
                replaceOtherUsernames: false,
                online,
                new HashSet<long>());

            // 2 partitioned plays + alpha's, beta's and the unattributed contribution.
            Assert.That(store.LoadSnapshot().RulesetStats.Single(s => s.RulesetId == 0).ScoreCount, Is.EqualTo(5));

            store.ExcludeUsernames("alpha", online, new HashSet<long>());

            // beta's play kept + beta's contribution + the still-unattributed row.
            Assert.That(store.LoadSnapshot().RulesetStats.Single(s => s.RulesetId == 0).ScoreCount, Is.EqualTo(3));
        }

        [Test]
        public void Append_scores_is_idempotent_and_adds_to_the_archive()
        {
            using var storage = new TemporaryNativeStorage($"ez-append-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            seed(store, aggregation("alpha", 0, pp: 100, drills: 1));

            var delta = aggregation("alpha", 0, pp: 20, drills: 1);

            Assert.That(store.AppendScores("alpha", delta, Array.Empty<EzLocalProfileOnlineScoreContribution>(), new HashSet<long>()), Is.True);

            var afterFirst = store.LoadSnapshot().RulesetStats.Single(s => s.RulesetId == 0);
            Assert.That(afterFirst.ScoreCount, Is.EqualTo(2));
            Assert.That(afterFirst.TotalPp, Is.EqualTo(120).Within(0.001));

            // The same drill row must not be counted twice (drill_scores is the ledger).
            Assert.That(store.AppendScores("alpha", delta, Array.Empty<EzLocalProfileOnlineScoreContribution>(), new HashSet<long>()), Is.False);

            var afterSecond = store.LoadSnapshot().RulesetStats.Single(s => s.RulesetId == 0);
            Assert.That(afterSecond.ScoreCount, Is.EqualTo(2));
            Assert.That(store.LoadDrillScores(0), Has.Count.EqualTo(2));
        }

        [Test]
        public void Append_does_not_resurrect_an_excluded_player()
        {
            using var storage = new TemporaryNativeStorage($"ez-append-excluded-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            seed(store, aggregation("alpha", 0, pp: 100, drills: 1), aggregation("beta", 0, pp: 40, drills: 1));

            store.ExcludeUsernames("alpha", Array.Empty<EzLocalProfileOnlineScoreContribution>(), new HashSet<long>());

            // A play settling for a fenced-out player must not silently pull them back into the archive totals.
            var delta = aggregation("alpha", 0, pp: 20, drills: 1);

            Assert.That(store.AppendScores("alpha", delta, Array.Empty<EzLocalProfileOnlineScoreContribution>(), new HashSet<long>()), Is.False);

            Assert.That(store.LoadIncludedUsernames(), Is.EquivalentTo(new[] { "beta" }));
            Assert.That(store.LoadSnapshot().RulesetStats.Single(s => s.RulesetId == 0).ScoreCount, Is.EqualTo(1));

            // Nor may the ledger claim a score the slice never counted, or the next diff would skip it forever.
            Assert.That(store.ContainsDrillScore(delta.DrillScores[0].ScoreId), Is.False);
            Assert.That(store.LoadDrillScores(0), Has.Count.EqualTo(2));
        }

        [Test]
        public void Append_keeps_the_archive_content_version_unset()
        {
            using var storage = new TemporaryNativeStorage($"ez-append-content-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            // Partitions written directly (no archive rebuild) leave content_version unset, i.e. stale.
            var initial = aggregation("alpha", 0, pp: 100, drills: 1);
            store.SavePartitionPayload("alpha", EzLocalProfilePartitionPayload.FromAggregation(initial));
            store.AppendDrills(initial.DrillScores);

            Assert.That(store.NeedsRecompute(), Is.True);

            // A single-play append must not claim the whole archive is at the current logic version: other
            // players' slices may still be behind, and only a compute over them may stamp it current.
            Assert.That(store.AppendScores("alpha", aggregation("alpha", 0, pp: 20, drills: 1),
                Array.Empty<EzLocalProfileOnlineScoreContribution>(), new HashSet<long>()), Is.True);

            Assert.That(store.NeedsRecompute(), Is.True);
        }

        [Test]
        public void Remove_online_contribution_drops_only_that_row()
        {
            using var storage = new TemporaryNativeStorage($"ez-online-remove-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            store.UpsertOnlineScoreContribution(new EzLocalProfileOnlineScoreContribution(11, 0, ScoreRank.S, 5, 4, 9, 500, 30, 60000, "alpha"));
            store.UpsertOnlineScoreContribution(new EzLocalProfileOnlineScoreContribution(12, 0, ScoreRank.S, 5, 4, 9, 500, 30, 60000, "alpha"));

            Assert.That(store.RemoveOnlineScoreContribution(11), Is.True);
            Assert.That(store.RemoveOnlineScoreContribution(11), Is.False);
            Assert.That(store.LoadOnlineScoreContributions().Select(c => c.OnlineId), Is.EquivalentTo(new long[] { 12 }));
        }

        /// <summary>Write slices the way the compute path does: partition + archive rebuild, then the drill rows.</summary>
        private static void seed(EzLocalProfileStore store, params EzLocalProfileAggregationResult[] results)        {
            store.ApplyUsernamePartitions(
                results.ToDictionary(r => r.IncludedUsernames.Single(), r => r, StringComparer.Ordinal),
                replaceOtherUsernames: true,
                Array.Empty<EzLocalProfileOnlineScoreContribution>(),
                new HashSet<long>());

            foreach (var result in results)
                store.AppendDrills(result.DrillScores);
        }

        private static EzLocalProfileAggregationResult aggregation(string username, int rulesetId, double pp, int drills)
        {
            var result = new EzLocalProfileAggregationResult
            {
                IncludedUsernames = new[] { username },
                RulesetStats =
                {
                    [rulesetId] = new EzLocalProfileAggregationResult.MutableRulesetStats
                    {
                        ScoreCount = drills,
                        TotalPp = pp * drills,
                        KpsSum = 6 * drills,
                        KpsSampleCount = drills,
                        MaxKps = 6,
                        TotalKeys = 1000 * drills,
                    }
                }
            };

            for (int i = 0; i < drills; i++)
                result.DrillScores.Add(drillRow(username, rulesetId, pp));

            return result;
        }

        private static EzLocalProfileDrillScoreRow drillRow(string username, int rulesetId, double pp) => new EzLocalProfileDrillScoreRow
        {
            ScoreId = Guid.NewGuid(),
            Username = username,
            RulesetId = rulesetId,
            Rank = ScoreRank.S,
            PpResolved = pp,
            Accuracy = 0.98,
            MaxCombo = 100,
            MaxAchievableCombo = 100,
            BeatmapHash = $"hash-{username}",
            BeatmapId = Guid.NewGuid(),
            Title = "Title",
            Artist = "Artist",
            DifficultyName = "Insane",
            MapperUsername = "Mapper",
            BeatmapStatus = BeatmapOnlineStatus.Ranked,
            StarRating = 5,
            Date = DateTimeOffset.UtcNow,
        };

        private static EzDanClearEvidenceRow danClear(string username, double creditedDan) => new EzDanClearEvidenceRow
        {
            Username = username,
            KeyCount = 4,
            Side = DanSkillSystem.SIDE_RC,
            BeatmapHash = $"hash-{username}",
            CreditedDan = creditedDan,
            Accuracy = 0.98,
            ScoredAt = DateTimeOffset.UtcNow,
        };

        private static EzAxisPlayEvidenceRow axisPlay(string username, double axisValue) => new EzAxisPlayEvidenceRow
        {
            Username = username,
            KeyCount = 4,
            SkillId = "stream",
            BeatmapHash = $"hash-{username}",
            AxisValue = axisValue,
            Accuracy = 0.98,
            ScoredAt = DateTimeOffset.UtcNow,
        };
    }
}
