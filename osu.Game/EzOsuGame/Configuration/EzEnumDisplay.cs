// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Configuration
{
    /// <summary>
    /// 显示模式枚举
    /// </summary>
    public enum EzEnumChartDisplay
    {
        /// <summary>
        /// 数字（默认，最高性能）
        /// </summary>
        Numbers,

        /// <summary>
        /// 柱状图
        /// </summary>
        BarChart
    }

    public enum KeySoundPreviewMode
    {
        Off = 0,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.AUTO_PREVIEW))]
        AutoPreview = 1,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.AUTO_PLAY_PLUS))]
        AutoPlayPlus = 2,
    }

    public enum EzBeatmapPreviewMode
    {
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.STATIC))]
        Static,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DYNAMIC))]
        Dynamic,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.STATIC_FULL_MAP))]
        StaticFullMap,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.STATIC_SCROLL))]
        StaticScroll,
    }

    public enum EzEffectType
    {
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.SCALE))]
        Scale,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.BOUNCE))]
        Bounce,

        None
    }

    public enum ScalingGameMode
    {
        Standard,

        Taiko,

        Mania,

        Catch,
    }

    public enum EzLogoVisualisationStyle
    {
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.LOGO_VIS_BARS))]
        RadialBars,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.LOGO_VIS_POLYLINE))]
        CircularPolyline,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.LOGO_VIS_WAVE))]
        CircularWave,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.LOGO_VIS_DOTS))]
        CircularDots,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.LOGO_VIS_NET))]
        CircularNet,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.LOGO_VIS_OFF))]
        Off,
    }

    public enum EzLogoBackdropStyle
    {
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.LOGO_BACKDROP_SOLID))]
        Solid,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.LOGO_BACKDROP_ACRYLIC))]
        Acrylic,

        // 实际使用效果并不好，注释掉暂时不用，勿删，以后有灵感再说。
        // [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.LOGO_BACKDROP_LINKED))]
        // Linked,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.LOGO_BACKDROP_CLEAR))]
        Clear,
    }
}
