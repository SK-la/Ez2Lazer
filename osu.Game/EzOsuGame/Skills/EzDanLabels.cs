// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Ports mania-hub labels.ts parseDan / srToRawDan (stream family default) and parseLnDan.
    /// Chart rawDan is heuristic until LeoBlack is ported.
    /// </summary>
    public static class EzDanLabels
    {
        private static readonly string[] dan_labels =
        {
            "1", "2", "3", "4", "5", "6", "7", "8", "9", "10",
            "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta", "iota", "kappa",
        };

        // stream family means from mania-hub labels.ts
        private static readonly double[] stream_means =
        {
            3.1, 3.5, 3.9, 4.3, 4.7, 5.05, 5.35, 5.6, 5.78, 5.92,
            6.12, 6.5, 6.92, 7.42, 8.08, 8.8, 9.65, 10.42, 11.2, 12.0,
        };

        private static readonly double[] jack_means =
        {
            3.15, 3.55, 3.95, 4.35, 4.75, 5.15, 5.45, 5.7, 5.92, 6.1,
            6.35, 6.75, 7.15, 7.65, 8.25, 8.85, 9.55, 10.25, 11.0, 11.8,
        };

        private static readonly double[] jumpstream_means =
        {
            3.15, 3.55, 3.95, 4.35, 4.75, 5.1, 5.4, 5.66, 5.86, 6.02,
            6.35, 6.72, 7.08, 7.55, 8.15, 8.85, 9.65, 10.38, 11.14, 11.95,
        };

        private static readonly double[] handstream_means =
        {
            3.2, 3.6, 4.0, 4.4, 4.8, 5.15, 5.45, 5.72, 5.92, 6.08,
            6.6, 6.9, 6.96, 7.72, 8.48, 9.18, 9.98, 10.72, 11.46, 12.2,
        };

        private static readonly double[] stamina_means =
        {
            3.2, 3.6, 4.0, 4.4, 4.8, 5.15, 5.45, 5.72, 5.92, 6.08,
            6.3, 6.7, 7.12, 7.62, 8.28, 8.98, 9.78, 10.52, 11.26, 12.0,
        };

        private static readonly double[] chordjack_means =
        {
            3.2, 3.6, 4.0, 4.42, 4.82, 5.18, 5.48, 5.75, 5.95, 6.12,
            6.35, 6.75, 7.15, 7.65, 8.25, 8.85, 9.55, 10.25, 11.0, 11.8,
        };

        private static readonly double[] tech_means =
        {
            3.25, 3.65, 4.05, 4.48, 4.88, 5.25, 5.55, 5.82, 6.02, 6.18,
            6.42, 6.82, 7.22, 7.72, 8.35, 9.02, 9.8, 10.52, 11.26, 12.0,
        };

        public const int LN_LADDER_TOP = 17;

        public static double SrToRawDan(double sr, string family = "stream")
        {
            if (!double.IsFinite(sr) || sr <= 0)
                return 1;

            return rawDanFromMeans(sr, meansFor(family));
        }

        public static string LabelFor(double rawDan, string side, int keyCount)
        {
            if (side == DanSkillSystem.SIDE_LN && keyCount == 4)
                return parseLnDan(rawDan);

            // Non-4K table labels deferred; use rice greek ladder as readable MVP.
            return parseDan(rawDan);
        }

        public static double FloorFor(string side, int keyCount)
            => keyCount == 4 ? 0.5 : 0;

        public static double? CeilingFor(string side, int keyCount)
        {
            if (keyCount == 4 && side == DanSkillSystem.SIDE_LN)
                return LN_LADDER_TOP + 0.5;

            return null;
        }

        public static string DominantFamily(IReadOnlyDictionary<string, double> msdSkills)
        {
            (string axis, string family)[] map =
            {
                (EzSkillIds.Msd(EzSkillIds.STREAM), "stream"),
                (EzSkillIds.Msd(EzSkillIds.JUMPSTREAM), "jumpstream"),
                (EzSkillIds.Msd(EzSkillIds.HANDSTREAM), "handstream"),
                (EzSkillIds.Msd(EzSkillIds.STAMINA), "stamina"),
                (EzSkillIds.Msd(EzSkillIds.JACK_SPEED), "jack"),
                (EzSkillIds.Msd(EzSkillIds.CHORDJACK), "chordjack"),
                (EzSkillIds.Msd(EzSkillIds.TECHNICAL), "tech"),
            };

            string best = "stream";
            double bestValue = -1;

            foreach (var (axis, family) in map)
            {
                if (msdSkills.TryGetValue(axis, out double value) && value > bestValue)
                {
                    bestValue = value;
                    best = family;
                }
            }

            return best;
        }

        private static string parseDan(double rawDan)
        {
            int maxLevel = dan_labels.Length;
            int level = Math.Min(maxLevel, Math.Max(1, (int)Math.Round(rawDan)));
            double offset = rawDan - level;
            string? variant = offset <= -0.45 ? "--" : offset <= -0.25 ? "-" : offset < 0.1 ? null : offset < 0.26 ? "+" : "++";
            return $"{dan_labels[level - 1]}{variant ?? string.Empty}";
        }

        private static string parseLnDan(double rawDan)
        {
            int level = Math.Max(1, Math.Min(LN_LADDER_TOP, (int)Math.Round(rawDan)));
            double offset = rawDan - level;
            string? variant = offset <= -0.45 ? "--" : offset <= -0.25 ? "-" : offset < 0.1 ? null : offset < 0.26 ? "+" : "++";
            return $"{level}{variant ?? string.Empty}";
        }

        private static double[] meansFor(string family) => family switch
        {
            "jack" => jack_means,
            "jumpstream" => jumpstream_means,
            "handstream" => handstream_means,
            "stamina" => stamina_means,
            "chordjack" => chordjack_means,
            "tech" => tech_means,
            _ => stream_means,
        };

        private static double rawDanFromMeans(double value, double[] means)
        {
            int maxIndex = dan_labels.Length - 1;
            var capped = means.AsSpan(0, Math.Min(means.Length, maxIndex + 1));

            if (value < boundaryLower(capped, 0))
                return 1;

            int last = capped.Length - 1;
            if (value >= boundaryUpper(capped, last))
                return maxIndex + 1;

            for (int index = 0; index < capped.Length; index++)
            {
                double lower = boundaryLower(capped, index);
                double upper = boundaryUpper(capped, index);

                if (value >= lower && value < upper)
                {
                    double t = (value - lower) / Math.Max(0.001, upper - lower);
                    return index + 1 + t - 0.5;
                }
            }

            return 1;
        }

        private static double boundaryLower(ReadOnlySpan<double> means, int index)
        {
            double mean = means[index];

            if (index == 0)
            {
                return mean - (means[
                    index + 1] - mean) / 2;
            }

            return (means[index - 1] + mean) / 2;
        }

        private static double boundaryUpper(ReadOnlySpan<double> means, int index)
        {
            double mean = means[index];
            if (index == means.Length - 1)
                return mean + (mean - means[index - 1]) / 2;

            return (mean + means[index + 1]) / 2;
        }
    }
}
