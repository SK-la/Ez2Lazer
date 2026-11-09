// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Taiko.Mods;
using osu.Game.Rulesets.Taiko.Objects;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzPPPChartAlgorithmTest
    {
        [Test]
        public void Taiko_double_time_changes_star_rating()
        {
            var beatmap = taikoChart();

            for (int i = 0; i < 32; i++)
            {
                beatmap.HitObjects.Add(new Hit
                {
                    StartTime = i * 120,
                    Type = i % 2 == 0 ? HitType.Centre : HitType.Rim,
                });
            }

            var nomod = new EzPPPEngine().CalculateChart(beatmap, Array.Empty<Mod>());
            var dt = new EzPPPEngine().CalculateChart(beatmap, new Mod[] { new TaikoModDoubleTime() });

            double srNomod = nomod[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STAR_RATING)];
            double srDt = dt[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STAR_RATING)];

            Assert.That(srNomod, Is.GreaterThan(0));
            Assert.That(srDt, Is.Not.EqualTo(srNomod).Within(1e-9));
        }

        [Test]
        public void Taiko_coloured_pattern_splits_axes_from_star_rating()
        {
            var beatmap = taikoChart();

            for (int i = 0; i < 24; i++)
            {
                beatmap.HitObjects.Add(new Hit
                {
                    StartTime = i * (i % 4 == 0 ? 160 : 100),
                    Type = i % 2 == 0 ? HitType.Centre : HitType.Rim,
                });
            }

            var skills = new EzPPPEngine().CalculateChart(beatmap, Array.Empty<Mod>());

            double rhythm = skills[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.RHYTHM)];
            double colour = skills[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.COLOUR)];
            double stamina = skills[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STAMINA)];
            double star = skills[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STAR_RATING)];

            Assert.That(colour, Is.GreaterThan(0));
            Assert.That(rhythm, Is.GreaterThan(0));
            Assert.That(stamina, Is.GreaterThan(0));
            Assert.That(star, Is.GreaterThan(0));
            Assert.That(Math.Abs(star - rhythm), Is.GreaterThan(0.001));
            Assert.That(Math.Abs(star - colour), Is.GreaterThan(0.001));
        }

        [Test]
        public void Catch_hard_rock_increases_movement()
        {
            var beatmap = catchChart();
            beatmap.Difficulty.CircleSize = 4;

            for (int i = 0; i < 16; i++)
            {
                beatmap.HitObjects.Add(new Fruit
                {
                    StartTime = i * 200,
                    OriginalX = i % 2 == 0 ? 40 : 380,
                });
            }

            var nomod = new EzPPPEngine().CalculateChart(beatmap, Array.Empty<Mod>());
            var hr = new EzPPPEngine().CalculateChart(beatmap, new Mod[] { new CatchModHardRock() });

            double movementNomod = nomod[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.MOVEMENT)];
            double movementHr = hr[EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.MOVEMENT)];

            Assert.That(movementNomod, Is.GreaterThan(0));
            Assert.That(movementHr, Is.GreaterThan(movementNomod));
        }

        [Test]
        public void Taiko_hidden_convert_skips_difficulty_bonus_on_perfect_pp()
        {
            const double star = 5;
            const int hits = 800;

            double nativeHidden = EzPPPPerformance.Taiko(star, hits, 25, 50, hidden: true, easy: false, hardRock: false, flashlight: false, isConvert: false);
            double convertHidden = EzPPPPerformance.Taiko(star, hits, 25, 50, hidden: true, easy: false, hardRock: false, flashlight: false, isConvert: true);

            Assert.That(nativeHidden, Is.GreaterThan(convertHidden));
        }

        private static Beatmap taikoChart() => new Beatmap
        {
            BeatmapInfo = new BeatmapInfo
            {
                Ruleset = new RulesetInfo { OnlineID = EzLocalProfileConstants.TAIKO_RULESET_ID, Available = true },
            },
        };

        private static Beatmap catchChart() => new Beatmap
        {
            BeatmapInfo = new BeatmapInfo
            {
                Ruleset = new RulesetInfo { OnlineID = EzLocalProfileConstants.CATCH_RULESET_ID, Available = true },
            },
        };
    }
}
