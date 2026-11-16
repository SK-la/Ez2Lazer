// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 选歌 Panel KPS：消费端只订 <see cref="EzAnalysisCache.GetBindableAnalysis"/>（bindable 首帧可带 SQLite 切片）。
    /// <see cref="TryResolveBaselineFromSqlite"/> 留给 cache 内部种子，不给 Panel 直读。
    /// </summary>
    public static class EzPanelKpsMetrics
    {
        /// <summary>
        /// Panel 显示 L1：NoMod 主 SQLite kps 基线；无可用切片时返回 <see langword="false"/>。
        /// </summary>
        public static bool TryResolveBaselineFromSqlite(
            EzAnalysisDatabase database,
            BeatmapInfo beatmap,
            RulesetInfo ruleset,
            IReadOnlyList<Mod>? mods,
            out EzSongSelectAnalysisDisplay.PanelMetrics metrics)
        {
            metrics = EzSongSelectAnalysisDisplay.EMPTY;

            if (!EzAnalysisDatabase.CanUseStoredAnalysis(beatmap, ruleset, mods))
                return false;

            if (!database.TryGetStoredSqliteSlice(beatmap, ruleset, out var storedSlice))
                return false;

            metrics = EzSongSelectAnalysisDisplay.Resolve(beatmap, storedSlice, mods);
            return metrics.KpsList.Count > 0;
        }
    }
}
