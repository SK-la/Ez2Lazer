// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Scoring;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzPpPlusStubEngineTest
    {
        [Test]
        public void CalculateChart_fills_ppplus_shaped_axes()
        {
            var engine = new EzPpPlusStubEngine();
            var attrs = new OsuDifficultyAttributes
            {
                AimDifficulty = 4.2,
                SpeedDifficulty = 3.1,
                SliderFactor = 0.4,
                SpeedNoteCount = 800,
                Mods = [],
            };
            var difficulty = new BeatmapDifficulty
            {
                CircleSize = 5,
                OverallDifficulty = 8,
            };

            var chart = engine.CalculateChart(attrs, difficulty, lengthSeconds: 120);
            var skills = chart.ToChartSkills();

            Assert.That(skills[EzOsuSkillAxis.JumpAim.ToChartSkillId()], Is.GreaterThan(0));
            Assert.That(skills[EzOsuSkillAxis.FlowAim.ToChartSkillId()], Is.GreaterThan(0));
            Assert.That(skills[EzOsuSkillAxis.Precision.ToChartSkillId()], Is.GreaterThan(0));
            Assert.That(skills[EzOsuSkillAxis.Speed.ToChartSkillId()], Is.EqualTo(3.1).Within(1e-9));
            Assert.That(skills[EzOsuSkillAxis.Stamina.ToChartSkillId()], Is.GreaterThan(0));
            Assert.That(skills[EzOsuSkillAxis.Accuracy.ToChartSkillId()], Is.GreaterThan(0));
            Assert.That(skills[EzOsuSkillAxis.Aim.ToChartSkillId()], Is.GreaterThan(0));
            Assert.That(EzPpPlusAttributes.IsCompleteChartCache(skills), Is.True);
            Assert.That(skills.Keys.All(k => k.StartsWith(EzSkillSystems.BEATMAP_PPPLUS + ".", System.StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void CalculatePlay_scales_by_accuracy()
        {
            var engine = new EzPpPlusStubEngine();
            var chart = new EzPpPlusAttributes(5, 4, 3, 2, 3.5, 3.2, 2.5);
            var score = new ScoreInfo
            {
                Accuracy = 0.95,
                MaxCombo = 500,
            };

            var play = engine.CalculatePlay(chart, score);
            Assert.That(play.JumpAim, Is.LessThan(chart.JumpAim));
            Assert.That(play.JumpAim, Is.GreaterThan(0));

            var skills = play.ToPlayerSkills();
            Assert.That(skills.Keys.All(k => k.StartsWith(EzSkillSystems.PLAYER_PPPLUS + ".", System.StringComparison.Ordinal)), Is.True);
        }
    }
}
