// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills.Ppp
{
    /// <summary>Shared PPP helpers: decay, smooth steps, and section-peak aggregation.</summary>
    public static class EzPppMath
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

        public static double Aggregate(List<double> times, List<double> strains, double sectionLength, double gamma)
        {
            if (times.Count == 0 || sectionLength <= 0)
                return 0;

            var peaks = SectionPeaks(times, strains, sectionLength);
            var positive = new List<double>(peaks.Count);

            foreach (double peak in peaks.Values)
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

        public static Dictionary<long, double> SectionPeaks(List<double> times, List<double> strains, double sectionLength)
        {
            var peaks = new Dictionary<long, double>();
            int count = Math.Min(times.Count, strains.Count);

            for (int i = 0; i < count; i++)
            {
                if (!double.IsFinite(strains[i]) || strains[i] <= 0)
                    continue;

                long bucket = (long)Math.Floor(times[i] / sectionLength);

                if (!peaks.TryGetValue(bucket, out double peak) || strains[i] > peak)
                    peaks[bucket] = strains[i];
            }

            return peaks;
        }

        public static double Finite(double value)
            => double.IsFinite(value) ? value : 0;
    }
}
