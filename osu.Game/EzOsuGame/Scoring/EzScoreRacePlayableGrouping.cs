// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Race 多幽灵构建时按 playable 转换相关 Mod 分组，避免「全员共用 Empty-Mods playable」把 HR/DT 幽灵判到 NM 谱上。
    /// </summary>
    public static class EzScoreRacePlayableGrouping
    {
        /// <summary>
        /// 返回待构建下标按 playable-mod 指纹分组；同组复用一次 <see cref="EzScoreRacePlayableResolver.GetSessionReady"/>。
        /// </summary>
        public static Dictionary<string, List<int>> GroupIndicesByPlayableMods(IReadOnlyList<ScoreInfo> scores, Func<int, bool> needsBuild)
        {
            ArgumentNullException.ThrowIfNull(scores);
            ArgumentNullException.ThrowIfNull(needsBuild);

            var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);

            for (int i = 0; i < scores.Count; i++)
            {
                if (!needsBuild(i))
                    continue;

                string key = GetPlayableModKey(scores[i].Mods);

                if (!groups.TryGetValue(key, out var list))
                    groups[key] = list = new List<int>();

                list.Add(i);
            }

            return groups;
        }

        /// <summary>
        /// playable 转换指纹：影响谱面转换/难度的 Mod 缩写排序；与 cosmetic 白名单无关——HR/DT 必须进键。
        /// </summary>
        public static string GetPlayableModKey(IEnumerable<Mod>? mods)
        {
            if (mods == null)
                return string.Empty;

            return string.Join(',', mods.OrderBy(m => m.Acronym).Select(m => m.Acronym));
        }
    }
}
