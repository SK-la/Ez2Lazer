// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Scoring;

// ReSharper disable InconsistentNaming

namespace osu.Game.Rulesets.Mania.EzMania.Helper
{
    /// <summary>
    /// BMS系列 数据统一使用 EX Score, 见: <see href="https://iidx.org/compendium/exscore"/>
    /// <para></para>
    /// EZ2AC 数据来自: <see href="https://namu.wiki/w/EZ2AC%20%EC%8B%9C%EB%A6%AC%EC%A6%88/%ED%8C%90%EC%A0%95%EA%B3%BC%20%EC%A0%90%EC%88%98%EC%B2%B4%EA%B3%84"/>
    /// <para></para>
    /// Malody 数据来自:  <see href="https://mzh.moegirl.org.cn/Malody#.E5.88.86.E6.95.B0"/>
    /// <para></para>
    /// O2Jam 由于机制过于复杂，这里简化忽略加成，只考虑50%比例；原始算法参考: <see href="https://games.sina.com.cn/o/z/jyt/2005-01-11/197476.shtml"/>/// </summary>
    public partial class HitModeHelper
    {
        private static readonly double[,] hit_range_bms =
        {
            //  305  300    200     100     50e  Miss  Poor
            // Kool  Cool   Good    -       Bad  Poor  KPoor
            { 16.67, 33.33, 116.67, 116.67, 250, 300,  300 }, // IIDX
            { 15.00, 30.00, 060.00, 060.00, 200, 300,  300 }, // LR2 Hard, TODO:此处poor范围过大，前后LN首尾间隙可能被覆盖，导致下一个note提前被结束。
            { 15.00, 45.00, 112.00, 112.00, 165, 300,  300 }, // raja normal (75%)
            { 20.00, 60.00, 150.00, 150.00, 220, 300,  300 }, // raja easy (100%)
        };

        private static readonly DifficultyRange perfect_window_range = new DifficultyRange(22.4D, 19.4D, 13.9D);
        private static readonly DifficultyRange great_window_range = new DifficultyRange(64, 49, 34);
        private static readonly DifficultyRange good_window_range = new DifficultyRange(97, 82, 67);
        private static readonly DifficultyRange ok_window_range = new DifficultyRange(127, 112, 97);
        private static readonly DifficultyRange meh_window_range = new DifficultyRange(151, 136, 121);
        private static readonly DifficultyRange miss_window_range = new DifficultyRange(188, 173, 158);

        public double Range305 { get; private set; }
        public double Range300 { get; private set; }
        public double Range200 { get; private set; }
        public double Range100 { get; private set; }
        public double Range050 { get; private set; }
        public double Range000 { get; private set; }
        public double RangePoor { get; private set; }

        public (double early, double late) RangeBD { get; private set; }
        public (double early, double late) RangePR { get; private set; }
        public (double early, double late) RangeKPR { get; private set; }

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

        // 改成调用独立更新函数
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

        public HitModeHelper()
            : this(GlobalConfigStore.EzConfig.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode))
        {
        }

        public HitModeHelper(EzEnumHitMode hitMode)
        {
            HitMode = hitMode;
        }

        public double[] GetHitRangeList => new[] { Range305, Range300, Range200, Range100, Range050, Range000, RangePoor };

        private void updateRanges()
        {
            switch (hitMode)
            {
                case EzEnumHitMode.O2Jam:
                    Range305 = 7500.0 / bpm * TotalMultiplier;
                    Range300 = Range305;
                    Range200 = 22500.0 / bpm * TotalMultiplier;
                    Range100 = Range200;
                    Range050 = 31250.0 / bpm * TotalMultiplier;
                    Range000 = Range050;
                    break;

                case EzEnumHitMode.EZ2AC:
                    Range305 = 16.67 * TotalMultiplier;
                    Range300 = 33.33 * TotalMultiplier;
                    Range200 = 83.33 * TotalMultiplier;
                    Range100 = 83.33 * TotalMultiplier;
                    Range050 = 100.0 * TotalMultiplier;
                    Range000 = 116.67 * TotalMultiplier;
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
                    RangePoor = hit_range_bms[row, 6];
                    break;

                case EzEnumHitMode.Malody_E:
                    Range305 = 20.0 * TotalMultiplier;
                    Range300 = 60.0 * TotalMultiplier;
                    Range200 = 94.0 * TotalMultiplier;
                    Range100 = 94.0 * TotalMultiplier;
                    Range050 = 94.0 * TotalMultiplier;
                    Range000 = 150.0 * TotalMultiplier;
                    break;

                case EzEnumHitMode.Malody_B:
                    Range305 = 44.0 * TotalMultiplier;
                    Range300 = 84.0 * TotalMultiplier;
                    Range200 = 118.0 * TotalMultiplier;
                    Range100 = 118.0 * TotalMultiplier;
                    Range050 = 118.0 * TotalMultiplier;
                    Range000 = 150.0 * TotalMultiplier;
                    break;

                case EzEnumHitMode.Classic:
                    double invertedOd = Math.Clamp(10 - OverallDifficulty, 0, 10);
                    Range305 = Math.Floor(16 * TotalMultiplier) + 0.5;
                    Range300 = Math.Floor((34 + 3 * invertedOd) * TotalMultiplier) + 0.5;
                    Range200 = Math.Floor((67 + 3 * invertedOd) * TotalMultiplier) + 0.5;
                    Range100 = Math.Floor((97 + 3 * invertedOd) * TotalMultiplier) + 0.5;
                    Range050 = Math.Floor((121 + 3 * invertedOd) * TotalMultiplier) + 0.5;
                    Range000 = Math.Floor((158 + 3 * invertedOd) * TotalMultiplier) + 0.5;
                    break;

                case EzEnumHitMode.Lazer:
                    Range305 = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, perfect_window_range) * TotalMultiplier) + 0.5;
                    Range300 = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, great_window_range) * TotalMultiplier) + 0.5;
                    Range200 = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, good_window_range) * TotalMultiplier) + 0.5;
                    Range100 = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, ok_window_range) * TotalMultiplier) + 0.5;
                    Range050 = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, meh_window_range) * TotalMultiplier) + 0.5;
                    Range000 = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(OverallDifficulty, miss_window_range) * TotalMultiplier) + 0.5;

                    break;
            }
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

        public HitResult ResultFor(double timeOffset)
        {
            double absOffset = Math.Abs(timeOffset);
            if (absOffset <= Range305) return HitResult.Perfect;
            if (absOffset <= Range300) return HitResult.Great;
            if (absOffset <= Range200) return HitResult.Good;
            if (absOffset <= Range100) return HitResult.Ok;
            if (absOffset <= Range050) return HitResult.Meh;
            if (absOffset <= Range000) return HitResult.Miss;
            if (absOffset <= RangePoor) return HitResult.Poor;

            return HitResult.None;
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

                case HitResult.Poor: return RangePoor;

                case HitResult.Miss: return Range000;

                default: throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }

#region 分数

        /// <summary>
        /// Compute LN (long note) tail score given head and tail offsets using this helper's ranges.
        /// </summary>
        public double GetClassicLNScore(double head, double tail)
        {
            double invertedOd = Math.Clamp(10 - OverallDifficulty, 0, 10);
            double r305 = Math.Floor(16 * TotalMultiplier) + 0.5;
            double r300 = Math.Floor((34 + 3 * invertedOd) * TotalMultiplier) + 0.5;
            double r200 = Math.Floor((67 + 3 * invertedOd) * TotalMultiplier) + 0.5;
            double r100 = Math.Floor((97 + 3 * invertedOd) * TotalMultiplier) + 0.5;
            double r050 = Math.Floor((121 + 3 * invertedOd) * TotalMultiplier) + 0.5;

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

        private const int score_base = 300;

        /// <summary>
        /// 根据判定模式获取基础分数
        /// </summary>
        public static int GetBaseScoreForResult(EzEnumHitMode hitMode, HitResult result)
        {
            switch (hitMode)
            {
                case EzEnumHitMode.Classic:
                    return getClassicBaseScore(result);

                case EzEnumHitMode.EZ2AC:
                    return getEZ2ACBaseScore(result);

                case EzEnumHitMode.O2Jam:
                    return getO2JamBaseScore(result);

                case EzEnumHitMode.IIDX_HD:
                case EzEnumHitMode.LR2_HD:
                case EzEnumHitMode.Raja_NM:
                    return getExScore(result);

                case EzEnumHitMode.Malody_E:
                    return getMalodyBaseScore(result, 1.2);

                case EzEnumHitMode.Malody_B:
                    return getMalodyBaseScore(result, 0.85);

                default:
                    return 0;
            }
        }

        // Stable经典模式
        private static int getClassicBaseScore(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                case HitResult.Great:
                    return score_base;

                case HitResult.Good:
                    return 200;

                case HitResult.Ok:
                    return 100;

                case HitResult.Meh:
                    return 50;

                default:
                    return 0;
            }
        }

        private static int getEZ2ACBaseScore(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return 300;  // Kool

                case HitResult.Great:
                    return 150;  // Cool

                case HitResult.Good:
                    return 41;  // Good

                default:
                    return 0;
            }
        }

        private static int getO2JamBaseScore(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return score_base;  // Cool

                case HitResult.Good:
                    return (int)(score_base * 0.5);  // Good

                default:
                    return 0;
            }
        }

        private static int getExScore(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return score_base;

                case HitResult.Great:
                    return (int)(score_base * 0.5);

                default:
                    return 0;
            }
        }

        private static int getMalodyBaseScore(HitResult result, double scoreMultiplier = 1.0)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return (int)(score_base * scoreMultiplier);  // Best

                case HitResult.Great:
                    return (int)(score_base * scoreMultiplier * 0.75);  // Cool

                case HitResult.Good:
                    return (int)(score_base * scoreMultiplier * 0.4);  // Good

                default:
                    return 0;
            }
        }

#endregion

#region 公共静态工具

        public static bool IsBMSHitMode(EzEnumHitMode hitMode)
        {
            return hitMode == EzEnumHitMode.IIDX_HD ||
                   hitMode == EzEnumHitMode.LR2_HD ||
                   hitMode == EzEnumHitMode.Raja_NM;
        }

#endregion
    }
}
