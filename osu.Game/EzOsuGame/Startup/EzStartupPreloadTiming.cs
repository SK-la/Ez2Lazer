// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Controls when Song Select preload work is scheduled relative to Intro / MainMenu.
    /// Change <see cref="EzStartupTuning.PreloadTiming"/> to A/B test.
    /// </summary>
    public enum EzStartupPreloadTiming
    {
        /// <summary>
        /// Start Song Select preload after BDSP finishes, with a short buffer and a timeout fallback.
        /// </summary>
        PipelineDefault,

        /// <summary>
        /// Start Song Select preload when MainMenu finishes loading offscreen.
        /// </summary>
        PipelineEarlySongSelect,

        /// <summary>
        /// Start Song Select preload after a short delay from MainMenu offscreen-ready.
        /// </summary>
        PipelineLateSongSelect,

        /// <summary>
        /// Legacy alias for <see cref="PipelineEarlySongSelect"/>.
        /// </summary>
        ImmediateAll,
    }
}
