// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills.PPP
{
    /// <summary>Shared PPP helpers: decay, smooth steps, and section-peak aggregation.</summary>
    public static class EzPPPMath
    {
        public static double Decay(double deltaMs, double beta)
        {
            if (deltaMs <= 0)
                return 1;

            if (beta <= 0)
                return 0;

            return Math.Pow(beta, deltaMs / 1000.0);
        }

        public static double T2T(double value, double start, double length)
        {
            if (length <= 0)
                return value >= start ? 1 : 0;

            if (value <= start)
                return 0;

            if (value >= start + length)
                return 1;

            return (1 - Math.Cos((value - start) * Math.PI / length)) / 2;
        }

        public static double T2F(double value, double start, double length)
            => 1 - T2T(value, start, length);

        public static bool RatioEqual(double rho, double a, double b)
            => a + 5 > rho * b && a - 5 < rho * b;

        public static bool CoarseEqual(double a, double b)
            => a * 1.25 > b && a / 1.25 < b;

        public static double PNorm(double a, double b, double p)
        {
            if (p <= 0)
                return 0;

            return Math.Pow(Math.Pow(Math.Abs(a), p) + Math.Pow(Math.Abs(b), p), 1.0 / p);
        }

        public static double Aggregate(List<double> times, List<double> strains, double sectionLength, double gamma, double sectionInitBeta)
        {
            if (times.Count == 0 || sectionLength <= 0)
                return 0;

            var peaks = SectionPeaks(times, strains, sectionLength, sectionInitBeta);
            var positive = new List<double>(peaks.Count);

            foreach (double peak in peaks)
            {
                if (peak > 0 && double.IsFinite(peak))
                    positive.Add(peak);
            }

            positive.Sort((x, y) => y.CompareTo(x));

            double sum = 0;

            for (int i = 0; i < positive.Count; i++)
                sum += positive[i] * Math.Pow(gamma, i);

            return sum;
        }

        /// <summary>
        /// Peak strain per section, starting at the first section boundary at or after the first object and
        /// including the trailing partial section.
        /// </summary>
        /// <param name="sectionInitBeta">
        /// Decay constant used to carry the previous object's strain into a section that starts inside a gap.
        /// Pass 0 for a skill whose section never inherits strain.
        /// </param>
        public static List<double> SectionPeaks(List<double> times, List<double> strains, double sectionLength, double sectionInitBeta)
        {
            var peaks = new List<double>();
            int count = Math.Min(times.Count, strains.Count);

            if (count == 0 || sectionLength <= 0)
                return peaks;

            double sectionEnd = Math.Ceiling(times[0] / sectionLength) * sectionLength;
            double peak = 0;

            for (int i = 0; i < count; i++)
            {
                while (times[i] > sectionEnd)
                {
                    peaks.Add(peak);
                    peak = i > 0 && sectionInitBeta > 0 ? strains[i - 1] * Decay(sectionEnd - times[i - 1], sectionInitBeta) : 0;
                    sectionEnd += sectionLength;
                }

                if (double.IsFinite(strains[i]) && strains[i] > peak)
                    peak = strains[i];
            }

            peaks.Add(peak);
            return peaks;
        }

        public static double Finite(double value)
            => double.IsFinite(value) ? value : 0;

        /// <summary>
        /// Inverse of the standard normal CDF. Acklam's rational approximation, relative error about 1e-9,
        /// which is far below what a displayed pp value can show.
        /// </summary>
        public static double InverseNormalCdf(double p)
        {
            if (p <= 0)
                return double.NegativeInfinity;

            if (p >= 1)
                return double.PositiveInfinity;

            const double p_low = 0.02425;
            const double p_high = 1 - p_low;

            if (p < p_low)
            {
                double q = Math.Sqrt(-2 * Math.Log(p));
                return (((((c0 * q + c1) * q + c2) * q + c3) * q + c4) * q + c5)
                       / ((((d0 * q + d1) * q + d2) * q + d3) * q + 1);
            }

            if (p > p_high)
            {
                double q = Math.Sqrt(-2 * Math.Log(1 - p));
                return -(((((c0 * q + c1) * q + c2) * q + c3) * q + c4) * q + c5)
                       / ((((d0 * q + d1) * q + d2) * q + d3) * q + 1);
            }

            double t = p - 0.5;
            double r = t * t;
            return (((((a0 * r + a1) * r + a2) * r + a3) * r + a4) * r + a5) * t
                   / (((((b0 * r + b1) * r + b2) * r + b3) * r + b4) * r + 1);
        }

        /// <summary>Inverse of the error function, from <see cref="InverseNormalCdf"/>.</summary>
        public static double InverseErf(double p)
        {
            if (p <= -1)
                return double.NegativeInfinity;

            if (p >= 1)
                return double.PositiveInfinity;

            return InverseNormalCdf((p + 1) / 2) / Math.Sqrt(2);
        }

        private const double a0 = -3.969683028665376e+01;
        private const double a1 = 2.209460984245205e+02;
        private const double a2 = -2.759285104469687e+02;
        private const double a3 = 1.383577518672690e+02;
        private const double a4 = -3.066479806614716e+01;
        private const double a5 = 2.506628277459239e+00;

        private const double b0 = -5.447609879822406e+01;
        private const double b1 = 1.615858368580409e+02;
        private const double b2 = -1.556989798598866e+02;
        private const double b3 = 6.680131188771972e+01;
        private const double b4 = -1.328068155288572e+01;

        private const double c0 = -7.784894002430293e-03;
        private const double c1 = -3.223964580411365e-01;
        private const double c2 = -2.400758277161838e+00;
        private const double c3 = -2.549732539343734e+00;
        private const double c4 = 4.374664141464968e+00;
        private const double c5 = 2.938163982698783e+00;

        private const double d0 = 7.784695709041462e-03;
        private const double d1 = 3.224671290700398e-01;
        private const double d2 = 2.445134137142996e+00;
        private const double d3 = 3.754408661907416e+00;
    }
}
