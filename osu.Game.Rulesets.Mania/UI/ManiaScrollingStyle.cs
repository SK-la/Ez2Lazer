// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Rulesets.Mania.UI
{
    public enum ManiaScrollingStyle
    {
        // [LocalisableDescription(typeof(RulesetSettingsStrings), nameof(RulesetSettingsStrings.ScrollingDirectionUp))]
        [Description("40速 通配速度风格(不可用)")]
        ScrollSpeedStyle,

        // [LocalisableDescription(typeof(RulesetSettingsStrings), nameof(RulesetSettingsStrings.ScrollingDirectionDown))]
        [Description("ms值 通配速度风格")]
        ScrollTimeStyle,

        [Description("ms值 恒定反应时间")]
        ScrollTimeStyleFixed,
    }
}
