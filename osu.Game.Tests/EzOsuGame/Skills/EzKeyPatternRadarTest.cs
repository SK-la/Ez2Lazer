// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.EzOsuGame.Skills.SkillTag.KeyPattern;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzKeyPatternRadarTest
    {
        [Test]
        public void Stored_complete_rows_rebuild_five_axes_and_ln_from_hold_ratio()
        {
            var skills = new Dictionary<string, double>
            {
                ["Bracket"] = 1.25,
                ["Chord"] = 2.5,
                ["Jack"] = 0.4,
                ["Delay"] = 0.8,
                ["Dump"] = 3.1,
            };

            var radar = EzKeyPatternRadar.TryCreateFromStored(skills, holdRatio: 0.125);

            Assert.That(radar, Is.Not.Null);
            Assert.That(radar!.Value.Count, Is.EqualTo(6));
            Assert.That(radar.Value[0].Axis, Is.EqualTo("Bracket"));
            Assert.That(radar.Value[2].Axis, Is.EqualTo("LN%"));
            Assert.That(radar.Value[2].Value, Is.EqualTo(12.5).Within(1e-9));
            Assert.That(radar.Value[3].Axis, Is.EqualTo("Jack"));
        }

        [Test]
        public void Ln_percent_is_not_required_in_stored_rows()
        {
            var skills = new Dictionary<string, double>
            {
                ["Bracket"] = 1,
                ["Chord"] = 1,
                ["LN%"] = 50,
                ["Jack"] = 1,
                ["Delay"] = 1,
                ["Dump"] = 1,
            };

            Assert.That(EzKeyPatternAxisExtensions.HasCompleteRadarSkills(skills), Is.True);

            skills.Remove("LN%");
            Assert.That(EzKeyPatternAxisExtensions.HasCompleteRadarSkills(skills), Is.True);
            Assert.That(EzKeyPatternRadar.TryCreateFromStored(skills, 0), Is.Not.Null);
        }

        [Test]
        public void Incomplete_rows_do_not_build_a_radar()
        {
            var skills = new Dictionary<string, double> { ["Bracket"] = 1 };

            Assert.That(EzKeyPatternRadar.TryCreateFromStored(skills, 0), Is.Null);
        }

        [Test]
        public void Quantify_scales_relative_full_score_by_sunny_star()
        {
            Assert.That(EzKeyPatternUnit.ToQuantified(10, 8), Is.EqualTo(8).Within(1e-9));
            Assert.That(EzKeyPatternUnit.ToRelative(8, 8), Is.EqualTo(10).Within(1e-9));
        }

        [Test]
        public void Sunny_zero_is_the_na_stamp_not_a_quantifiable_star()
        {
            Assert.That(EzKeyPatternUnit.IsQuantifiableSunny(0), Is.False);
            Assert.That(EzKeyPatternUnit.IsQuantifiableSunny(-1), Is.False);
            Assert.That(EzKeyPatternUnit.IsQuantifiableSunny(0.01), Is.True);
        }
    }
}
