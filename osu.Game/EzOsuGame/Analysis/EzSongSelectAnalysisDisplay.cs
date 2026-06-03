// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 选歌面板分析显示合成：对齐官方星级缓存（BeatmapDifficultyCache）与 <see cref="BeatmapInfo.StarRating"/> 的分工。
    /// L1 Realm 提供 NoMod xxy/PP 基线；L2/L3 的 <see cref="EzAnalysisResult"/> 仅提供 kps/KPC 与 mod 动态指标。
    /// </summary>
    public static class EzSongSelectAnalysisDisplay
    {
        public readonly record struct PanelMetrics(
            double? PerformancePoints,
            double AverageKps,
            double MaxKps,
            IReadOnlyList<double> KpsList,
            EzManiaSummary? ManiaSummary);

        public static bool HasActiveMods(IReadOnlyList<Mod>? mods) => mods != null && mods.Count > 0;

        public static PanelMetrics Resolve(BeatmapInfo beatmap, EzAnalysisResult? dynamic, IReadOnlyList<Mod>? mods)
        {
            bool hasMods = HasActiveMods(mods);
            bool useDynamicMetrics = hasMods && dynamicHasDisplayMetrics(dynamic);

            double? pp = useDynamicMetrics && dynamic!.Value.Pp is double dynamicPp
                ? dynamicPp
                : beatmap.PerformancePoints >= 0
                    ? beatmap.PerformancePoints
                    : dynamic?.Pp;

            double avgKps = dynamic?.AverageKps ?? 0;
            double maxKps = dynamic?.MaxKps ?? 0;
            var kpsList = dynamic?.KpsList ?? System.Array.Empty<double>();

            EzManiaSummary? maniaSummary = beatmap.ToEzManiaSummaryForDisplay(
                dynamic?.ManiaSummary,
                preferAnalysisValues: useDynamicMetrics);

            return new PanelMetrics(pp, avgKps, maxKps, kpsList, maniaSummary);
        }

        private static bool dynamicHasDisplayMetrics(EzAnalysisResult? dynamic)
        {
            if (!dynamic.HasValue)
                return false;

            var result = dynamic.Value;

            if (result.Pp != null)
                return true;

            if (result.ManiaSummary?.XxySr != null)
                return true;

            return result.AverageKps > 0 || result.MaxKps > 0 || result.KpsList.Count > 0;
        }
    }
}
