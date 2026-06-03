// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;

namespace osu.Game.Rulesets.Mania.EzMania
{
    /// <summary>
    /// Ez2Ac (10k2s1p) display column layout: 14k beatmaps may render as 13 on-screen columns.
    /// </summary>
    public static class ManiaEzColumnLayout
    {
        public static int GetDisplayColumnCount(StageDefinition stage) => GetDisplayColumnCount(stage.Columns);

        public static int GetDisplayColumnCount(int logicalColumnCount)
        {
            if (logicalColumnCount <= 0)
                return 0;

            if (GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.ManiaSkipEmptyEdgeColumns) && logicalColumnCount == 14)
                return 13;

            return logicalColumnCount;
        }

        public static bool ShouldSkipBeatmapColumn(StageDefinition stage, int beatmapColumnIndex)
            => beatmapColumnIndex >= GetDisplayColumnCount(stage);
    }
}
