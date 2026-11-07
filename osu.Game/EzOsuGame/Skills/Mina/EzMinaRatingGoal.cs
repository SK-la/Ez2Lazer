// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Skills.Mina
{
    /// <summary>
    /// Ports mania-hub <c>ssrGoalForScore</c> / <c>estimateWifeAccuracy</c> (subset).
    /// Pre-release corrective: does not bump <see cref="EzMinaSkillAlgorithm.VERSION"/>.
    /// </summary>
    public static class EzMinaRatingGoal
    {
        // --- Goal clamps (hub ssrGoalForScore) ---
        public const double GOAL_MIN = 0.8;
        public const double CALC_GOAL_CAP = 0.965;
        public const double GOAL_CAP = 0.9975;

        // --- Judgement / OD window (hub estimateWifeAccuracy inputs) ---
        public const double ASSUMED_OD = 8;
        private const double ez_window_scale = 1.4;
        private const double wife3_full_points_ms = 5;
        private const double wife3_zero_ms = 65;
        private const double wife3_erf_dev_ms = 22.7;
        private const double wife3_miss_ms = 180;
        private const double wife3_miss_points = -2.75;
        private const double stable_max_window_ms = 16.5;
        private const double od_window_step_ms = 3;

        private static readonly double[] stable_window_bases_ms = { 64, 97, 127, 151 };

        private static readonly Dictionary<string, double[]> expected_points_cache = new Dictionary<string, double[]>();

        /// <summary>
        /// Returns null when goal would sit on the calc floor (play must not count).
        /// </summary>
        public static float? ForScore(ScoreInfo score, double? lnRatio = null, double? od = null)
        {
            double goal = forScoreUnchecked(score, lnRatio, od);
            if (!(goal > GOAL_MIN))
                return null;

            return (float)goal;
        }

        public static float ForAccuracy(double accuracy)
        {
            double acc = double.IsFinite(accuracy) ? accuracy : 0.93;
            return (float)(Math.Round(Math.Max(GOAL_MIN, Math.Min(CALC_GOAL_CAP, acc)) * 10_000) / 10_000);
        }

        private static double forScoreUnchecked(ScoreInfo score, double? lnRatio, double? od)
        {
            double? wife = estimateWifeAccuracy(score.Statistics, od, ezWindowScale(score.Mods));
            if (wife is null)
                return ForAccuracy(score.Accuracy);

            double wifeGoal = Math.Max(GOAL_MIN, Math.Min(GOAL_CAP, wife.Value));

            // Lazer judges LN head/tail separately — fade Wife toward plain accuracy by LN share.
            if (!score.IsLegacyScore)
            {
                double accGoal = ForAccuracy(score.Accuracy);
                double fade = lnRatio is null ? 1 : Math.Clamp(lnRatio.Value, 0, 1);
                return Math.Round((wifeGoal * (1 - fade) + accGoal * fade) * 10_000) / 10_000;
            }

            return Math.Round(wifeGoal * 10_000) / 10_000;
        }

        private static double ezWindowScale(IEnumerable<Mod> mods)
        {
            double scale = 1;

            foreach (var mod in mods)
            {
                string acronym = mod.Acronym;
                if (acronym == "EZ")
                    scale *= ez_window_scale;
                else if (acronym == "HR")
                    scale /= ez_window_scale;
            }

            return scale;
        }

        private static double? estimateWifeAccuracy(IReadOnlyDictionary<HitResult, int> statistics, double? od, double windowScale)
        {
            if (statistics.Count == 0)
                return null;

            double odValue = od is double o && double.IsFinite(o) ? Math.Clamp(o, 0, 10) : ASSUMED_OD;
            double scale = double.IsFinite(windowScale) && windowScale > 0 ? windowScale : 1;
            double[] expected = expectedWife3Points(odValue, scale);

            int perfect = readCount(statistics, HitResult.Perfect);
            int great = readCount(statistics, HitResult.Great);
            int good = readCount(statistics, HitResult.Good);
            int ok = readCount(statistics, HitResult.Ok);
            int meh = readCount(statistics, HitResult.Meh);
            int miss = readCount(statistics, HitResult.Miss);

            int total = perfect + great + good + ok + meh + miss;
            if (total <= 0)
                return null;

            double points = perfect * expected[0]
                            + great * expected[1]
                            + good * expected[2]
                            + ok * expected[3]
                            + meh * expected[4]
                            + miss * expected[5];

            return points / total;
        }

        private static int readCount(IReadOnlyDictionary<HitResult, int> statistics, HitResult result)
            => statistics.TryGetValue(result, out int count) && count > 0 ? count : 0;

        private static double[] expectedWife3Points(double od, double windowScale)
        {
            string key = $"{od}|{windowScale}";
            if (expected_points_cache.TryGetValue(key, out double[]? cached))
                return cached;

            double[] edges =
            {
                stable_max_window_ms * windowScale,
                (stable_window_bases_ms[0] - od_window_step_ms * od) * windowScale,
                (stable_window_bases_ms[1] - od_window_step_ms * od) * windowScale,
                (stable_window_bases_ms[2] - od_window_step_ms * od) * windowScale,
                (stable_window_bases_ms[3] - od_window_step_ms * od) * windowScale,
            };

            double[] points = new[]
            {
                wife3BandAverage(0, edges[0]),
                wife3BandAverage(edges[0], edges[1]),
                wife3BandAverage(edges[1], edges[2]),
                wife3BandAverage(edges[2], edges[3]),
                wife3BandAverage(edges[3], edges[4]),
                wife3_miss_points,
            };

            expected_points_cache[key] = points;
            return points;
        }

        private static double wife3PointsAt(double ms)
        {
            if (ms <= wife3_full_points_ms)
                return 1;
            if (ms <= wife3_zero_ms)
                return erf((wife3_zero_ms - ms) / wife3_erf_dev_ms);
            if (ms >= wife3_miss_ms)
                return wife3_miss_points;

            return wife3_miss_points * (ms - wife3_zero_ms) / (wife3_miss_ms - wife3_zero_ms);
        }

        private static double wife3BandAverage(double fromMs, double toMs)
        {
            if (!(toMs > fromMs))
                return wife3PointsAt(toMs);

            const int steps = 512;
            double step = (toMs - fromMs) / steps;
            double sum = 0;

            for (int i = 0; i < steps; i++)
                sum += wife3PointsAt(fromMs + (i + 0.5) * step);

            return sum / steps;
        }

        // Abramowitz & Stegun 7.1.26 — see EzAbramowitzErf.
        private static double erf(double x) => EzAbramowitzErf.Erf(x);
    }
}
