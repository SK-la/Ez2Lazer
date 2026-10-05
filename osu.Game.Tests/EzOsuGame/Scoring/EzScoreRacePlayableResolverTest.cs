// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzScoreRacePlayableResolverTest
    {
        [SetUp]
        [TearDown]
        public void ResetCache() => EzPlayableBeatmapCache.Reset();

        [Test]
        public void SameModsReturnSameBoundInstance()
        {
            var working = new TestWorkingBeatmap(new TestBeatmap(new OsuRuleset().RulesetInfo));
            var ruleset = working.BeatmapInfo.Ruleset;

            var first = EzScoreRacePlayableResolver.GetSessionReady(working, ruleset);
            var second = EzScoreRacePlayableResolver.GetSessionReady(working, ruleset);

            Assert.That(second, Is.SameAs(first));
            Assert.That(EzPlayableBeatmapCache.IsSharedInstance(first), Is.False);
        }

        [Test]
        public void DifferentModsReturnDifferentInstances()
        {
            var rulesetInstance = new OsuRuleset();
            var working = new TestWorkingBeatmap(new TestBeatmap(rulesetInstance.RulesetInfo));
            var hr = rulesetInstance.CreateMod<OsuModHardRock>()!;

            var nm = EzScoreRacePlayableResolver.GetSessionReady(working, rulesetInstance.RulesetInfo);
            var hardRock = EzScoreRacePlayableResolver.GetSessionReady(working, rulesetInstance.RulesetInfo, new[] { hr });

            Assert.That(hardRock, Is.Not.SameAs(nm));
            Assert.That(EzPlayableBeatmapCache.IsSharedInstance(hardRock), Is.False);
        }
    }
}
