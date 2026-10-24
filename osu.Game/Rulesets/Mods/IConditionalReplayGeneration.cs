// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// An <see cref="ICreateReplayData"/> mod that can be configured to stop generating a replay, so the beatmap is
    /// played by the user rather than watched.
    /// </summary>
    /// <remarks>
    /// Whether a replay is generated decides which <see cref="Screens.Play.Player"/> is used, which is fixed once
    /// gameplay starts. Call sites that only need the "watch it" behaviour should therefore ask this interface first.
    /// </remarks>
    public interface IConditionalReplayGeneration
    {
        /// <summary>
        /// Whether a replay should be generated for this mod as currently configured.
        /// </summary>
        bool ReplayGenerationEnabled { get; }
    }
}
