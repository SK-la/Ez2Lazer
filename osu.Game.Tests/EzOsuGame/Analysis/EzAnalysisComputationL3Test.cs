// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// The live (not persisted) pass must produce every selectable system's scalar, because baseline columns are
    /// only filled in by the backfill passes.
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

            var mina = result.AxesOf(EzAnalysisSystem.Mina);
            var ppp = result.AxesOf(EzAnalysisSystem.PPP);

            Assert.That(mina, Is.Not.Empty);
            Assert.That(ppp, Is.Not.Empty);
            Assert.That(mina[EzMinaSkillAxis.Overall.ToMetaId()], Is.GreaterThan(0));
            Assert.That(result.RatingOf(EzAnalysisSystem.PPP).StarRating, Is.GreaterThan(0));
        }

        [Test]
        public void Mania_pass_mina_matches_the_playable_write_path()
        {
            var beatmap = new TestBeatmap(new ManiaRuleset().RulesetInfo);
            var working = new TestWorkingBeatmap(beatmap);
            var lookup = new EzAnalysisLookupCache(beatmap.BeatmapInfo, beatmap.BeatmapInfo.Ruleset, mods: null);

            var result = EzAnalysisComputation.Compute(working, lookup);
            var playable = working.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods);
            var written = EzAnalysisComputation.ComputeMinaAxes(beatmap.BeatmapInfo.Ruleset.OnlineID, playable, 1);

            Assert.That(written, Is.Not.Null);
            Assert.That(result.AxesOf(EzAnalysisSystem.Mina)[EzMinaSkillAxis.Overall.ToMetaId()],
                Is.EqualTo(written!.Value.Axes[EzMinaSkillAxis.Overall.ToMetaId()]));
        }

        [Test]
        public void Mania_pass_uses_persisted_sunny_star_for_headline()
        {
            var beatmap = new TestBeatmap(new ManiaRuleset().RulesetInfo);
            beatmap.BeatmapInfo.EzRatingData.SunnySR = 9.87;
            var lookup = new EzAnalysisLookupCache(beatmap.BeatmapInfo, beatmap.BeatmapInfo.Ruleset, mods: null);

            var result = EzAnalysisComputation.Compute(new TestWorkingBeatmap(beatmap), lookup);

            Assert.That(result.RatingOf(EzAnalysisSystem.SSR).StarRating, Is.EqualTo(9.87));
            Assert.That(result.ManiaSummary, Is.Not.Null);
        }

        [Test]
        public void Osu_pass_emits_ppp_axes()
        {
            var beatmap = new TestBeatmap(new OsuRuleset().RulesetInfo);
            var lookup = new EzAnalysisLookupCache(beatmap.BeatmapInfo, beatmap.BeatmapInfo.Ruleset, mods: null);

            var result = EzAnalysisComputation.Compute(new TestWorkingBeatmap(beatmap), lookup);

            Assert.That(result.AxesOf(EzAnalysisSystem.PPP), Is.Not.Empty);
            Assert.That(result.AxesOf(EzAnalysisSystem.PPP).ContainsKey(EzPPPSkillCatalog.STAR_RATING), Is.False,
                "a star is a scalar, not an axis");
            Assert.That(result.RatingOf(EzAnalysisSystem.PPP).StarRating, Is.GreaterThan(0));
        }
    }
}
