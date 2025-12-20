// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Exposes a mod time range that can be updated from editor timeline UI.
    /// All times are in milliseconds.
    /// </summary>
    public interface ILoopTimeRangeMod
    {
        /// <summary>
        /// Updates the time range represented by this mod.
        /// </summary>
        /// <param name="startTime">Start time in milliseconds.</param>
        /// <param name="endTime">End time in milliseconds.</param>
        void SetLoopTimeRange(double startTime, double endTime);
    }
}
