// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 选歌 Panel PP 显示解析（L1 Realm + L2 官方 cache）。
    /// <para>
    /// L2：<see cref="StarDifficulty.PerformanceAttributes"/>?.Total — 与 Star 同一次 <see cref="BeatmapDifficultyCache"/> 计算（mod 感知）。
    /// </para>
    /// <para>
    /// L1：<see cref="BeatmapInfo.PerformancePoints"/> — NoMod 基线，与 <see cref="BeatmapInfo.StarRating"/> 对称；
    /// 随 <see cref="Rulesets.RulesetInfo.LastAppliedDifficultyVersion"/> 与 Star 一并失效并重算。
    /// </para>
    /// </summary>
    public static class EzPanelPerformancePoints
    {
        public static double? ResolvePanelPp(in StarDifficulty star, BeatmapInfo beatmap)
        {
            if (star.PerformanceAttributes is { Total: var total } && double.IsFinite(total))
                return total;

            return ResolveRealmBaselinePp(beatmap);
        }

        /// <summary>
        /// L1 Realm 基线 PP（NoMod）；Panel 首帧与 Star cache 未就绪时的占位。
        /// </summary>
        public static double? ResolveRealmBaselinePp(BeatmapInfo beatmap) =>
            beatmap.PerformancePoints >= 0 ? beatmap.PerformancePoints : null;
    }
}
