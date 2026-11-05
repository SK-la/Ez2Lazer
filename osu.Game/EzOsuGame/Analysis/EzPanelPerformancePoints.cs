// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// Panel 的 PP 读取：优先取与 Star 同一次难度计算的结果（mod 感知），缺失时回落到 Realm 的 NoMod 基线。
    /// </summary>
    public static class EzPanelPerformancePoints
    {
        public static double? ResolvePanelPp(in StarDifficulty star, BeatmapInfo beatmap)
        {
            if (star.PerformanceAttributes is { Total: var total } && double.IsFinite(total))
                return total;

            return ResolveRealmBaselinePp(beatmap);
        }

        public static double? ResolveRealmBaselinePp(BeatmapInfo beatmap) =>
            beatmap.PerformancePoints >= 0 ? beatmap.PerformancePoints : null;
    }
}
