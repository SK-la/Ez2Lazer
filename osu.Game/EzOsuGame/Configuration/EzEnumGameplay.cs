// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Configuration
{
    public enum ColumnWidthStyle
    {
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.EZ_STYLE_PRO_ONLY))]
        EzSkinOnly = 0,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.GLOBAL_WIDTH))]
        GlobalWidth = 1,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.GLOBAL_TOTAL_WIDTH))]
        GlobalTotalWidth = 2,
    }

    public enum EzEnumHitMode
    {
        [Description("Lazer Style")]
        Lazer = 0,

        [Description("EZ2AC Style")]
        EZ2AC = 1,

        [Description("O2JAM Style")]
        O2Jam = 2,

        [Description("IIDX Hard Style")]
        IIDX_HD = 3,

        [Description("LR2 Hard Style")]
        LR2_HD = 4,

        [Description("Raja Hard Style")]
        Raja_NM = 5,

        [Description("")]
        Malody = 6,

        [Description("")]
        Classic = 7,
    }

    public enum EzEnumHealthMode
    {
        [Description("Lazer")]
        Lazer = 0,

        [Description("O2Jam Easy")]
        O2JamEasy = 1,

        [Description("O2Jam Normal")]
        O2JamNormal = 2,

        [Description("O2Jam Hard")]
        O2JamHard = 3,

        [Description("Ez2Ac(NoActive)")]
        Ez2Ac = 4,

        [Description("IIDX Hard(Testing)")]
        IIDX_HD = 5,

        [Description("LR2 Hard(Testing)")]
        LR2_HD = 6,

        [Description("raja normal(Testing)")]
        Raja_NM = 7,
    }
}
