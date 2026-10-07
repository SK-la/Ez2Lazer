// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Scoring.Bonus
{
    /// <summary>
    /// Ez 附加分 V1 公式（docs/mania_scoring_formula_research_v1.md）。纯函数，输入均为真实时间（已按 rate 换算）。
    /// </summary>
    public static class EzScoreBonusFormula
    {
        public const double KPS_START = 15;
        public const double KPS_SATURATION = 40;

        public const double OFFSET_BOUNDARY_MS = 13;
        public const double OFFSET_BOUNDARY_VALUE = 0.5;
        public const double OFFSET_LOG_K = 9;
        public const double OFFSET_DECAY_LAMBDA = 1.2;
        public const double OFFSET_ZERO_MS = 16;

        /// <summary>
        /// 判定加成上限（严格小于 1 万，保持在千位量级）。
        /// </summary>
        public const int JUDGE_BONUS_MAX = 9999;

        /// <summary>
        /// 每次 Miss 的额外罚分（与所在区间 KPS 无关）。
        /// </summary>
        public const int MISS_PENALTY_UNIT = 500;

        /// <summary>
        /// Miss 罚分总量上限（绝对值）。
        /// </summary>
        public const int MISS_PENALTY_CAP = 20000;

        private static readonly double log_k_denominator = Math.Log(1 + OFFSET_LOG_K);

        public static double NormalisedKps(double kps)
            => Math.Clamp((kps - KPS_START) / (KPS_SATURATION - KPS_START), 0, 1);

        /// <summary>
        /// 判定用 KPS 权重 W(K)：Smoothstep，15 以下为 0，40 以上为 1。
        /// </summary>
        public static double JudgeWeight(double kps)
        {
            double z = NormalisedKps(kps);
            return z * z * (3 - 2 * z);
        }

        public static int MissPenalty(int missCount)
            => -Math.Min(MISS_PENALTY_CAP, MISS_PENALTY_UNIT * Math.Max(0, missCount));

        /// <summary>
        /// Offset 精度价值 F(E)，E 为绝对误差（ms）。F(0)=1，F(b)=c，E ≥ 16ms 时为 0。
        /// </summary>
        public static double OffsetQuality(double absoluteErrorMs)
        {
            double e = Math.Abs(absoluteErrorMs);

            if (double.IsNaN(e) || e >= OFFSET_ZERO_MS)
                return 0;

            if (e <= OFFSET_BOUNDARY_MS)
            {
                double inner = 1 + OFFSET_LOG_K * (1 - e / OFFSET_BOUNDARY_MS);
                return OFFSET_BOUNDARY_VALUE + (1 - OFFSET_BOUNDARY_VALUE) * Math.Log(inner) / log_k_denominator;
            }

            return OFFSET_BOUNDARY_VALUE * Math.Exp(-OFFSET_DECAY_LAMBDA * (e - OFFSET_BOUNDARY_MS));
        }
    }
}
