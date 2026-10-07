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
        /// Miss 权重下限：Miss 全落在权重最低的区间时，罚分仍保留此比例。
        /// </summary>
        public const double MISS_WEIGHT_FLOOR = 0.2;

        /// <summary>
        /// 鼓励区：Miss 率不超过此值时，罚分不超过「接受偏差」下的判定加成。
        /// </summary>
        public const double MISS_RATE_ACCEPTED = 0.005;

        /// <summary>
        /// 不鼓励区：Miss 率达到此值时罚满 <see cref="MISS_PENALTY_CAP"/>（再乘 KPS 倍率）。
        /// </summary>
        public const double MISS_RATE_DISCOURAGED = 0.02;

        /// <summary>
        /// 接受偏差：在 <see cref="MISS_RATE_ACCEPTED"/> 处，罚分等于全谱按此误差命中可得的判定加成。
        /// </summary>
        public const double ACCEPTED_ERROR_MS = 11;

        /// <summary>
        /// Miss 罚分上限（绝对值）。
        /// </summary>
        public const int MISS_PENALTY_CAP = 20000;

        private static readonly double log_k_denominator = Math.Log(1 + OFFSET_LOG_K);

        public static double NormalisedKps(double kps)
            => Math.Clamp((kps - KPS_START) / (KPS_SATURATION - KPS_START), 0, 1);

        /// <summary>
        /// 判定用 KPS 权重 W(K)。正向：Smoothstep，15 以下为 0，40 以上为 1；颠倒：镜像，15 以下为 1，40 以上为 0。
        /// </summary>
        public static double JudgeWeight(double kps, bool favourHighKps = true)
        {
            double z = NormalisedKps(kps);
            double w = z * z * (3 - 2 * z);
            return favourHighKps ? w : 1 - w;
        }

        /// <summary>
        /// Miss 用 KPS 权重。正向：从 0 KPS 的 <see cref="MISS_WEIGHT_FLOOR"/> 线性升到 <see cref="KPS_SATURATION"/> 的 1；
        /// 颠倒：镜像，0 KPS 为 1，降到 <see cref="KPS_SATURATION"/> 及以上的 <see cref="MISS_WEIGHT_FLOOR"/>。任何区间的 Miss 都计罚分。
        /// </summary>
        public static double MissWeight(double kps, bool favourHighKps = true)
        {
            double t = Math.Clamp(kps / KPS_SATURATION, 0, 1);
            return MISS_WEIGHT_FLOOR + (1 - MISS_WEIGHT_FLOOR) * (favourHighKps ? t : 1 - t);
        }

        /// <summary>
        /// 鼓励点罚分下限：判定权重接近 0 时仍保证等比段有有限倍率。
        /// </summary>
        public const double MISS_ANCHOR_MIN = 200;

        /// <summary>
        /// 由 Miss 率得到罚分（≤ 0）：0 到鼓励点线性升到锚点（每个 Miss 等额、不叠加）；
        /// 鼓励点到不鼓励点按等比（对数尺度线性）升到上限；之后封顶。整条曲线再乘 Miss 所在区间的平均 KPS 权重。
        /// </summary>
        /// <param name="missRate">Miss 个数 / 谱面总 Note 数（普通 Note + LN）。</param>
        /// <param name="judgeCoverage">同一倾向下 Σ<see cref="JudgeWeight"/> / 计入 Note 数，用于计算鼓励点锚值。</param>
        /// <param name="missWeight">各 Miss 的 <see cref="MissWeight"/> 平均值，作为整体倍率。</param>
        public static int MissPenalty(double missRate, double judgeCoverage, double missWeight = 1)
            => -(int)Math.Round(missPenaltyMagnitude(missRate, judgeCoverage) * Math.Clamp(missWeight, 0, 1));

        private static double missPenaltyMagnitude(double missRate, double judgeCoverage)
        {
            double rate = Math.Max(0, missRate);
            double anchor = AcceptedMissPenalty(judgeCoverage);
            double penalty;

            if (rate <= MISS_RATE_ACCEPTED)
                penalty = anchor * rate / MISS_RATE_ACCEPTED;
            else if (rate >= MISS_RATE_DISCOURAGED)
                penalty = MISS_PENALTY_CAP;
            else
            {
                double x = (rate - MISS_RATE_ACCEPTED) / (MISS_RATE_DISCOURAGED - MISS_RATE_ACCEPTED);
                penalty = anchor * Math.Pow(MISS_PENALTY_CAP / anchor, x);
            }

            return penalty;
        }

        /// <summary>
        /// 鼓励点罚分（正值）：全谱按 <see cref="ACCEPTED_ERROR_MS"/> 命中时的判定加成，不低于 <see cref="MISS_ANCHOR_MIN"/>。
        /// </summary>
        public static double AcceptedMissPenalty(double judgeCoverage)
            => Math.Max(MISS_ANCHOR_MIN, JUDGE_BONUS_MAX * OffsetQuality(ACCEPTED_ERROR_MS) * Math.Clamp(judgeCoverage, 0, 1));

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
