// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;

namespace osu.Game.EzOsuGame.Clocks
{
    /// <summary>
    /// 计算「谱面时基」下的谱面全程时间区间。
    /// 默认 <c>Start = -LEAD_IN_MS</c>，<c>End = max(h.GetEndTime()) + TAIL_BUFFER_MS</c>。
    /// </summary>
    public static class EzBeatmapTimeRangeProvider
    {
        /// <summary>
        /// 谱面前置 lead-in 时间（毫秒），让玩家在谱面第一个物件前有时间准备。
        /// </summary>
        public const double LEAD_IN_MS = 3000;

        /// <summary>
        /// 谱面尾部缓冲（毫秒），用于吸收播放 / 渲染开销与可能的判定延迟。
        /// </summary>
        public const double TAIL_BUFFER_MS = 3000;

        /// <summary>
        /// 计算给定谱面在「谱面时基」下应该覆盖的 [Start, End] 范围（毫秒）。
        /// </summary>
        public static (double Start, double End) ComputeRange(IBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            double start = -LEAD_IN_MS;

            double end = 0;
            bool any = false;

            var hitObjects = beatmap.HitObjects;

            for (int i = 0; i < hitObjects.Count; i++)
            {
                var ho = hitObjects[i];

                if (ho == null)
                    continue;

                double t = ho.GetEndTime();

                if (!any || t > end)
                {
                    end = t;
                    any = true;
                }
            }

            if (!any)
                end = LEAD_IN_MS;
            else
                end += TAIL_BUFFER_MS;

            return (start, end);
        }
    }
}
