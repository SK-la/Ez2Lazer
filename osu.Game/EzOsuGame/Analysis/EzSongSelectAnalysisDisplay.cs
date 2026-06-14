// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 选歌面板 Ez 分析显示合成（kps/KPC/xxy 等 SQLite 体系指标）。
    /// Panel PP 见 <see cref="EzPanelPerformancePoints"/>（L2 cache + L1 Realm），不在此合成。
    /// L1 Realm 提供 NoMod xxy 基线；L2/L3 <see cref="EzAnalysisResult"/> 提供 kps/KPC 与 mod 动态 xxy（受 <see cref="Ez2Setting.EzAnalysisRecEnabled"/>）。
    /// </summary>
    public static class EzSongSelectAnalysisDisplay
    {
        public readonly record struct PanelMetrics(
            double AverageKps,
            double MaxKps,
            IReadOnlyList<double> KpsList,
            EzManiaSummary? ManiaSummary);

        public static bool HasActiveMods(IReadOnlyList<Mod>? mods) => mods != null && mods.Count > 0;

        public static bool HasDisplayableKps(in EzAnalysisResult result)
            => result.KpsList.Count > 0 || result.AverageKps > 0 || result.MaxKps > 0;

        /// <summary>
        /// 避免 bindable 在异步重算完成前用空占位结果清空 KPS 折线（尤其有 Mod 时）。
        /// </summary>
        public static bool ShouldApplyPanelKpsUpdate(in EzAnalysisResult result, IReadOnlyList<Mod>? mods)
            => HasDisplayableKps(result) || !HasActiveMods(mods);

        public static PanelMetrics Resolve(BeatmapInfo beatmap, EzAnalysisResult? dynamic, IReadOnlyList<Mod>? mods)
        {
            bool useDynamicStarMetrics = HasActiveMods(mods) && dynamic?.ManiaSummary?.XxySr != null;

            double avgKps = 0;
            double maxKps = 0;
            IReadOnlyList<double> kpsList = Array.Empty<double>();

            if (dynamic is { } analysis && HasDisplayableKps(analysis))
            {
                avgKps = analysis.AverageKps;
                maxKps = analysis.MaxKps;
                kpsList = analysis.KpsList;
            }

            EzManiaSummary? maniaSummary = beatmap.ToEzManiaSummaryForDisplay(
                dynamic?.ManiaSummary,
                preferAnalysisValues: useDynamicStarMetrics);

            return new PanelMetrics(avgKps, maxKps, kpsList, maniaSummary);
        }
    }
}
