// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// Mania 列数解析与稀疏列统计补全（<c>osu.Game</c> 侧）。
    /// <para>
    /// 真实列数 N = ruleset 的 <see cref="Ruleset.GetVariantForBeatmap"/>（Mania 下等于 <c>GetKeyCount</c>，含 key / converter mod），
    /// 回落 <see cref="BeatmapDifficulty.CircleSize"/>。<br/>
    /// 显示列数 = Ez2Ac 10k2s1p（<see cref="Ez2Setting.ManiaSkipEmptyEdgeColumns"/>）开启时 14k 按 13 列渲染。
    /// </para>
    /// <para>
    /// 游戏内渲染的列数由 Mania 程序集内的 <c>ManiaEzColumnLayout</c> 负责；本类供无法引用 Mania 程序集的消费方
    /// （选歌面板 scratch 标签、KPC 图）使用。
    /// </para>
    /// </summary>
    public static class EzManiaColumnLayout
    {
        /// <summary>
        /// 屏幕渲染列数：Ez2Ac 10k2s1p 开启且真实列数为 14 时按 13 列。
        /// </summary>
        public static int GetDisplayColumnCount(int realColumnCount)
        {
            if (realColumnCount <= 0)
                return 0;

            if (realColumnCount == 14 && GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.ManiaSkipEmptyEdgeColumns))
                return 13;

            return realColumnCount;
        }

        /// <summary>
        /// 解析真实列数：优先 ruleset（含 converter mod），回落 <see cref="BeatmapDifficulty.CircleSize"/>。
        /// </summary>
        public static int ResolveRealColumnCount(IRulesetInfo? ruleset, BeatmapInfo? beatmap, IReadOnlyList<Mod>? mods)
        {
            if (ruleset != null && beatmap != null)
            {
                try
                {
                    int fromRuleset = ruleset.CreateInstance().GetVariantForBeatmap(beatmap, mods);

                    if (fromRuleset > 0)
                        return fromRuleset;
                }
                catch
                {
                    // 忽略：回落 CircleSize。
                }
            }

            if (beatmap != null)
            {
                int fromCircleSize = (int)Math.Round(beatmap.Difficulty.CircleSize);

                if (fromCircleSize > 0)
                    return fromCircleSize;
            }

            return 0;
        }

        /// <summary>
        /// 把稀疏列统计补 0 到真实列数，只在末尾缺口补。
        /// <paramref name="counts"/> 为空表示"无列数据"（0 note 谱面），不补。
        /// </summary>
        public static void PadToRealColumnCount(Dictionary<int, int>? counts, int realColumnCount)
        {
            if (counts == null || counts.Count == 0 || realColumnCount <= 0)
                return;

            int maxColumn = 0;

            foreach (int column in counts.Keys)
            {
                if (column > maxColumn)
                    maxColumn = column;
            }

            for (int column = maxColumn + 1; column < realColumnCount; column++)
                counts[column] = 0;
        }

        /// <summary>
        /// 同时补全 <c>ColumnCounts</c> 与 <c>HoldNoteCounts</c>，保持两序列对齐。
        /// </summary>
        public static void PadToRealColumnCount(Dictionary<int, int>? columnCounts, Dictionary<int, int>? holdNoteCounts, int realColumnCount)
        {
            PadToRealColumnCount(columnCounts, realColumnCount);
            PadToRealColumnCount(holdNoteCounts, realColumnCount);
        }
    }
}
