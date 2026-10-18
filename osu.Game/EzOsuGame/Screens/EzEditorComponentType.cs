// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Screens
{
    /// <summary>
    /// Ez Pro Skin 侧栏里可点选的资源品类。每项对应一组写回 EzSkinSettings 的卡片。
    /// </summary>
    public enum EzEditorComponentType
    {
        [LocalisableDescription(typeof(EzSkinStrings), nameof(EzSkinStrings.COMPONENT_TYPE_NOTE))]
        Note = 0,

        [LocalisableDescription(typeof(EzSkinStrings), nameof(EzSkinStrings.COMPONENT_TYPE_STAGE))]
        Stage,

        [LocalisableDescription(typeof(EzSkinStrings), nameof(EzSkinStrings.COMPONENT_TYPE_PANEL))]
        Panel,

        [LocalisableDescription(typeof(EzSkinStrings), nameof(EzSkinStrings.COMPONENT_TYPE_EARLY_LATE))]
        EarlyLate,

        [LocalisableDescription(typeof(EzSkinStrings), nameof(EzSkinStrings.COMPONENT_TYPE_JUDGEMENT))]
        Judgement,

        [LocalisableDescription(typeof(EzSkinStrings), nameof(EzSkinStrings.COMPONENT_TYPE_COMBO))]
        Combo,

        [LocalisableDescription(typeof(EzSkinStrings), nameof(EzSkinStrings.COMPONENT_TYPE_JUDGEMENT_LINE))]
        JudgementLine,

        [LocalisableDescription(typeof(EzSkinStrings), nameof(EzSkinStrings.COMPONENT_TYPE_COLUMN_LIGHT))]
        ColumnLight,
    }
}
