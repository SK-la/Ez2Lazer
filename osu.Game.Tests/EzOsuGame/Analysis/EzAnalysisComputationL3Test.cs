// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// The live (not persisted) pass must produce every selectable system's scalar, because no baseline column
    /// is written while Realm v12 writes are disabled.
    /// </summary>
    [TestFixture]
    public class EzAnalysisComputationL3Test
    {
        [Test]
        public void Mania_pass_emits_mina_and_ppp_axes()
        {
            var beatmap = new TestBeatmap(new ManiaRuleset().RulesetInfo);
            var lookup = new EzAnalysisLookupCache(beatmap.BeatmapInfo, beatmap.BeatmapInfo.Ruleset, mods: null);

            var result = EzAnalysisComputation.Compute(new TestWorkingBeatmap(beatmap), lookup);

            var mina = result.AxesOf(EzAnalysisChannels.MINA);
            var ppp = result.AxesOf(EzAnalysisChannels.PPP);

            Assert.That(mina, Is.Not.Empty);
            Assert.That(ppp, Is.Not.Empty);
            Assert.That(mina[EzMinaSkillAxis.Overall.ToId()], Is.GreaterThan(0));
            Assert.That(ppp[EzAnalysisChannels.STAR_RATING], Is.GreaterThan(0));
        }

        [Test]
        public void Osu_pass_emits_ppp_axes()
        {
            var beatmap = new TestBeatmap(new OsuRuleset().RulesetInfo);
            var lookup = new EzAnalysisLookupCache(beatmap.BeatmapInfo, beatmap.BeatmapInfo.Ruleset, mods: null);

            var result = EzAnalysisComputation.Compute(new TestWorkingBeatmap(beatmap), lookup);

            Assert.That(result.AxesOf(EzAnalysisChannels.PPP), Is.Not.Empty);
            Assert.That(result.AxesOf(EzAnalysisChannels.PPP).ContainsKey(EzAnalysisChannels.STAR_RATING), Is.True);
        }
    }
}
