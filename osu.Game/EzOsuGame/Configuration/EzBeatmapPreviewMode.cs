// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Configuration
{
    public enum EzBeatmapPreviewMode
    {
        [LocalisableDescription(typeof(EzBeatmapPreviewModeStrings), nameof(EzBeatmapPreviewModeStrings.STATIC))]
        Static,

        [LocalisableDescription(typeof(EzBeatmapPreviewModeStrings), nameof(EzBeatmapPreviewModeStrings.DYNAMIC))]
        Dynamic,

        [LocalisableDescription(typeof(EzBeatmapPreviewModeStrings), nameof(EzBeatmapPreviewModeStrings.STATIC_FULL_MAP))]
        StaticFullMap,

        [LocalisableDescription(typeof(EzBeatmapPreviewModeStrings), nameof(EzBeatmapPreviewModeStrings.STATIC_SCROLL))]
        StaticScroll,
    }

    public static class EzBeatmapPreviewModeStrings
    {
        public static readonly LocalisableString STATIC = new EzLocalizationManager.EzLocalisableString("静态预览", "Static Preview");
        public static readonly LocalisableString DYNAMIC = new EzLocalizationManager.EzLocalisableString("动态预览", "Dynamic Preview");
        public static readonly LocalisableString STATIC_FULL_MAP = new EzLocalizationManager.EzLocalisableString("全图预览", "Full-map Preview");
        public static readonly LocalisableString STATIC_SCROLL = new EzLocalizationManager.EzLocalisableString("卷轴预览", "Scroll Preview");
    }
}
