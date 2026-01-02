// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Rulesets.Mania.LAsEZMania
{
    public enum EzManiaScrollingStyle
    {
        // [LocalisableDescription(typeof(RulesetSettingsStrings), nameof(RulesetSettingsStrings.ScrollingDirectionUp))]
        [Description("40速 通配速度风格(不可用)")]
        ScrollSpeedStyle,

        // [LocalisableDescription(typeof(RulesetSettingsStrings), nameof(RulesetSettingsStrings.ScrollingDirectionDown))]
        [Description("ms值 相对默认判定线")]
        ScrollTimeForDefaultJudgement,

        [Description("ms值 相对实际判定线")]
        ScrollTimeForRealJudgement,

        [Description("ms值 相对屏幕底部")]
        ScrollTimeForScreenBottom,
    }
}
