// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.Rulesets.Mania.EzMania
{
    public enum EzManiaScrollingStyle
    {
        [LocalisableDescription(typeof(EzManiaScrollingStyleStrings), nameof(EzManiaScrollingStyleStrings.SCROLL_SPEED_STYLE))]
        ScrollSpeedStyle = 0,

        [LocalisableDescription(typeof(EzManiaScrollingStyleStrings), nameof(EzManiaScrollingStyleStrings.SCROLL_TIME_FOR_DEFAULT_JUDGEMENT))]
        ScrollTimeStyle = 1,

        [LocalisableDescription(typeof(EzManiaScrollingStyleStrings), nameof(EzManiaScrollingStyleStrings.SCROLL_TIME_FOR_REAL_JUDGEMENT))]
        ScrollTimeForRealJudgement = 2,

        [LocalisableDescription(typeof(EzManiaScrollingStyleStrings), nameof(EzManiaScrollingStyleStrings.SCROLL_TIME_FOR_SCREEN_BOTTOM))]
        ScrollTimeStyleFixed = 3,

        // [Obsolete("Renamed to ScrollTimeStyleFixed. Kept for backward compatibility with stored settings.")]
        // ScrollTimeStyle = ScrollTimeStyleFixed,
        //
        // [Obsolete("Renamed to ScrollTimeStyleFixed. Kept for backward compatibility with stored settings.")]
        // ScrollTimeStyleFixed = ScrollTimeStyleFixed,
    }

    public static class EzManiaScrollingStyleStrings
    {
        public static readonly LocalisableString SCROLL_SPEED_STYLE = new EzLocalizationManager.EzLocalisableString(
            "40速 通配速度风格(1-400速对应1.0~40.0倍速)",
            "40-speed universal style(1-400 speed corresponds to 1.0~40.0x speed)");

        public static readonly LocalisableString SCROLL_TIME_FOR_DEFAULT_JUDGEMENT = new EzLocalizationManager.EzLocalisableString(
            "(ms) 相对于到默认判定线的时间",
            "(ms) Relative to default judgement line");

        public static readonly LocalisableString SCROLL_TIME_FOR_REAL_JUDGEMENT = new EzLocalizationManager.EzLocalisableString(
            "(ms) 相对于到实际判定线的时间",
            "(ms) Relative to real judgement line");

        public static readonly LocalisableString SCROLL_TIME_FOR_SCREEN_BOTTOM = new EzLocalizationManager.EzLocalisableString(
            "(ms) 相对于到屏幕底部的时间",
            "(ms) Relative to screen bottom");
    }
}
