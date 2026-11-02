// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Configuration
{
    public enum EzManiaNotePart
    {
        Note,
        HoldHead,
        HoldBody,
        HoldTail,
    }

    /// <summary>
    /// 全谱预览的绘制风格。目前只有柱面（按时间纵向铺满各列的既有绘制）；后续在此追加。
    /// </summary>
    public enum ManiaLongNoteStyle
    {
        [LocalisableDescription(typeof(ManiaLongNoteStyleStrings), nameof(ManiaLongNoteStyleStrings.COLUMN))]
        Column,
    }

    public static class ManiaLongNoteStyleStrings
    {
        public static readonly LocalisableString COLUMN = new EzLocalizationManager.EzLocalisableString("柱面", "Column LN");
    }
}
