// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Replay Session 运行用途：区分储存历史状态和 Live 当前状态。
    /// </summary>
    public enum ReplayRunPurpose
    {
        /// <summary>
        /// 储存历史状态。
        /// </summary>
        ForStored,

        /// <summary>
        /// Live 当前状态。
        /// </summary>
        ForLive,
    }
}
