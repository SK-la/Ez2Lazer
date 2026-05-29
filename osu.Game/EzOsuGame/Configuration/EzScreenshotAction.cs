// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Configuration
{
    public enum EzScreenshotAction
    {
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.SCREENSHOT_ACTION_SAVE_ONLY))]
        SaveOnly = 1,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.SCREENSHOT_ACTION_SAVE_AND_COPY))]
        SaveAndCopy = 2,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.SCREENSHOT_ACTION_COPY_ONLY))]
        CopyOnly = 3,
    }
}
