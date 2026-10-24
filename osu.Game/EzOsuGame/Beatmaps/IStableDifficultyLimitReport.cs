// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Beatmaps
{
    /// <summary>
    /// Reports whether a decoded beatmap carries values that osu!stable loads for gameplay but rejects when calculating
    /// difficulty.
    /// </summary>
    /// <remarks>
    /// Implemented by the decoded beatmap type so reader-side callers do not need the concrete type.
    /// </remarks>
    public interface IStableDifficultyLimitReport
    {
        bool ExceedsStableDifficultyLimits { get; }
    }
}
