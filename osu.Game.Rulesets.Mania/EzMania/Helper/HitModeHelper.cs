// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.Helper
{
    // NOTE: 当前模式数量很少，优先保持 switch/数组索引与低分配路径，收益通常高于引入 FrozenDictionary。
    public class HitModeHelper
    {
        private static readonly double[,] hit_range_bms =
        {
            // Kool   Cool   Good      -   Bad   Poor  []Poor
            //  305    300     200     100   50  Miss  Poor
            { 16.67, 33.33, 116.67, 116.67, 250, 250, 500 }, // IIDX
            { 15.00, 30.00, 060.00, 060.00, 200, 1000, 1000 }, // LR2 Hard, TODO:此处poor范围过大，前后LN首尾间隙可能被覆盖，导致下一个note提前被结束。
            { 15.00, 45.00, 112.00, 112.00, 165, 500, 500 }, // raja normal (75%)
            { 20.00, 60.00, 150.00, 150.00, 500, 500, 500 }, // raja easy (100%)
        };

        private EzEnumHitMode hitMode = EzEnumHitMode.Classic;

        public EzEnumHitMode HitMode
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
        public double PoorRange { get; private set; }

        private static readonly DifficultyRange perfect_window_range = new DifficultyRange(22.4D, 19.4D, 13.9D);
        private static readonly DifficultyRange great_window_range = new DifficultyRange(64, 49, 34);
        private static readonly DifficultyRange good_window_range = new DifficultyRange(97, 82, 67);
        private static readonly DifficultyRange ok_window_range = new DifficultyRange(127, 112, 97);
        private static readonly DifficultyRange meh_window_range = new DifficultyRange(151, 136, 121);
        private static readonly DifficultyRange miss_window_range = new DifficultyRange(188, 173, 158);
        private const double poor_offset = 150.0;

        public HitModeHelper()
            : this(GlobalConfigStore.EzConfig.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode))
        {
        }

        public HitModeHelper(EzEnumHitMode hitMode)
        {
            HitMode = hitMode;
        }

        public double[] GetHitRangeList => new[] { Range305, Range300, Range200, Range100, Range050, Range000, PoorRange };

        public double[] GetHitWindowsClassic()
        {
            double invertedOd = Math.Clamp(10 - OverallDifficulty, 0, 10);
            Range305 = Math.Floor(16 * TotalMultiplier) + 0.5;
            Range300 = Math.Floor((34 + 3 * invertedOd) * TotalMultiplier) + 0.5;
            Range200 = Math.Floor((67 + 3 * invertedOd) * TotalMultiplier) + 0.5;
            Range100 = Math.Floor((97 + 3 * invertedOd) * TotalMultiplier) + 0.5;
            Range050 = Math.Floor((121 + 3 * invertedOd) * TotalMultiplier) + 0.5;
            Range000 = Math.Floor((158 + 3 * invertedOd) * TotalMultiplier) + 0.5;

            return new[] { Range305, Range300, Range200, Range100, Range050, Range000, Range000 };
        }

        private void updateRanges()
        {
            switch (hitMode)
            {
                case EzEnumHitMode.Lazer:
                    double perfect = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, perfect_window_range) * TotalMultiplier) + 0.5;
                    double great = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, great_window_range) * TotalMultiplier) + 0.5;
                    double good = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, good_window_range) * TotalMultiplier) + 0.5;
                    double ok = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, ok_window_range) * TotalMultiplier) + 0.5;
                    double meh = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, meh_window_range) * TotalMultiplier) + 0.5;
                    double miss = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, miss_window_range) * TotalMultiplier) + 0.5;
                    double poor = miss;
                    setRanges(perfect, great, good, ok, meh, miss, poor);
                    break;

                case EzEnumHitMode.O2Jam:
                    Range305 = 7500.0 / bpm * TotalMultiplier;
                    Range300 = Range305;
                    Range200 = 22500.0 / bpm * TotalMultiplier;
                    Range100 = Range200;
                    Range050 = 31250.0 / bpm * TotalMultiplier;
                    Range000 = Range050;
                    PoorRange = Range000 + poor_offset;
                    break;

                case EzEnumHitMode.EZ2AC:
                    Range305 = 16.67 * TotalMultiplier;
                    Range300 = 33.33 * TotalMultiplier;
                    Range200 = 83.33 * TotalMultiplier;
                    Range100 = 83.33 * TotalMultiplier;
                    Range050 = 100.0 * TotalMultiplier;
                    Range000 = 116.67 * TotalMultiplier;
                    PoorRange = Range000 + poor_offset;
                    break;

                case EzEnumHitMode.IIDX_HD:
                case EzEnumHitMode.LR2_HD:
                case EzEnumHitMode.Raja_NM:
                    int row = hitMode == EzEnumHitMode.LR2_HD ? 1
                        : hitMode == EzEnumHitMode.Raja_NM ? 2
                        : 0;

                    Range305 = hit_range_bms[row, 0] * TotalMultiplier;
                    Range300 = hit_range_bms[row, 1] * TotalMultiplier;
                    Range200 = hit_range_bms[row, 2] * TotalMultiplier;
                    Range100 = hit_range_bms[row, 3] * TotalMultiplier;
                    Range050 = hit_range_bms[row, 4] * TotalMultiplier;
                    Range000 = hit_range_bms[row, 5];
                    PoorRange = hit_range_bms[row, 6];
                    break;

                case EzEnumHitMode.Malody_E:
                    Range305 = 44.0 * TotalMultiplier;
                    Range300 = 84.0 * TotalMultiplier;
                    Range200 = 118.0 * TotalMultiplier;
                    Range100 = 118.0 * TotalMultiplier;
                    Range050 = 118.0 * TotalMultiplier;
                    Range000 = 150.0 * TotalMultiplier;
                    PoorRange = Range000 + poor_offset;
                    break;

                case EzEnumHitMode.Malody_B:
                    Range305 = 20.0 * TotalMultiplier;
                    Range300 = 60.0 * TotalMultiplier;
                    Range200 = 94.0 * TotalMultiplier;
                    Range100 = 94.0 * TotalMultiplier;
                    Range050 = 94.0 * TotalMultiplier;
                    Range000 = 150.0 * TotalMultiplier;
                    PoorRange = Range000 + poor_offset;
                    break;

                default:
                    double invertedOd = Math.Clamp(10 - OverallDifficulty, 0, 10);
                    Range305 = Math.Floor(16 * TotalMultiplier) + 0.5;
                    Range300 = Math.Floor((34 + 3 * invertedOd) * TotalMultiplier) + 0.5;
                    Range200 = Math.Floor((67 + 3 * invertedOd) * TotalMultiplier) + 0.5;
                    Range100 = Math.Floor((97 + 3 * invertedOd) * TotalMultiplier) + 0.5;
                    Range050 = Math.Floor((121 + 3 * invertedOd) * TotalMultiplier) + 0.5;
                    Range000 = Math.Floor((158 + 3 * invertedOd) * TotalMultiplier) + 0.5;
                    PoorRange = Range000;
                    break;
            }
        }

        public HitResult ResultFor(double timeOffset)
        {
            timeOffset = Math.Abs(timeOffset);

            for (var result = HitResult.Perfect; result >= HitResult.Poor; --result)
            {
                if (IsHitResultAllowed(result) && timeOffset <= WindowFor(result))
                    return result;
            }

            return HitResult.None;
        }

        public virtual bool AllowPoorEnabled => GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.BmsPoorHitResultEnable);

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

                case HitResult.Poor:
                    return AllowPoorEnabled;

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

                case HitResult.Poor: return PoorRange;

                case HitResult.Miss: return Range000;

                default: throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }

        /// <summary>
        /// 计划上是用于从外部Mod设置传入自定义值的，但目前Mod走另一套接口。
        /// </summary>
        public void SetRanges(double[]? ranges)
        {
            if (ranges == null) return;

            if (ranges.Length >= 7)
            {
                Range305 = ranges[0];
                Range300 = ranges[1];
                Range200 = ranges[2];
                Range100 = ranges[3];
                Range050 = ranges[4];
                Range000 = ranges[5];
                PoorRange = ranges[6];
            }
        }

        private void setRanges(double range305, double range300, double range200, double range100, double range050, double range000, double poorRange)
        {
            Range305 = range305;
            Range300 = range300;
            Range200 = range200;
            Range100 = range100;
            Range050 = range050;
            Range000 = range000;
            PoorRange = poorRange;
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
