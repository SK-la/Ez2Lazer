// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzBeatmapMsdComputerTest
    {
        [Test]
        public void TestIsCurrentMsdCacheRequiresHoldRatio()
        {
            var skills = buildCompleteSkills();
            skills.Remove(EzSkillSystems.MsdHoldRatioSkillId);

            Assert.That(EzBeatmapMsdComputer.IsCurrentMsdCache(skills), Is.False);
        }

        [Test]
        public void TestIsCurrentMsdCacheRequiresEveryAxis()
        {
            var skills = buildCompleteSkills();
            skills.Remove(EzMinaSkillAxis.JackSpeed.ToMsdSkillId());

            Assert.That(EzBeatmapMsdComputer.IsCurrentMsdCache(skills), Is.False);
        }

        [Test]
        public void TestIsCurrentMsdCacheAcceptsCompleteCurrentIds()
        {
            Assert.That(EzBeatmapMsdComputer.IsCurrentMsdCache(buildCompleteSkills()), Is.True);
        }

        private static Dictionary<string, double> buildCompleteSkills()
        {
            var skills = new Dictionary<string, double>
            {
                [EzSkillSystems.MsdHoldRatioSkillId] = 0.25,
            };

            foreach (var axis in EzMinaSkillAxisExtensions.All)
                skills[axis.ToMsdSkillId()] = 10;

            return skills;
        }
    }
}
