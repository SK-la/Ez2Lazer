// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.Beatmaps.Formats
{
    public static class BeatmapEncoder
    {
        public static IBeatmapEncoder Create(IBeatmap beatmap, ISkin? skin, Storyboard? storyboard)
        {
            IBeatmapEncoder? custom = beatmap.BeatmapInfo.Ruleset.CreateInstance().CreateBeatmapEncoder(beatmap, skin, storyboard);
            return custom ?? new LegacyBeatmapEncoder(beatmap, skin, storyboard);
        }
    }
}
