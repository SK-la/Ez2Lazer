// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzOsuSkillMappingTest
    {
        [Test]
        public void FromDifficultyAttributes_maps_aim_speed_reading()
        {
            var attrs = new OsuDifficultyAttributes
            {
                AimDifficulty = 4.2,
                SpeedDifficulty = 3.1,
                ReadingDifficulty = 2.5,
                FlashlightDifficulty = 1.8,
                Mods = [],
            };

            var skills = EzOsuSkillMapping.FromDifficultyAttributes(attrs);

            Assert.That(skills[EzOsuSkillAxis.Aim.ToDiffSkillId()], Is.EqualTo(4.2).Within(1e-9));
            Assert.That(skills[EzOsuSkillAxis.Speed.ToDiffSkillId()], Is.EqualTo(3.1).Within(1e-9));
            Assert.That(skills[EzOsuSkillAxis.Reading.ToDiffSkillId()], Is.EqualTo(2.5).Within(1e-9));
            // Without FL mod, ToDatabaseAttributes omits flashlight.
            Assert.That(skills[EzOsuSkillAxis.Flashlight.ToDiffSkillId()], Is.EqualTo(0));
            Assert.That(EzOsuSkillMapping.IsCurrentDiffCache(skills), Is.True);
        }

        [Test]
        public void FromDifficultyAttributes_includes_flashlight_with_fl_mod()
        {
            var attrs = new OsuDifficultyAttributes
            {
                AimDifficulty = 1,
                SpeedDifficulty = 1,
                ReadingDifficulty = 1,
                FlashlightDifficulty = 2.25,
                Mods = [new OsuModFlashlight()],
            };

            var skills = EzOsuSkillMapping.FromDifficultyAttributes(attrs);
            Assert.That(skills[EzOsuSkillAxis.Flashlight.ToDiffSkillId()], Is.EqualTo(2.25).Within(1e-9));
        }

        [Test]
        public void FromPerformanceAttributes_maps_pp_portions()
        {
            var attrs = new OsuPerformanceAttributes
            {
                Aim = 100,
                Speed = 80,
                Flashlight = 10,
                Reading = 20,
                Accuracy = 30,
                Total = 240,
            };

            var skills = EzOsuSkillMapping.FromPerformanceAttributes(attrs);

            Assert.That(skills[EzOsuSkillAxis.Aim.ToPerfSkillId()], Is.EqualTo(100));
            Assert.That(skills[EzOsuSkillAxis.Speed.ToPerfSkillId()], Is.EqualTo(80));
            Assert.That(skills[EzOsuSkillAxis.Flashlight.ToPerfSkillId()], Is.EqualTo(10));
            Assert.That(skills[EzOsuSkillAxis.Reading.ToPerfSkillId()], Is.EqualTo(20));
            Assert.That(skills[EzOsuSkillAxis.Accuracy.ToPerfSkillId()], Is.EqualTo(30));
            Assert.That(skills.Keys.All(k => k.StartsWith(EzSkillSystems.PLAYER_OSU_PERF + ".", System.StringComparison.Ordinal)), Is.True);
        }
    }
}
