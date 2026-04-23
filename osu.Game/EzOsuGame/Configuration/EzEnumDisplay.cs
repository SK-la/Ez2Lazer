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

    public enum EzComEffectType
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
}
