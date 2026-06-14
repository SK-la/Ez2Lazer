// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzSongSelectAnalysisDisplayTest
    {
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

            Assert.That(EzSongSelectAnalysisDisplay.HasDisplayableKps(emptyAnalysis), Is.False);
            Assert.That(EzSongSelectAnalysisDisplay.ShouldApplyPanelKpsUpdate(emptyAnalysis, Array.Empty<Mod>()), Is.True);
        }

        [Test]
        public void ShouldApplyPanelKpsUpdate_skips_empty_kps_when_mods_active()
        {
            var emptyAnalysis = new EzAnalysisResult(new KpsSummary(0, 0, Array.Empty<double>()));

            Assert.That(EzSongSelectAnalysisDisplay.ShouldApplyPanelKpsUpdate(emptyAnalysis, new Mod[] { new OsuModHardRock() }), Is.False);
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
