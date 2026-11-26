// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzLocalScoreQueriesModMatchTest
    {
        [Test]
        public void TestEmptySelectionMatchesNoModScoresOnly()
        {
            Assert.That(EzLocalScoreQueries.MatchesExactMods(Array.Empty<Mod>(), Array.Empty<Mod>()), Is.True);
            Assert.That(EzLocalScoreQueries.MatchesExactMods(new Mod[] { new OsuModHardRock() }, Array.Empty<Mod>()), Is.False);
        }

        [Test]
        public void TestSelectionRequiresExactSet()
        {
            var hardRock = new Mod[] { new OsuModHardRock() };

            Assert.That(EzLocalScoreQueries.MatchesExactMods(new Mod[] { new OsuModHardRock() }, hardRock), Is.True);
            Assert.That(EzLocalScoreQueries.MatchesExactMods(Array.Empty<Mod>(), hardRock), Is.False);
            Assert.That(EzLocalScoreQueries.MatchesExactMods(new Mod[] { new OsuModDoubleTime() }, hardRock), Is.False);
        }

        [Test]
        public void TestExtraScoreModDisqualifies()
        {
            var selected = new Mod[] { new OsuModHardRock() };

            Assert.That(EzLocalScoreQueries.MatchesExactMods(new Mod[] { new OsuModHardRock(), new OsuModDoubleTime() }, selected), Is.False);
        }

        [Test]
        public void TestOrderIndependent()
        {
            var selected = new Mod[] { new OsuModDoubleTime(), new OsuModHardRock() };

            Assert.That(EzLocalScoreQueries.MatchesExactMods(new Mod[] { new OsuModHardRock(), new OsuModDoubleTime() }, selected), Is.True);
        }
    }
}
