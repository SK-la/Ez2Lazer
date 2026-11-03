// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.Configuration
{
    /// <summary>
    /// Routing strategy used when entering BMS gameplay.
    /// </summary>
    public enum BMSGameplayRoute
    {
        /// <summary>
        /// Convert the BMS beatmap to a Mania beatmap and run on the Mania ruleset pipeline.
        /// </summary>
        ManiaCompatibility,

        /// <summary>
        /// Run on the native BMS ruleset pipeline (experimental).
        /// </summary>
        BmsNative,
    }
}
