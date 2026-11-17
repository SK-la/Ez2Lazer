// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 选歌面板的 KPS 与列统计。星和 PP 由 <see cref="EzAnalysisCache.ReadVariantValues"/> 读取。
    /// </summary>
    public static class EzSongSelectAnalysisDisplay
    {
        public readonly record struct PanelMetrics(
            double AverageKps,
            double MaxKps,
            IReadOnlyList<double> KpsList,
            EzManiaSummary? ManiaSummary);

        public static readonly PanelMetrics EMPTY = new PanelMetrics(0, 0, Array.Empty<double>(), null);

        public static bool HasActiveMods(IReadOnlyList<Mod>? mods) => mods != null && mods.Count > 0;

        /// <summary>
        /// 避免 bindable 在异步重算完成前用空占位结果清空 KPS 折线（尤其有 Mod 时）。
        /// </summary>
        public static bool ShouldApplyPanelKpsUpdate(in EzAnalysisResult result, IReadOnlyList<Mod>? mods)
            => result.KpsList.Count > 0 || !HasActiveMods(mods);

        public static PanelMetrics Resolve(BeatmapInfo beatmap, EzAnalysisResult? dynamic, IReadOnlyList<Mod>? mods)
        {
            double avgKps = 0;
            double maxKps = 0;
            IReadOnlyList<double> kpsList = Array.Empty<double>();

            if (dynamic is { } analysis && analysis.KpsList.Count > 0)
            {
                avgKps = analysis.AverageKps;
                maxKps = analysis.MaxKps;
                kpsList = analysis.KpsList;
            }

            return new PanelMetrics(avgKps, maxKps, kpsList, dynamic?.ManiaSummary);
        }
    }
}
