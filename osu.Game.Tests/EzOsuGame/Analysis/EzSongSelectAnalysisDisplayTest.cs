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
        public void Resolve_uses_realm_pp_for_non_mania_when_dynamic_has_no_kps()
        {
            var beatmap = createOsuBeatmap(performancePoints: 123.4);
            var emptyAnalysis = new EzAnalysisResult(new KpsSummary(0, 0, Array.Empty<double>()));

            var metrics = EzSongSelectAnalysisDisplay.Resolve(beatmap, emptyAnalysis, Array.Empty<Mod>());

            Assert.That(metrics.PerformancePoints, Is.EqualTo(123.4));
            Assert.That(metrics.AverageKps, Is.EqualTo(0));
            Assert.That(metrics.KpsList, Is.Empty);
        }

        [Test]
        public void ShouldApplyPanelUpdate_allows_empty_kps_when_no_mods()
        {
            var emptyAnalysis = new EzAnalysisResult(new KpsSummary(0, 0, Array.Empty<double>()));

            Assert.That(EzSongSelectAnalysisDisplay.HasDisplayableKps(emptyAnalysis), Is.False);
            Assert.That(EzSongSelectAnalysisDisplay.ShouldApplyPanelUpdate(emptyAnalysis, Array.Empty<Mod>()), Is.True);
        }

        [Test]
        public void ShouldApplyPanelUpdate_skips_empty_kps_when_mods_active()
        {
            var emptyAnalysis = new EzAnalysisResult(new KpsSummary(0, 0, Array.Empty<double>()));

            Assert.That(EzSongSelectAnalysisDisplay.ShouldApplyPanelUpdate(emptyAnalysis, new Mod[] { new OsuModHardRock() }), Is.False);
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
