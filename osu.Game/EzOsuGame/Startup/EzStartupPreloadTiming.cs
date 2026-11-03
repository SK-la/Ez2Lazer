// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Controls when Ez startup preload work is scheduled relative to Intro / MainMenu.
    /// Change <see cref="EzStartupTuning.PreloadTiming"/> to A/B test.
    /// </summary>
    public enum EzStartupPreloadTiming
    {
        /// <summary>
        /// Settings at PrepareMenuLoad; song select +800ms; detach after main menu settles / BDSP Ez backfill.
        /// </summary>
        PipelineDefault,

        /// <summary>
        /// Settings and song select at PrepareMenuLoad; detach deferred.
        /// </summary>
        PipelineEarlySongSelect,

        /// <summary>
        /// Settings at PrepareMenuLoad; song select at LoadMenu; detach deferred.
        /// </summary>
        PipelineLateSongSelect,

        /// <summary>
        /// All preload work at PrepareMenuLoad (legacy; may stack with BDSP).
        /// </summary>
        ImmediateAll,
    }
}
