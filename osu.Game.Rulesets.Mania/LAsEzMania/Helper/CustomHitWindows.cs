// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.LAsEzExtensions.Background;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.LAsEZMania.Helper
{
    public class CustomHitWindowsHelper
    {
        private static readonly double[,] hit_range_bms =
        {
            //  305   300    200     100     50     Miss    Poor
            { 16.67, 33.33, 116.67, 250, 250, 250, 500 }, // IIDX
            { 15.00, 30.00, 060.00, 200, 200, 1000, 1000 }, // LR2 Hard
            { 15.00, 45.00, 112.00, 165, 165, 500, 500 }, // raja normal (75%)
            { 20.00, 60.00, 150.00, 220, 500, 500, 500 }, // raja easy (100%)
        };

        private EzMUGHitMode hitMode = EzMUGHitMode.Classic;

        public EzMUGHitMode HitMode
        {
            get => hitMode;
            set
            {
                hitMode = value;
                updateRanges();
            }
        }

        private double totalMultiplier = 1.0;

        public double TotalMultiplier
        {
            get => totalMultiplier;
            set
            {
                totalMultiplier = value;
                updateRanges();
            }
        }

        private double overallDifficulty = 1.0;

        public double OverallDifficulty
        {
            get => overallDifficulty;
            set
            {
                overallDifficulty = value;
                updateRanges();
            }
        }

        private double bpm;

        public double BPM
        {
            get => bpm;
            set
            {
                bpm = value;
                updateRanges();
            }
        }

        // Ranges compatible with Mania naming used elsewhere (Range305 == Perfect, Range300 == Great, ...)
        public double Range305 { get; private set; }
        public double Range300 { get; private set; }
        public double Range200 { get; private set; }
        public double Range100 { get; private set; }
        public double Range050 { get; private set; }
        public double Range000 { get; private set; }
        public double PoolRange { get; private set; }

        private static readonly DifficultyRange perfect_window_range = new DifficultyRange(22.4D, 19.4D, 13.9D);
        private static readonly DifficultyRange great_window_range = new DifficultyRange(64, 49, 34);
        private static readonly DifficultyRange good_window_range = new DifficultyRange(97, 82, 67);
        private static readonly DifficultyRange ok_window_range = new DifficultyRange(127, 112, 97);
        private static readonly DifficultyRange meh_window_range = new DifficultyRange(151, 136, 121);
        private static readonly DifficultyRange miss_window_range = new DifficultyRange(188, 173, 158);
        private const double pool_offset = 150.0;

        public CustomHitWindowsHelper()
            : this(GlobalConfigStore.EzConfig?.Get<EzMUGHitMode>(Ez2Setting.HitMode) ?? EzMUGHitMode.Classic)
        {
        }

        public CustomHitWindowsHelper(EzMUGHitMode hitMode)
        {
            HitMode = hitMode;
            // UpdateRanges is called by the property setter
        }

        public double[] GetHitWindowsClassic()
        {
            double invertedOd = Math.Clamp(10 - OverallDifficulty, 0, 10);
            Range305 = Math.Floor(16 * TotalMultiplier) + 0.5;
            Range300 = Math.Floor((34 + 3 * invertedOd) * TotalMultiplier) + 0.5;
            Range200 = Math.Floor((67 + 3 * invertedOd) * TotalMultiplier) + 0.5;
            Range100 = Math.Floor((97 + 3 * invertedOd) * TotalMultiplier) + 0.5;
            Range050 = Math.Floor((121 + 3 * invertedOd) * TotalMultiplier) + 0.5;
            Range000 = Math.Floor((158 + 3 * invertedOd) * TotalMultiplier) + 0.5;
            PoolRange = Range000 + pool_offset;

            return new[] { Range305, Range300, Range200, Range100, Range050, Range000, PoolRange };
        }

        public double[] GetHitWindowsO2Jam(double setBpm)
        {
            bpm = setBpm;
            Range305 = 7500.0 / bpm * TotalMultiplier;
            Range300 = Range305;
            Range200 = 22500.0 / bpm * TotalMultiplier;
            Range100 = Range200;
            Range050 = 31250.0 / bpm * TotalMultiplier;
            Range000 = Range050;
            PoolRange = Range000 + pool_offset;

            return new[] { Range305, Range300, Range200, Range100, Range050, Range000, PoolRange };
        }

        public double[] GetHitWindowsEZ2AC()
        {
            Range305 = 18.0 * TotalMultiplier;
            Range300 = 38.0 * TotalMultiplier;
            Range200 = 68.0 * TotalMultiplier;
            Range100 = 88.0 * TotalMultiplier;
            Range050 = 88.0 * TotalMultiplier;
            Range000 = 100.0 * TotalMultiplier;
            PoolRange = pool_offset;

            return new[] { Range305, Range300, Range200, Range100, Range050, Range000, PoolRange };
        }

        public double[] GetHitWindowsIIDX(EzMUGHitMode mode)
        {
            int row = 0;

            switch (mode)
            {
                case EzMUGHitMode.IIDX_HD:
                    row = 0;
                    break;

                case EzMUGHitMode.LR2_HD:
                    row = 1;
                    break;

                case EzMUGHitMode.Raja_NM:
                    row = 2;
                    break;
            }

            Range305 = hit_range_bms[row, 0] * TotalMultiplier;
            Range300 = hit_range_bms[row, 1] * TotalMultiplier;
            Range200 = hit_range_bms[row, 2] * TotalMultiplier;
            Range100 = hit_range_bms[row, 3] * TotalMultiplier;
            Range050 = hit_range_bms[row, 4] * TotalMultiplier;
            Range000 = hit_range_bms[row, 5] * TotalMultiplier;
            PoolRange = hit_range_bms[row, 6] * TotalMultiplier;

            return new[] { Range305, Range300, Range200, Range100, Range050, Range000, PoolRange };
        }

        public double[] GetHitWindowsMelody()
        {
            Range305 = 20.0 * TotalMultiplier;
            Range300 = 40.0 * TotalMultiplier;
            Range200 = 60.0 * TotalMultiplier;
            Range100 = 80.0 * TotalMultiplier;
            Range050 = 100.0 * TotalMultiplier;
            Range000 = 120.0 * TotalMultiplier;
            PoolRange = Range000 + pool_offset;

            return new[] { Range305, Range300, Range200, Range100, Range050, Range000, PoolRange };
        }

        private void updateRanges()
        {
            switch (HitMode)
            {
                case EzMUGHitMode.O2Jam:
                    SetRanges(GetHitWindowsO2Jam(bpm));
                    break;

                case EzMUGHitMode.Lazer:
                    double perfect = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, perfect_window_range) * TotalMultiplier) + 0.5;
                    double great = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, great_window_range) * TotalMultiplier) + 0.5;
                    double good = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, good_window_range) * TotalMultiplier) + 0.5;
                    double ok = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, ok_window_range) * TotalMultiplier) + 0.5;
                    double meh = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, meh_window_range) * TotalMultiplier) + 0.5;
                    double miss = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, miss_window_range) * TotalMultiplier) + 0.5;
                    double pool = miss + pool_offset;
                    SetRanges(new[] { perfect, great, good, ok, meh, miss, pool });
                    break;

                case EzMUGHitMode.EZ2AC:
                    SetRanges(GetHitWindowsEZ2AC());
                    break;

                case EzMUGHitMode.IIDX_HD:
                    SetRanges(GetHitWindowsIIDX(0));
                    break;

                case EzMUGHitMode.Malody:
                    SetRanges(GetHitWindowsMelody());
                    break;

                default:
                    SetRanges(GetHitWindowsClassic());
                    break;
            }
        }

        public HitResult ResultFor(double timeOffset)
        {
            timeOffset = Math.Abs(timeOffset);

            if (AllowPoolEnabled)
            {
                if (IsHitResultAllowed(HitResult.Pool))
                {
                    double miss = WindowFor(HitResult.Miss);
                    double poolEarlyWindow = miss + 50;
                    double poolLateWindow = miss + 50;
                    if (timeOffset > -poolEarlyWindow &&
                        timeOffset < -miss ||
                        timeOffset < poolLateWindow &&
                        timeOffset > miss)
                        return HitResult.Pool;
                }
            }

            for (var result = HitResult.Perfect; result >= HitResult.Miss; --result)
            {
                if (IsHitResultAllowed(result) && timeOffset <= WindowFor(result))
                    return result;
            }

            return HitResult.None;
        }

        public virtual bool AllowPoolEnabled => GlobalConfigStore.EzConfig?.Get<bool>(Ez2Setting.CustomPoorHitResultBool) ?? false;

        public virtual bool IsHitResultAllowed(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                case HitResult.Great:
                case HitResult.Good:
                case HitResult.Ok:
                case HitResult.Meh:
                case HitResult.Miss:
                    return true;

                case HitResult.Pool:
                    return AllowPoolEnabled;

                default:
                    return false;
            }
        }

        public double WindowFor(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect: return Range305;

                case HitResult.Great: return Range300;

                case HitResult.Good: return Range200;

                case HitResult.Ok: return Range100;

                case HitResult.Meh: return Range050;

                case HitResult.Pool: return PoolRange;

                case HitResult.Miss: return Range000;

                default: throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }

        /// <summary>
        /// Allow external code to replace the current windows (e.g. when switching hit modes).
        /// </summary>
        public void SetRanges(double[]? ranges)
        {
            if (ranges == null) return;

            if (ranges.Length >= 6)
            {
                Range305 = ranges[0];
                Range300 = ranges[1];
                Range200 = ranges[2];
                Range100 = ranges[3];
                Range050 = ranges[4];
                Range000 = ranges[5];
            }

            if (ranges.Length >= 7)
                PoolRange = ranges[6];
            else
                PoolRange = Range000 + pool_offset;
        }

        /// <summary>
        /// Compute LN (long note) tail score given head and tail offsets using this helper's ranges.
        /// </summary>
        public double GetLNScore(double head, double tail)
        {
            // This LN scoring method is Classic-specific: always use Classic hit windows
            double[] classicRanges = GetHitWindowsClassic();

            double r305 = classicRanges[0];
            double r300 = classicRanges[1];
            double r200 = classicRanges[2];
            double r100 = classicRanges[3];
            double r050 = classicRanges[4];

            double combined = head + tail;

            (double range, double headFactor, double combinedFactor, double score)[] rules = new[]
            {
                (range: r305, headFactor: 1.2, combinedFactor: 2.4, score: 300.0),
                (range: r300, headFactor: 1.1, combinedFactor: 2.2, score: 300),
                (range: r200, headFactor: 1.0, combinedFactor: 2.0, score: 200),
                (range: r100, headFactor: 1.0, combinedFactor: 2.0, score: 100),
                (range: r050, headFactor: 1.0, combinedFactor: 2.0, score: 50),
            };

            foreach (var (range, headFactor, combinedFactor, score) in rules)
            {
                if (head < range * headFactor && combined < range * combinedFactor)
                    return score;
            }

            return 0;
        }
    }
}
