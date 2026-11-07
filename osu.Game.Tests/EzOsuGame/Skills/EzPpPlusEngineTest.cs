// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Ppp;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzPpPlusEngineTest
    {
        [Test]
        public void Osu_jump_pattern_rates_and_matches_star_formula()
        {
            var beatmap = chart(0);
            beatmap.Difficulty.CircleSize = 4;

            for (int i = 0; i < 16; i++)
            {
                beatmap.HitObjects.Add(new HitCircle
                {
                    StartTime = i * 180,
                    Position = new Vector2(i % 2 == 0 ? 80 : 430, 192),
                });
            }

            var skills = new EzPpPlusEngine().CalculateChart(beatmap, Array.Empty<Mod>());

            double aim = skills[EzOsuSkillAxis.Aim.ToChartSkillId()];
            double jump = skills[EzOsuSkillAxis.JumpAim.ToChartSkillId()];
            double flow = skills[EzOsuSkillAxis.FlowAim.ToChartSkillId()];
            double speed = skills[EzOsuSkillAxis.Speed.ToChartSkillId()];
            double stamina = skills[EzOsuSkillAxis.Stamina.ToChartSkillId()];
            double star = skills[EzOsuSkillAxis.StarRating.ToChartSkillId()];
            double expected = 1.6 * Math.Pow(Math.Pow(aim, 3) + Math.Pow(Math.Max(speed, stamina), 3), 1.0 / 3.0);

            Assert.That(EzPppSkillCatalog.IsCompleteChart(0, skills), Is.True);
            Assert.That(jump, Is.GreaterThan(0.01));
            Assert.That(jump, Is.GreaterThan(flow));
            Assert.That(skills[EzOsuSkillAxis.Accuracy.ToChartSkillId()], Is.GreaterThanOrEqualTo(1));
            Assert.That(star, Is.EqualTo(expected).Within(1e-6));
        }

        [Test]
        public void Taiko_alternation_produces_finite_skills()
        {
            var beatmap = chart(EzPppSkillCatalog.TAIKO_RULESET_ID);

            for (int i = 0; i < 24; i++)
            {
                beatmap.HitObjects.Add(new Hit
                {
                    StartTime = i * (i % 4 == 0 ? 160 : 100),
                    Type = i % 2 == 0 ? HitType.Centre : HitType.Rim,
                });
            }

            var skills = new EzPpPlusEngine().CalculateChart(beatmap, Array.Empty<Mod>());

            Assert.That(EzPppSkillCatalog.IsCompleteChart(EzPppSkillCatalog.TAIKO_RULESET_ID, skills), Is.True);
            Assert.That(skills[EzPppSkillCatalog.ChartSkillId(EzPppSkillCatalog.COLOUR)], Is.GreaterThan(0));
            Assert.That(skills[EzPppSkillCatalog.ChartSkillId(EzPppSkillCatalog.STAR_RATING)], Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Catch_spaced_fruits_produce_movement()
        {
            var beatmap = chart(EzPppSkillCatalog.CATCH_RULESET_ID);
            beatmap.Difficulty.CircleSize = 4;

            for (int i = 0; i < 12; i++)
            {
                beatmap.HitObjects.Add(new Fruit
                {
                    StartTime = i * 250,
                    OriginalX = i % 2 == 0 ? 40 : 360,
                });
            }

            var skills = new EzPpPlusEngine().CalculateChart(beatmap, Array.Empty<Mod>());

            Assert.That(EzPppSkillCatalog.IsCompleteChart(EzPppSkillCatalog.CATCH_RULESET_ID, skills), Is.True);
            Assert.That(skills[EzPppSkillCatalog.ChartSkillId(EzPppSkillCatalog.MOVEMENT)], Is.GreaterThan(0));
        }

        [Test]
        public void Mania_chords_produce_strain()
        {
            var beatmap = chart(3);
            beatmap.Difficulty.CircleSize = 4;

            for (int i = 0; i < 16; i++)
            {
                beatmap.HitObjects.Add(new Note { StartTime = i * 120, Column = i % 4 });

                if (i % 4 == 0)
                    beatmap.HitObjects.Add(new Note { StartTime = i * 120, Column = (i + 1) % 4 });
            }

            var skills = new EzPpPlusEngine().CalculateChart(beatmap, Array.Empty<Mod>());

            Assert.That(EzPppSkillCatalog.IsCompleteChart(3, skills), Is.True);
            Assert.That(skills[EzPppSkillCatalog.ChartSkillId(EzPppSkillCatalog.STRAIN)], Is.GreaterThan(0));
            Assert.That(skills[EzPppSkillCatalog.ChartSkillId(EzPppSkillCatalog.STAR_RATING)], Is.EqualTo(skills[EzPppSkillCatalog.ChartSkillId(EzPppSkillCatalog.STRAIN)]));
        }

        [Test]
        public void Play_scales_chart_by_accuracy()
        {
            var chart = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzOsuSkillAxis.Speed.ToChartSkillId()] = 4,
                [EzOsuSkillAxis.Accuracy.ToChartSkillId()] = 2,
            };

            var score = new ScoreInfo
            {
                Accuracy = 1,
                MaxCombo = 10,
                MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Great] = 10 },
            };

            var play = new EzPpPlusEngine().CalculatePlay(chart, score);

            Assert.That(play[EzOsuSkillAxis.Speed.ToPlayerSkillId()], Is.EqualTo(4).Within(1e-6));
            Assert.That(play[EzOsuSkillAxis.Accuracy.ToPlayerSkillId()], Is.EqualTo(2).Within(1e-6));
        }

        private static Beatmap chart(int rulesetId) => new Beatmap
        {
            BeatmapInfo = new BeatmapInfo
            {
                Ruleset = new RulesetInfo { OnlineID = rulesetId, Available = true },
            },
        };
    }
}
