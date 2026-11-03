// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 选歌 Panel KPS 显示解析（L1 主 SQLite + L2 <see cref="EzAnalysisCache"/> bindable）。
    /// <para>
    /// L1：NoMod 主 SQLite kps 切片 — <see cref="TryResolveBaselineFromSqlite"/>，Panel 首帧同步读。
    /// </para>
    /// <para>
    /// L2：当前 ruleset/mods 下 <see cref="EzAnalysisCache.GetBindableAnalysis"/>（mod 感知 / Rec 即时算）。
    /// </para>
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
            metrics = EzSongSelectAnalysisDisplay.Empty;

            if (!EzAnalysisDatabase.CanUseStoredAnalysis(beatmap, ruleset, mods))
                return false;

            if (!database.TryGetStoredSqliteSlice(beatmap, ruleset, out var storedSlice))
                return false;

            metrics = EzSongSelectAnalysisDisplay.Resolve(beatmap, storedSlice, mods);
            return metrics.KpsList.Count > 0;
        }
    }
}
