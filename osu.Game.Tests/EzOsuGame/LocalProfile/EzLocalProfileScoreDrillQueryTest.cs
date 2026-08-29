// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.EzOsuGame.LocalProfile
{
    [TestFixture]
    public class EzLocalProfileScoreDrillQueryTest
    {
        [Test]
        public void TestFilterMatchesTitle()
        {
            var scores = new List<EzLocalProfileDrillScoreRow>
            {
                createRow(rulesetId: 0, pp: 120, title: "Unique Drill Title"),
                createRow(rulesetId: 0, pp: 80, title: "Other Title"),
            };

            var filtered = EzLocalProfileScoreDrillQuery.Filter(scores, "Unique Drill");

            Assert.That(filtered, Has.Count.EqualTo(1));
            Assert.That(filtered[0].PpResolved, Is.EqualTo(120));
        }

        [Test]
        public void TestFilterEmptySearchReturnsAll()
        {
            var scores = new List<EzLocalProfileDrillScoreRow>
            {
                createRow(rulesetId: 0, pp: 100, title: "A"),
                createRow(rulesetId: 0, pp: 90, title: "B"),
            };

            Assert.That(EzLocalProfileScoreDrillQuery.Filter(scores, string.Empty), Has.Count.EqualTo(2));
            Assert.That(EzLocalProfileScoreDrillQuery.Filter(scores, "   "), Has.Count.EqualTo(2));
        }

        [Test]
        public void TestPeersOnSameBeatmapMatchesHashAndVersion()
        {
            var target = createRow(rulesetId: 0, pp: 100, title: "Song", beatmapHash: "hash-a", difficultyName: "Insane");
            var same = createRow(rulesetId: 0, pp: 90, title: "Song", beatmapHash: "hash-a", difficultyName: "Insane");
            var otherVersion = createRow(rulesetId: 0, pp: 80, title: "Song", beatmapHash: "hash-a", difficultyName: "Hard");

            var peers = EzLocalProfileScoreDrillQuery.PeersOnSameBeatmap(target, new[] { target, same, otherVersion });

            Assert.That(peers, Has.Count.EqualTo(2));
        }

        [Test]
        public void TestLoadDrillScoresFiltersByRuleset()
        {
            using var storage = new TemporaryNativeStorage($"ez-drill-{Guid.NewGuid():N}");
            using var store = new EzLocalProfileStore(storage);

            var result = new EzLocalProfileAggregationResult();
            result.DrillScores.Add(createRow(rulesetId: 0, pp: 100, title: "Osu 1"));
            result.DrillScores.Add(createRow(rulesetId: 0, pp: 90, title: "Osu 2"));
            result.DrillScores.Add(createRow(rulesetId: 3, pp: 50, title: "Mania"));
            store.ReplaceAll(result);

            var osuScores = store.LoadDrillScores(0);
            Assert.That(osuScores, Has.Count.EqualTo(2));
            Assert.That(osuScores.All(s => s.RulesetId == 0), Is.True);

            var maniaScores = store.LoadDrillScores(3);
            Assert.That(maniaScores, Has.Count.EqualTo(1));
            Assert.That(maniaScores[0].RulesetId, Is.EqualTo(3));
        }

        private static EzLocalProfileDrillScoreRow createRow(
            int rulesetId,
            double pp,
            string title,
            string beatmapHash = "test-hash",
            string difficultyName = "Insane")
        {
            return new EzLocalProfileDrillScoreRow
            {
                ScoreId = Guid.NewGuid(),
                Username = "tester",
                RulesetId = rulesetId,
                Rank = ScoreRank.S,
                PpResolved = pp,
                Accuracy = 0.98,
                MaxCombo = 100,
                MaxAchievableCombo = 100,
                BeatmapHash = beatmapHash,
                BeatmapId = Guid.NewGuid(),
                Title = title,
                Artist = "Artist",
                DifficultyName = difficultyName,
                MapperUsername = "Mapper",
                BeatmapStatus = BeatmapOnlineStatus.Ranked,
                StarRating = 5,
                Date = DateTimeOffset.UtcNow,
            };
        }
    }

    [TestFixture]
    public class EzLocalProfileRealmLinqGuardTest : RealmTest
    {
        [Test]
        public void TestRealmRejectsNestedRulesetOnlineIdLinq()
        {
            RunTestWithRealm((realm, storage) =>
            {
                using var _ = new RealmRulesetStore(realm, storage);

                Assert.Throws<NotSupportedException>(() =>
                {
                    realm.Run(r => r.All<ScoreInfo>().Where(s => s.Ruleset.OnlineID == 0).ToList());
                });
            });
        }
    }
}
