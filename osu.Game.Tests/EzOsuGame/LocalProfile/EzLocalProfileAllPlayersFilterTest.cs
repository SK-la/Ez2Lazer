// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Scoring;

namespace osu.Game.Tests.EzOsuGame.LocalProfile
{
    [TestFixture]
    public class EzLocalProfileAllPlayersFilterTest
    {
        [TestCase(null, true)]
        [TestCase("", true)]
        [TestCase("   ", true)]
        [TestCase(EzLocalProfileConstants.ALL_PLAYERS, true)]
        [TestCase("All", true)]
        [TestCase("alice", false)]
        [TestCase(EzLocalProfileConstants.GUEST_USERNAME, false)]
        public void TestIsAllPlayersFilter(string? username, bool expected)
            => Assert.That(EzLocalProfileConstants.IsAllPlayersFilter(username), Is.EqualTo(expected));

        [Test]
        public void TestLoadDrillScoresAllMatchesUnfiltered()
        {
            using var storage = new TemporaryNativeStorage($"ez-all-drill-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            var result = new EzLocalProfileAggregationResult();
            result.DrillScores.Add(createDrill("alice", 10));
            result.DrillScores.Add(createDrill("bob", 20));
            store.ReplaceAll(result);

            var allNamed = store.LoadDrillScores(EzLocalProfileConstants.MANIA_RULESET_ID, EzLocalProfileConstants.ALL_PLAYERS);
            var unfiltered = store.LoadDrillScores(EzLocalProfileConstants.MANIA_RULESET_ID, null);
            var aliceOnly = store.LoadDrillScores(EzLocalProfileConstants.MANIA_RULESET_ID, "alice");

            Assert.That(allNamed, Has.Count.EqualTo(2));
            Assert.That(unfiltered, Has.Count.EqualTo(2));
            Assert.That(aliceOnly, Has.Count.EqualTo(1));
            Assert.That(allNamed.Select(r => r.Username).OrderBy(n => n), Is.EqualTo(new[] { "alice", "bob" }));
        }

        [Test]
        public void TestGetDanClearsAllUnionsRealPlayersAndSkipsAllSentinel()
        {
            using var storage = new TemporaryNativeStorage($"ez-all-dan-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            // Touch schema via empty replace-all.
            store.ReplaceAll(new EzLocalProfileAggregationResult());

            store.ReplaceDanClears("alice", new[]
            {
                createDanClear("alice", "hash-a", 10),
            });
            store.ReplaceDanClears("bob", new[]
            {
                createDanClear("bob", "hash-b", 11),
            });
            // Residual All evidence (legacy materialisation) must not appear in All reads.
            store.ReplaceDanClears(EzLocalProfileConstants.ALL_PLAYERS, new[]
            {
                createDanClear(EzLocalProfileConstants.ALL_PLAYERS, "hash-legacy", 99),
            });

            var all = store.GetDanClears(EzLocalProfileConstants.ALL_PLAYERS);
            Assert.That(all, Has.Count.EqualTo(2));
            Assert.That(all.Select(r => r.Username).OrderBy(n => n), Is.EqualTo(new[] { "alice", "bob" }));

            // Clearing All sentinel leaves only real-player rows.
            store.ReplaceDanClears(EzLocalProfileConstants.ALL_PLAYERS, Array.Empty<EzDanClearEvidenceRow>());
            Assert.That(store.GetDanClears(EzLocalProfileConstants.ALL_PLAYERS), Has.Count.EqualTo(2));
        }

        [Test]
        public void TestGetAxisPlaysAllUnionsRealPlayersAndSkipsAllSentinel()
        {
            using var storage = new TemporaryNativeStorage($"ez-all-axis-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            store.ReplaceAll(new EzLocalProfileAggregationResult());

            store.ReplaceAxisPlays("alice", new[]
            {
                createAxisPlay("alice", "player_ssr.jack", 5),
            });
            store.ReplaceAxisPlays("bob", new[]
            {
                createAxisPlay("bob", "player_ssr.stream", 6),
            });
            store.ReplaceAxisPlays(EzLocalProfileConstants.ALL_PLAYERS, new[]
            {
                createAxisPlay(EzLocalProfileConstants.ALL_PLAYERS, "player_ssr.jack", 99),
            });

            var all = store.GetAxisPlays(EzLocalProfileConstants.ALL_PLAYERS);
            Assert.That(all, Has.Count.EqualTo(2));
            Assert.That(all.All(r => r.Username != EzLocalProfileConstants.ALL_PLAYERS), Is.True);

            store.ReplaceAxisPlays(EzLocalProfileConstants.ALL_PLAYERS, Array.Empty<EzAxisPlayEvidenceRow>());
            Assert.That(store.GetAxisPlays(EzLocalProfileConstants.ALL_PLAYERS), Has.Count.EqualTo(2));
        }

        private static EzLocalProfileDrillScoreRow createDrill(string username, double pp)
            => new EzLocalProfileDrillScoreRow
            {
                ScoreId = Guid.NewGuid(),
                Username = username,
                RulesetId = EzLocalProfileConstants.MANIA_RULESET_ID,
                Rank = ScoreRank.S,
                PpResolved = pp,
                Accuracy = 0.98,
                MaxCombo = 100,
                MaxAchievableCombo = 100,
                BeatmapHash = "hash",
                BeatmapId = Guid.NewGuid(),
                Title = "Song",
                Artist = "Artist",
                DifficultyName = "Insane",
                MapperUsername = "Mapper",
                Date = DateTimeOffset.UtcNow,
            };

        private static EzDanClearEvidenceRow createDanClear(string username, string hash, double dan)
            => new EzDanClearEvidenceRow
            {
                Username = username,
                KeyCount = 4,
                Side = "rc",
                BeatmapHash = hash,
                Rate = 1,
                CreditedDan = dan,
                Accuracy = 0.96,
                ScoredAt = DateTimeOffset.UtcNow,
                AlgorithmVersion = EzDanAlgorithm.VERSION,
            };

        private static EzAxisPlayEvidenceRow createAxisPlay(string username, string skillId, double value)
            => new EzAxisPlayEvidenceRow
            {
                Username = username,
                KeyCount = 4,
                SkillId = skillId,
                BeatmapHash = "hash",
                AxisValue = value,
                Accuracy = 0.96,
                Rate = 1,
                ScoredAt = DateTimeOffset.UtcNow,
                AlgorithmVersion = EzManiaSkillAlgorithm.VERSION,
            };
    }
}
