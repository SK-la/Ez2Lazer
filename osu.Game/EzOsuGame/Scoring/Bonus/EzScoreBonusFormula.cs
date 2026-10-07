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
        public const double KPS_START = 5;
        public const double KPS_SATURATION = 40;

        public const double OFFSET_BOUNDARY_MS = 11; // 0 与归零点的中点。类对数缓降到这里，价值剩 0.5，之后急坠。
        public const double OFFSET_BOUNDARY_VALUE = 0.5;
        public const double OFFSET_LOG_K = 9;

        // 急坠段的衰减量与原先 3ms、λ=1.2 相同，只是摊到中点至归零点的整段上。
        public const double OFFSET_DECAY_LAMBDA = 3.6 / (OFFSET_ZERO_MS - OFFSET_BOUNDARY_MS);
        public const double OFFSET_ZERO_MS = 22; // 到这里判定加成变成 0，再大也不再算。

        /// <summary>
        /// 判定加成上限。25% 的 Note 在 6ms 内且权重为 1、其余无价值时约 +2 万；0ms 且权重为 1 时为 +9 万。
        /// </summary>
        public const int JUDGE_BONUS_MAX = 90000;

        /// <summary>
        /// Miss 权重下限：Miss 全落在权重最低的区间时，罚分仍保留此比例。
        /// </summary>
        public const double MISS_WEIGHT_FLOOR = 0.3;

        /// <summary>
        /// 鼓励区：Miss 率不超过此值时，罚分不超过「接受偏差」下的判定加成。
        /// </summary>
        public const double MISS_RATE_ACCEPTED = 0.005;

        /// <summary>
        /// 不鼓励区：Miss 率达到此值时罚满 <see cref="MissPenaltyCap"/>，不再乘 KPS 倍率。
        /// </summary>
        public const double MISS_RATE_DISCOURAGED = 0.03;

        /// <summary>
        /// 不鼓励区罚满金额 = 该比例 × 整谱打在 <see cref="OFFSET_BOUNDARY_MS"/> 的判定加成。8/3 时，权重为 1 的封顶为 12 万。
        /// </summary>
        public const double MISS_CAP_RATIO = 8.0 / 3.0;

        private static readonly double log_k_denominator = Math.Log(1 + OFFSET_LOG_K);

        public static double NormalisedKps(double kps)
            => Math.Clamp((kps - KPS_START) / (KPS_SATURATION - KPS_START), 0, 1);

        /// <summary>
        /// 判定用 KPS 权重 W(K)。正向：Smoothstep，<see cref="KPS_START"/> 以下为 0，<see cref="KPS_SATURATION"/> 以上为 1；颠倒后镜像。
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
        /// 鼓励点到不鼓励点按等比（对数尺度线性）升到上限；之后封顶。
        /// KPS 倍率只乘在鼓励区内，并随 Miss 率向不鼓励点收拢到 1。
        /// </summary>
        /// <param name="missRate">Miss 个数 / 谱面总 Note 数（普通 Note 各 1，LN 的头和尾各 1）。</param>
        /// <param name="judgeCoverage">同一倾向下 Σ<see cref="JudgeWeight"/> / 计入 Note 数，用于计算鼓励点锚值。</param>
        /// <param name="missWeight">各 Miss 的 <see cref="MissWeight"/> 平均值。鼓励区内作为整体倍率。</param>
        public static int MissPenalty(double missRate, double judgeCoverage, double missWeight = 1)
        {
            double rate = Math.Max(0, missRate);
            return -(int)Math.Round(missPenaltyMagnitude(rate, judgeCoverage) * effectiveMissWeight(rate, missWeight));
        }

        /// <summary>
        /// 鼓励区内用 Miss 的 KPS 权重；到不鼓励点收拢为 1，差成绩不再因落在低权重区间而少罚。
        /// </summary>
        private static double effectiveMissWeight(double rate, double missWeight)
        {
            double weight = Math.Clamp(missWeight, 0, 1);

            if (rate <= MISS_RATE_ACCEPTED)
                return weight;

            if (rate >= MISS_RATE_DISCOURAGED)
                return 1;

            double fade = (rate - MISS_RATE_ACCEPTED) / (MISS_RATE_DISCOURAGED - MISS_RATE_ACCEPTED);
            return weight + (1 - weight) * fade;
        }

        private static double missPenaltyMagnitude(double missRate, double judgeCoverage)
        {
            double rate = Math.Max(0, missRate);
            double anchor = AcceptedMissPenalty(judgeCoverage);
            double penalty;

            if (rate <= MISS_RATE_ACCEPTED)
                penalty = anchor * rate / MISS_RATE_ACCEPTED;
            else if (rate >= MISS_RATE_DISCOURAGED)
                penalty = MissPenaltyCap(judgeCoverage);
            else
            {
                double x = (rate - MISS_RATE_ACCEPTED) / (MISS_RATE_DISCOURAGED - MISS_RATE_ACCEPTED);
                penalty = anchor * Math.Pow(MissPenaltyCap(judgeCoverage) / anchor, x);
            }

            return penalty;
        }

        /// <summary>
        /// 鼓励点罚分（正值）：全谱按 <see cref="OFFSET_BOUNDARY_MS"/> 命中时的判定加成，不低于 <see cref="MISS_ANCHOR_MIN"/>。
        /// </summary>
        public static double AcceptedMissPenalty(double judgeCoverage)
            => Math.Max(MISS_ANCHOR_MIN, JUDGE_BONUS_MAX * OffsetQuality(OFFSET_BOUNDARY_MS) * Math.Clamp(judgeCoverage, 0, 1));

        /// <summary>
        /// 不鼓励点罚满金额：<see cref="MISS_CAP_RATIO"/> × <see cref="AcceptedMissPenalty"/>。
        /// </summary>
        public static double MissPenaltyCap(double judgeCoverage)
            => MISS_CAP_RATIO * AcceptedMissPenalty(judgeCoverage);

        /// <summary>
        /// Offset 精度价值 F(E)，E 为绝对误差（ms）。F(0)=1，F(中点)=0.5，E ≥ <see cref="OFFSET_ZERO_MS"/> 时为 0。
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
