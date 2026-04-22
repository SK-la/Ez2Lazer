// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// BMS-specific beatmap container.
    /// </summary>
    public class BMSBeatmap : Beatmap
    {
        /// <summary>
        /// Total number of columns in this beatmap.
        /// </summary>
        public int TotalColumns { get; set; } = 8;

        /// <summary>
        /// Whether this beatmap includes a scratch lane.
        /// </summary>
        public bool HasScratch => TotalColumns == 6 || TotalColumns == 8 || TotalColumns == 16;

        /// <summary>
        /// Whether this is a Double Play beatmap (14K).
        /// </summary>
        public bool IsDoublePlay => TotalColumns > 9;
    }
}
