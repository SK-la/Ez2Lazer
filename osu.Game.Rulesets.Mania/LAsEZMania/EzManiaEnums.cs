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
        [Description("(ms) For Default Judgement Line")]
        ScrollTimeStyle = 1,

        [Description("(ms) For Real Judgement Line")]
        ScrollTimeForRealJudgement = 2,

        [Description("(ms) For Screen Bottom")]
        ScrollTimeStyleFixed = 3,

        // [Obsolete("Renamed to ScrollTimeStyleFixed. Kept for backward compatibility with stored settings.")]
        // ScrollTimeStyle = ScrollTimeStyleFixed,
        //
        // [Obsolete("Renamed to ScrollTimeStyleFixed. Kept for backward compatibility with stored settings.")]
        // ScrollTimeStyleFixed = ScrollTimeStyleFixed,
    }
}
