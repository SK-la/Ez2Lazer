// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzScoreRacePlayableGroupingTest
    {
        [Test]
        public void NomodAndHardRockGhostsDoNotSharePlayableGroup()
        {
            var ruleset = new OsuRuleset();
            var nm = scoreWithMods();
            var hr = scoreWithMods(ruleset.CreateMod<OsuModHardRock>()!);

            var groups = EzScoreRacePlayableGrouping.GroupIndicesByPlayableMods(
                new[] { nm, hr },
                _ => true);

            Assert.That(groups.Count, Is.EqualTo(2));
            Assert.That(groups.Values.SelectMany(g => g).OrderBy(i => i), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void SameGameplayModsShareOneGroupEvenWithDifferentScoreIds()
        {
            var ruleset = new OsuRuleset();
            var hr = ruleset.CreateMod<OsuModHardRock>()!;
            var a = scoreWithMods(hr);
            var b = scoreWithMods(hr);

            var groups = EzScoreRacePlayableGrouping.GroupIndicesByPlayableMods(
                new[] { a, b },
                _ => true);

            Assert.That(groups.Count, Is.EqualTo(1));
            Assert.That(groups.Values.Single(), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void SkipsIndicesThatAlreadyHaveTimeline()
        {
            var scores = new[] { scoreWithMods(), scoreWithMods() };
            var groups = EzScoreRacePlayableGrouping.GroupIndicesByPlayableMods(
                scores,
                i => i == 1);

            Assert.That(groups.Count, Is.EqualTo(1));
            Assert.That(groups.Values.Single(), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void EmptyModsKeyIsStableEmptyString()
        {
            Assert.That(EzScoreRacePlayableGrouping.GetPlayableModKey(Array.Empty<osu.Game.Rulesets.Mods.Mod>()), Is.EqualTo(string.Empty));
        }

        private static ScoreInfo scoreWithMods(params osu.Game.Rulesets.Mods.Mod[] mods) => new ScoreInfo
        {
            ID = Guid.NewGuid(),
            Mods = mods,
        };
    }
}
