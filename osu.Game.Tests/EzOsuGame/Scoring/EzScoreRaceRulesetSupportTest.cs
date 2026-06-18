// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzScoreRaceRulesetSupportTest
    {
        [Test]
        public void TestManiaSupportsGhostRace()
        {
            var ruleset = new ManiaRuleset().RulesetInfo;
            Assert.That(EzScoreRaceRulesetSupport.SupportsGhostRace(ruleset), Is.True);
            Assert.That(EzScoreRaceRulesetSupport.GetGhostTimelineMode(ruleset), Is.EqualTo(EzScoreRaceGhostTimelineMode.ManiaSession));
        }

        [Test]
        public void TestOsuSupportsGhostRace()
        {
            var ruleset = new OsuRuleset().RulesetInfo;
            Assert.That(EzScoreRaceRulesetSupport.SupportsGhostRace(ruleset), Is.True);
            Assert.That(EzScoreRaceRulesetSupport.GetGhostTimelineMode(ruleset), Is.EqualTo(EzScoreRaceGhostTimelineMode.HitEvents));
        }

        [Test]
        public void TestTaikoDoesNotSupportGhostRace()
        {
            var ruleset = new TaikoRuleset().RulesetInfo;
            Assert.That(EzScoreRaceRulesetSupport.SupportsGhostRace(ruleset), Is.False);
            Assert.That(EzScoreRaceRulesetSupport.GetGhostTimelineMode(ruleset), Is.EqualTo(EzScoreRaceGhostTimelineMode.None));
        }

        [Test]
        public void TestCatchDoesNotSupportGhostRace()
        {
            var ruleset = new CatchRuleset().RulesetInfo;
            Assert.That(EzScoreRaceRulesetSupport.SupportsGhostRace(ruleset), Is.False);
            Assert.That(EzScoreRaceRulesetSupport.GetGhostTimelineMode(ruleset), Is.EqualTo(EzScoreRaceGhostTimelineMode.None));
        }
    }
}
