// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.SSR;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Difficulty;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class TestSunnyStarRatingVersion
    {
        [Test]
        public void Mania_is_the_ruleset_the_ssr_system_rates()
        {
            Assert.That(EzSSRUnit.SupportsRuleset(new ManiaRuleset().RulesetInfo.OnlineID), Is.True);
            Assert.That(EzSSRUnit.VERSION, Is.EqualTo(EzSSREngine.VERSION));
        }

        [Test]
        public void A_ruleset_without_a_column_layout_does_not_declare_ssr()
        {
            Assert.That(EzSSRUnit.SupportsRuleset(-1), Is.False);
        }
    }

    [TestFixture]
    public class TestPerformancePointsVersion
    {
        [Test]
        public void TryGetPerformancePointsVersion_uses_official_difficulty_calculator_version()
        {
            var rulesetInfo = new ManiaRuleset().RulesetInfo;
            rulesetInfo.Available = true;
            var workingBeatmap = new FlatWorkingBeatmap(new Beatmap());

            Assert.That(EzPerformancePointsSupport.TryGetPerformancePointsVersion(rulesetInfo, workingBeatmap, out int ppVersion), Is.True);
            Assert.That(ppVersion, Is.EqualTo(new ManiaDifficultyCalculator(rulesetInfo, workingBeatmap).Version));
            Assert.That(ppVersion, Is.Not.EqualTo(EzSSRUnit.VERSION));
        }
    }
}
