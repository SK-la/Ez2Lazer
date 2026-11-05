// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Mina;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzBeatmapMsdComputerTest
    {
        [Test]
        public void TestIsCurrentMinaCacheRequiresHoldRatio()
        {
            var skills = buildCompleteSkills();
            skills.Remove(EzSkillSystems.MinaHoldRatioSkillId);

            Assert.That(EzBeatmapMinaComputer.IsCurrentMinaCache(skills), Is.False);
        }

        [Test]
        public void TestIsCurrentMinaCacheRequiresEveryAxis()
        {
            var skills = buildCompleteSkills();
            skills.Remove(EzMinaSkillAxis.JackSpeed.ToMsdSkillId());

            Assert.That(EzBeatmapMinaComputer.IsCurrentMinaCache(skills), Is.False);
        }

        [Test]
        public void TestIsCurrentMinaCacheAcceptsCompleteCurrentIds()
        {
            Assert.That(EzBeatmapMinaComputer.IsCurrentMinaCache(buildCompleteSkills()), Is.True);
            Assert.That(EzBeatmapMinaComputer.IsSettledMinaCache(buildCompleteSkills()), Is.True);
        }

        [Test]
        public void TestUnrateableMarkerIsSettledButNotCurrent()
        {
            var skills = new Dictionary<string, double>
            {
                [EzSkillSystems.MinaUnrateableSkillId] = 1,
            };

            Assert.That(EzBeatmapMinaComputer.IsUnrateableMina(skills), Is.True);
            Assert.That(EzBeatmapMinaComputer.IsCurrentMinaCache(skills), Is.False);
            Assert.That(EzBeatmapMinaComputer.IsSettledMinaCache(skills), Is.True);
        }

        [Test]
        public void TestEmptySkillsAreNeitherCurrentNorSettled()
        {
            var skills = new Dictionary<string, double>();

            Assert.That(EzBeatmapMinaComputer.IsCurrentMinaCache(skills), Is.False);
            Assert.That(EzBeatmapMinaComputer.IsUnrateableMina(skills), Is.False);
            Assert.That(EzBeatmapMinaComputer.IsSettledMinaCache(skills), Is.False);
        }

        private static Dictionary<string, double> buildCompleteSkills()
        {
            var skills = new Dictionary<string, double>
            {
                [EzSkillSystems.MinaHoldRatioSkillId] = 0.25,
            };

            foreach (var axis in EzMinaSkillAxisExtensions.All)
                skills[axis.ToMsdSkillId()] = 10;

            return skills;
        }
    }
}
