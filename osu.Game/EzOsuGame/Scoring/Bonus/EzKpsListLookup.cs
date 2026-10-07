// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.EzOsuGame.Scoring.Bonus
{
    /// <summary>
    /// 附加分的外部 KPS 输入：按 Note 起始时间返回真实时间 KPS。
    /// </summary>
    public interface IEzKpsSectionLookup
    {
        double KpsAt(double time);
    }

    /// <summary>
    /// 复用 <see cref="OptimizedBeatmapCalculator.GetKpsOptimized"/> 的完整 KPS list（按主 BPM 每 4 拍一段，从 0ms 起）。
    /// </summary>
    public sealed class EzKpsListLookup : IEzKpsSectionLookup
    {
        private readonly IReadOnlyList<double> kpsList;
        private readonly double interval;
        private readonly double songEnd;
        private readonly double rate;
        private readonly bool fromAnalysis;

        public EzKpsListLookup(IReadOnlyList<double> kpsList, double bpm, double rate)
        {
            this.kpsList = kpsList;
            interval = bpm > 0 ? 240000.0 / bpm : 0;
            this.rate = rate > 0 ? rate : 1;
        }

        private EzKpsListLookup(IReadOnlyList<double> kpsList, double songEndTime)
        {
            this.kpsList = kpsList;
            songEnd = songEndTime;
            fromAnalysis = true;
            rate = 1;
        }

        /// <param name="beatmap">已应用 mod 的可玩谱面（track 时间）。</param>
        /// <param name="rate">播放速率；KPS 换算为真实时间。</param>
        public static EzKpsListLookup FromBeatmap(IBeatmap beatmap, double rate)
            => new EzKpsListLookup(OptimizedBeatmapCalculator.GetKpsOptimized(beatmap).kpsList, beatmap.BeatmapInfo.BPM, rate);

        /// <summary>
        /// <see cref="EzAnalysisCache"/> 里活动谱面 + mod 的 KPS list。数值已含速率，按歌曲进度取样。
        /// </summary>
        public static EzKpsListLookup FromAnalysis(IReadOnlyList<double> kpsList, double songEndTime)
            => new EzKpsListLookup(kpsList, songEndTime);

        public double KpsAt(double time)
        {
            if (kpsList.Count == 0 || double.IsNaN(time))
                return 0;

            if (fromAnalysis)
            {
                if (kpsList.Count == 1 || songEnd <= 0)
                    return kpsList[0];

                double fraction = Math.Clamp(time / songEnd, 0, 1);
                int index = (int)Math.Round(fraction * (kpsList.Count - 1));
                return kpsList[Math.Clamp(index, 0, kpsList.Count - 1)];
            }

            if (interval <= 0)
                return 0;

            int bar = Math.Clamp((int)Math.Floor(time / interval), 0, kpsList.Count - 1);
            return kpsList[bar] * rate;
        }
    }
}
