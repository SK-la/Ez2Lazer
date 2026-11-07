// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.PPP;
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
    public class EzPPPEngineTest
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

            var skills = new EzPPPEngine().CalculateChart(beatmap, Array.Empty<Mod>());

            double aim = skills[EzPPPSkillAxis.Aim.ToChartSkillId()];
            double jump = skills[EzPPPSkillAxis.JumpAim.ToChartSkillId()];
            double flow = skills[EzPPPSkillAxis.FlowAim.ToChartSkillId()];
            double speed = skills[EzPPPSkillAxis.Speed.ToChartSkillId()];
            double stamina = skills[EzPPPSkillAxis.Stamina.ToChartSkillId()];
            double star = skills[EzPPPSkillAxis.StarRating.ToChartSkillId()];
            double expected = 1.6 * Math.Pow(Math.Pow(aim, 3) + Math.Pow(Math.Max(speed, stamina), 3), 1.0 / 3.0);

            Assert.That(isCompleteChart(0, skills), Is.True);
            Assert.That(jump, Is.GreaterThan(0.01));
            Assert.That(jump, Is.GreaterThan(flow));
            Assert.That(skills[EzPPPSkillAxis.Accuracy.ToChartSkillId()], Is.GreaterThanOrEqualTo(1));
            Assert.That(star, Is.EqualTo(expected).Within(1e-6));
        }

        [Test]
        public void Osu_chart_produces_every_declared_axis_including_raw_aim()
        {
            var beatmap = chart(0);
            beatmap.Difficulty.CircleSize = 4;
            beatmap.Difficulty.OverallDifficulty = 8;

            for (int i = 0; i < 16; i++)
            {
                beatmap.HitObjects.Add(new HitCircle
                {
                    StartTime = i * 180,
                    Position = new Vector2(i % 2 == 0 ? 80 : 430, 192),
                });
            }

            var skills = new EzPPPEngine().CalculateChart(beatmap, Array.Empty<Mod>());

            Assert.That(skills.ContainsKey(EzPPPSkillAxis.RawAim.ToChartSkillId()), Is.True);
            Assert.That(skills[EzPPPSkillAxis.RawAim.ToChartSkillId()], Is.GreaterThan(0));
            Assert.That(skills[EzPPPSkillAxis.Precision.ToChartSkillId()], Is.GreaterThanOrEqualTo(0));
            Assert.That(skills[EzPPPSkillAxis.Aim.ToChartSkillId()], Is.GreaterThanOrEqualTo(skills[EzPPPSkillAxis.JumpAim.ToChartSkillId()]));
        }

        [Test]
        public void Osu_performance_is_computed_from_the_chart_vector()
        {
            var beatmap = chart(0);
            beatmap.Difficulty.CircleSize = 4;
            beatmap.Difficulty.OverallDifficulty = 8;

            for (int i = 0; i < 64; i++)
            {
                beatmap.HitObjects.Add(new HitCircle
                {
                    StartTime = i * 180,
                    Position = new Vector2(i % 2 == 0 ? 80 : 430, 192),
                });
            }

            var skills = new EzPPPEngine().CalculateChart(beatmap, Array.Empty<Mod>());
            double pp = EzPPPPerformanceCalculators.Calculate(0, skills, beatmap, Array.Empty<Mod>(), 1);

            Assert.That(pp, Is.GreaterThan(0));
            Assert.That(pp, Is.LessThan(2000));
        }

        [Test]
        public void Taiko_alternation_produces_finite_skills()
        {
            var beatmap = chart(EzLocalProfileConstants.TAIKO_RULESET_ID);

            for (int i = 0; i < 24; i++)
            {
                beatmap.HitObjects.Add(new Hit
                {
                    StartTime = i * (i % 4 == 0 ? 160 : 100),
                    Type = i % 2 == 0 ? HitType.Centre : HitType.Rim,
                });
            }

            var skills = new EzPPPEngine().CalculateChart(beatmap, Array.Empty<Mod>());

            Assert.That(isCompleteChart(EzLocalProfileConstants.TAIKO_RULESET_ID, skills), Is.True);
            Assert.That(skills[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.COLOUR)], Is.GreaterThan(0));
            Assert.That(skills[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STAR_RATING)], Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Catch_spaced_fruits_produce_movement()
        {
            var beatmap = chart(EzLocalProfileConstants.CATCH_RULESET_ID);
            beatmap.Difficulty.CircleSize = 4;

            for (int i = 0; i < 12; i++)
            {
                beatmap.HitObjects.Add(new Fruit
                {
                    StartTime = i * 250,
                    OriginalX = i % 2 == 0 ? 40 : 360,
                });
            }

            var skills = new EzPPPEngine().CalculateChart(beatmap, Array.Empty<Mod>());

            Assert.That(isCompleteChart(EzLocalProfileConstants.CATCH_RULESET_ID, skills), Is.True);
            Assert.That(skills[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.MOVEMENT)], Is.GreaterThan(0));
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

            var skills = new EzPPPEngine().CalculateChart(beatmap, Array.Empty<Mod>());

            Assert.That(isCompleteChart(3, skills), Is.True);
            Assert.That(skills[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STRAIN)], Is.GreaterThan(0));
            Assert.That(skills[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STAR_RATING)], Is.EqualTo(skills[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STRAIN)]));
        }

        [Test]
        public void Play_scales_chart_by_accuracy()
        {
            var chart = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzPPPSkillAxis.Speed.ToChartSkillId()] = 4,
                [EzPPPSkillAxis.Accuracy.ToChartSkillId()] = 2,
            };

            var score = new ScoreInfo
            {
                Accuracy = 1,
                MaxCombo = 10,
                MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Great] = 10 },
            };

            var play = new EzPPPEngine().CalculatePlay(chart, score);

            Assert.That(play[EzPPPSkillAxis.Speed.ToPlayerSkillId()], Is.EqualTo(4).Within(1e-6));
            Assert.That(play[EzPPPSkillAxis.Accuracy.ToPlayerSkillId()], Is.EqualTo(2).Within(1e-6));
        }

        private static Beatmap chart(int rulesetId) => new Beatmap
        {
            BeatmapInfo = new BeatmapInfo
            {
                Ruleset = new RulesetInfo { OnlineID = rulesetId, Available = true },
            },
        };

        /// <summary>
        /// Completeness comes from the producer's declared axis set, which is the only axis list in the system.
        /// </summary>
        private static bool isCompleteChart(int rulesetOnlineId, IReadOnlyDictionary<string, double> skills) => EzMetricRegistries.ChartRegistry.Get(EzChartMetricProducer.Ppp.Id())
                                                                                                                                  ?.IsCompleteFor(EzSkillSystems.UNSLICED_KEY, rulesetOnlineId, skills)
                                                                                                                == true;
    }
}
