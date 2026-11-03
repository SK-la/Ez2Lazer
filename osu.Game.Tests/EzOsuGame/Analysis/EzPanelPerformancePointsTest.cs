// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzPanelPerformancePointsTest
    {
        [Test]
        public void ResolvePanelPp_prefers_l2_performance_attributes()
        {
            var beatmap = createBeatmap(performancePoints: 100);
            var star = new StarDifficulty(new OsuDifficultyAttributes { StarRating = 5 }, new PerformanceAttributes { Total = 200 });

            Assert.That(EzPanelPerformancePoints.ResolvePanelPp(star, beatmap), Is.EqualTo(200));
        }

        [Test]
        public void ResolvePanelPp_falls_back_to_l1_realm_when_attributes_missing()
        {
            var beatmap = createBeatmap(performancePoints: 123.4);
            var star = new StarDifficulty(beatmap.StarRating, 0);

            Assert.That(EzPanelPerformancePoints.ResolvePanelPp(star, beatmap), Is.EqualTo(123.4));
        }

        [Test]
        public void ResolvePanelPp_returns_null_when_l1_and_l2_unavailable()
        {
            var beatmap = createBeatmap(performancePoints: -1);
            var star = new StarDifficulty(beatmap.StarRating, 0);

            Assert.That(EzPanelPerformancePoints.ResolvePanelPp(star, beatmap), Is.Null);
        }

        [Test]
        public void ResolvePanelPp_ignores_non_finite_l2_total()
        {
            var beatmap = createBeatmap(performancePoints: 50);
            var star = new StarDifficulty(new OsuDifficultyAttributes { StarRating = 5 }, new PerformanceAttributes { Total = double.NaN });

            Assert.That(EzPanelPerformancePoints.ResolvePanelPp(star, beatmap), Is.EqualTo(50));
        }

        [Test]
        public void ResolveRealmBaselinePp_returns_null_for_pending_sentinel()
        {
            var beatmap = createBeatmap(performancePoints: -1);

            Assert.That(EzPanelPerformancePoints.ResolveRealmBaselinePp(beatmap), Is.Null);
        }

        private static BeatmapInfo createBeatmap(double performancePoints)
        {
            return new BeatmapInfo
            {
                ID = Guid.NewGuid(),
                Hash = Guid.NewGuid().ToString(),
                MD5Hash = Guid.NewGuid().ToString(),
                StarRating = 4.5,
                PerformancePoints = performancePoints,
                Ruleset = new OsuRuleset().RulesetInfo,
                DifficultyName = "test",
            };
        }
    }
}
