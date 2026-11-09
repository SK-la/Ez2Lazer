// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills.PPP;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzPPPMathTest
    {
        [Test]
        public void Sections_start_at_the_first_boundary_and_keep_the_trailing_partial()
        {
            var times = new List<double> { 180, 300, 500, 900 };
            var strains = new List<double> { 1, 2, 3, 4 };

            var peaks = EzPPPMath.SectionPeaks(times, strains, 400, 0);

            Assert.That(peaks, Is.EqualTo(new[] { 2, 3, 4 }));
        }

        [Test]
        public void A_section_inside_a_gap_inherits_the_decayed_previous_strain()
        {
            var times = new List<double> { 180, 1180 };
            var strains = new List<double> { 10, 1 };

            var peaks = EzPPPMath.SectionPeaks(times, strains, 400, 0.5);

            Assert.That(peaks.Count, Is.EqualTo(3));
            Assert.That(peaks[0], Is.EqualTo(10).Within(1e-9));
            Assert.That(peaks[1], Is.EqualTo(10 * EzPPPMath.Decay(220, 0.5)).Within(1e-9));
            Assert.That(peaks[2], Is.EqualTo(10 * EzPPPMath.Decay(620, 0.5)).Within(1e-9));
        }

        [Test]
        public void Difficulty_value_weights_peaks_in_descending_order()
        {
            var times = new List<double> { 0, 400, 800 };
            var strains = new List<double> { 1, 3, 2 };

            double value = EzPPPMath.Aggregate(times, strains, 400, 0.9, 0);

            Assert.That(value, Is.EqualTo(3 + 2 * 0.9 + 1 * 0.81).Within(1e-9));
        }

        [Test]
        public void Zero_peaks_do_not_consume_a_weight_step()
        {
            var times = new List<double> { 0, 400, 800 };
            var strains = new List<double> { 0, 5, 1 };

            double value = EzPPPMath.Aggregate(times, strains, 400, 0.9, 0);

            Assert.That(value, Is.EqualTo(5 + 0.9).Within(1e-9));
        }
    }
}
