// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.LAsEzExtensions.Localization;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public enum ColumnWidthStyle
    {
        [LocalisableDescription(typeof(ColumnWidthStyleStrings), nameof(ColumnWidthStyleStrings.EZ_STYLE_PRO_ONLY))]
        EzStyleProOnly = 0,

        [LocalisableDescription(typeof(ColumnWidthStyleStrings), nameof(ColumnWidthStyleStrings.GLOBAL_WIDTH))]
        GlobalWidth = 1,

        [LocalisableDescription(typeof(ColumnWidthStyleStrings), nameof(ColumnWidthStyleStrings.GLOBAL_TOTAL_WIDTH))]
        GlobalTotalWidth = 2,
    }

    public static class ColumnWidthStyleStrings
    {
        public static readonly LocalisableString EZ_STYLE_PRO_ONLY = new EzLocalizationManager.EzLocalisableString("Ez Pro 皮肤专用", "EzStylePro Skin Only");
        public static readonly LocalisableString GLOBAL_WIDTH = new EzLocalizationManager.EzLocalisableString("全局宽度", "Global Width");
        public static readonly LocalisableString GLOBAL_TOTAL_WIDTH = new EzLocalizationManager.EzLocalisableString("全局总宽度", "Global Total Width");
    }
}
