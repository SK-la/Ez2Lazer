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
        public void Completeness_ignores_the_hold_ratio()
        {
            // The hold ratio is a hitobject ratio, not a Mina axis: it must not gate the Mina stage.
            var skills = buildCompleteSkills();

            Assert.That(skills.ContainsKey("beatmap_msd.__hold_ratio"), Is.False);
            Assert.That(EzMinaUnit.IsCurrentMinaCache(skills), Is.True);
        }

        [Test]
        public void TestIsCurrentMinaCacheRequiresEveryAxis()
        {
            var skills = buildCompleteSkills();
            skills.Remove(EzMinaSkillAxis.JackSpeed.ToMsdSkillId());

            Assert.That(EzMinaUnit.IsCurrentMinaCache(skills), Is.False);
        }

        [Test]
        public void TestIsCurrentMinaCacheAcceptsCompleteCurrentIds()
        {
            Assert.That(EzMinaUnit.IsCurrentMinaCache(buildCompleteSkills()), Is.True);
            Assert.That(EzMinaUnit.IsSettledMinaCache(EzSkillRowState.Current, buildCompleteSkills()), Is.True);
        }

        [Test]
        public void TestUnrateableMarkerIsSettledButNotCurrent()
        {
            var skills = new Dictionary<string, double>();

            Assert.That(EzMinaUnit.IsCurrentMinaCache(skills), Is.False);
            Assert.That(EzMinaUnit.IsSettledMinaCache(EzSkillRowState.Unrateable, skills), Is.True);
        }

        [Test]
        public void TestEmptySkillsAreNeitherCurrentNorSettled()
        {
            var skills = new Dictionary<string, double>();

            Assert.That(EzMinaUnit.IsCurrentMinaCache(skills), Is.False);
            Assert.That(EzMinaUnit.IsSettledMinaCache(EzSkillRowState.Missing, skills), Is.False);
        }

        private static Dictionary<string, double> buildCompleteSkills()
        {
            var skills = new Dictionary<string, double>();

            foreach (var axis in EzMinaSkillAxisExtensions.All)
                skills[axis.ToMsdSkillId()] = 10;

            return skills;
        }
    }
}
