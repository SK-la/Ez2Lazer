// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzSongSelectAnalysisDisplayTest
    {
        [Test]
        public void Empty_has_non_null_empty_kps_list()
        {
            Assert.That(EzSongSelectAnalysisDisplay.Empty.KpsList, Is.Not.Null);
            Assert.That(EzSongSelectAnalysisDisplay.Empty.KpsList, Is.Empty);
            Assert.That(EzSongSelectAnalysisDisplay.Empty.AverageKps, Is.Zero);
            Assert.That(EzSongSelectAnalysisDisplay.Empty.MaxKps, Is.Zero);
        }

        [Test]
        public void Resolve_returns_kps_only()
        {
            var beatmap = createOsuBeatmap(performancePoints: 123.4);
            var analysis = new EzAnalysisResult(new KpsSummary(12.3, 45.6, new[] { 1.0, 2.0 }));

            var metrics = EzSongSelectAnalysisDisplay.Resolve(beatmap, analysis, Array.Empty<Mod>());

            Assert.That(metrics.AverageKps, Is.EqualTo(12.3));
            Assert.That(metrics.MaxKps, Is.EqualTo(45.6));
            Assert.That(metrics.KpsList, Has.Count.EqualTo(2));
        }

        [Test]
        public void ShouldApplyPanelKpsUpdate_allows_empty_kps_when_no_mods()
        {
            var emptyAnalysis = new EzAnalysisResult(new KpsSummary(0, 0, Array.Empty<double>()));

            Assert.That(emptyAnalysis.KpsList, Is.Empty);
            Assert.That(EzSongSelectAnalysisDisplay.ShouldApplyPanelKpsUpdate(emptyAnalysis, Array.Empty<Mod>()), Is.True);
        }

        [Test]
        public void ShouldApplyPanelKpsUpdate_skips_empty_kps_when_mods_active()
        {
            var emptyAnalysis = new EzAnalysisResult(new KpsSummary(0, 0, Array.Empty<double>()));

            Assert.That(EzSongSelectAnalysisDisplay.ShouldApplyPanelKpsUpdate(emptyAnalysis, new Mod[] { new OsuModHardRock() }), Is.False);
        }

        [Test]
        public void Resolve_accepts_a_default_result_before_the_pass_lands()
        {
            // A bindable's initial value is a default struct: no KPS summary, no axes table. The readout must
            // report that as absent rather than throw.
            var beatmap = new BeatmapInfo
            {
                ID = Guid.NewGuid(),
                Hash = Guid.NewGuid().ToString(),
                MD5Hash = Guid.NewGuid().ToString(),
                Ruleset = new ManiaRuleset().RulesetInfo,
                Difficulty = new BeatmapDifficulty { CircleSize = 4 },
                DifficultyName = "test",
                MinaRating = 12.5,
            };

            var metrics = EzSongSelectAnalysisDisplay.Resolve(beatmap, default(EzAnalysisResult), Array.Empty<Mod>());

            Assert.That(metrics.KpsList, Is.Empty);
            Assert.That(metrics.ManiaSummary?.XxySr, Is.EqualTo(12.5), "the stored baseline answers for an unfinished pass");
        }

        [Test]
        public void Default_result_has_no_axes()
        {
            Assert.That(default(EzAnalysisResult).AxesOf(EzChartMetricProducer.Mina.Id()), Is.Empty);
        }

        private static BeatmapInfo createOsuBeatmap(double performancePoints)
        {
            return new BeatmapInfo
            {
                ID = Guid.NewGuid(),
                Hash = Guid.NewGuid().ToString(),
                MD5Hash = Guid.NewGuid().ToString(),
                Ruleset = new OsuRuleset().RulesetInfo,
                DifficultyName = "test",
                PerformancePoints = performancePoints,
            };
        }
    }
}
