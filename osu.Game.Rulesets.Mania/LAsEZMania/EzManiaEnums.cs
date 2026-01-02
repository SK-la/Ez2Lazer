// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;

namespace osu.Game.Rulesets.Mania.LAsEZMania
{
    public enum EzManiaScrollingStyle
    {
        // [LocalisableDescription(typeof(RulesetSettingsStrings), nameof(RulesetSettingsStrings.ScrollingDirectionUp))]
        [Description("40速 通配速度风格(不可用)")]
        ScrollSpeedStyle = 0,

        // [LocalisableDescription(typeof(RulesetSettingsStrings), nameof(RulesetSettingsStrings.ScrollingDirectionDown))]
        [Description("ms值 相对默认判定线")]
        ScrollTimeForDefaultJudgement = 1,

        [Description("ms值 相对实际判定线")]
        ScrollTimeForRealJudgement = 2,

        [Description("ms值 相对屏幕底部")]
        ScrollTimeForScreenBottom = 3,

        [Obsolete("Renamed to ScrollTimeForScreenBottom. Kept for backward compatibility with stored settings.")]
        ScrollTimeStyle = ScrollTimeForScreenBottom,

        [Obsolete("Renamed to ScrollTimeForScreenBottom. Kept for backward compatibility with stored settings.")]
        ScrollTimeStyleFixed = ScrollTimeForScreenBottom,
    }
}
