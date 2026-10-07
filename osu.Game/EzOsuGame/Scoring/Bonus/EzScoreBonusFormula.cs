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

        public const double OFFSET_FULL_MS = 6;
        public const double OFFSET_CROSS_MS = 44;

        /// <summary>
        /// 谱面没有判定窗口时的 Miss 边界。有窗口时用该模式 Miss 区间，不用这个常数。
        /// </summary>
        public const double OFFSET_MISS_MS = 200;

        // (44-16)/(44-6) 的这个次方等于 0.30，使类余切在 16ms 约为 0.30、11ms 约为 0.57。
        public static readonly double OFFSET_CURVE_POWER = Math.Log(0.30) / Math.Log((OFFSET_CROSS_MS - 16.0) / (OFFSET_CROSS_MS - OFFSET_FULL_MS));

        /// <summary>
        /// 单次价值的分数尺度。价值为 1 时加成等于此值，价值为 -1（极大 offset / Miss）时罚分等于此值。
        /// </summary>
        public const int JUDGE_BONUS_MAX = 90000;

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
        /// Miss 用 KPS 权重。正向：从 0 KPS 的 0.3 线性升到 <see cref="KPS_SATURATION"/> 的 1。
        /// </summary>
        public static double MissWeight(double kps, bool favourHighKps = true)
        {
            const double floor = 0.3;
            double t = Math.Clamp(kps / KPS_SATURATION, 0, 1);
            return floor + (1 - floor) * (favourHighKps ? t : 1 - t);
        }

        /// <summary>
        /// offset 价值，范围 [-1, 1]。默认类反余切。<paramref name="cotangent"/> 为 true 时用贴横轴的类余切。
        /// <paramref name="missBoundaryMs"/> 是当前模式的 Miss 窗口；Miss 事件传同一个值。
        /// </summary>
        public static double OffsetQuality(double absoluteErrorMs, bool cotangent = false, double missBoundaryMs = OFFSET_MISS_MS)
        {
            if (double.IsNaN(absoluteErrorMs))
                return 0;

            double miss = missBoundaryMs > OFFSET_FULL_MS && !double.IsNaN(missBoundaryMs) ? missBoundaryMs : OFFSET_MISS_MS;
            double e = Math.Abs(absoluteErrorMs);

            if (e >= miss)
                return -1;

            if (cotangent || e <= OFFSET_FULL_MS)
                return cotangentValue(e, miss);

            if (e <= OFFSET_CROSS_MS)
                return 1 - cotangentValue(OFFSET_FULL_MS + OFFSET_CROSS_MS - e, miss);

            if (miss <= OFFSET_CROSS_MS)
                return -1;

            return -1 - cotangentValue(OFFSET_CROSS_MS + miss - e, miss);
        }

        /// <summary>
        /// 贴横轴的价值。6ms 为 1，44ms 为 0，Miss 窗口处为 -1。44→16 慢，16→6 快。
        /// </summary>
        private static double cotangentValue(double e, double miss)
        {
            if (e <= OFFSET_FULL_MS)
                return 1;

            if (e >= miss)
                return -1;

            if (e <= OFFSET_CROSS_MS)
                return Math.Pow((OFFSET_CROSS_MS - e) / (OFFSET_CROSS_MS - OFFSET_FULL_MS), OFFSET_CURVE_POWER);

            if (miss <= OFFSET_CROSS_MS)
                return -1;

            return -Math.Pow((e - OFFSET_CROSS_MS) / (miss - OFFSET_CROSS_MS), OFFSET_CURVE_POWER);
        }
    }
}
