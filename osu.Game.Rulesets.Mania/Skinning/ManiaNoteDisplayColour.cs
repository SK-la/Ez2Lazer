// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit.Settings;
using osu.Game.Graphics;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// 把原始列色 / 节拍线色按皮肤全局着色算法换成显示色。所有着色点都从这里取，算法只此一处分支。
    /// </summary>
    public static class ManiaNoteDisplayColour
    {
        /// <summary>
        ///     Column-assigned note tint (Column bindables), shaped by the skin-wide colouring algorithm.
        /// </summary>
        public static Colour4 ForColumnNoteDisplayTint(Colour4 columnColour, EzColouringMode colouringMode)
        {
            if (colouringMode == EzColouringMode.Direct)
                return columnColour;

            return ManiaNoteDisplayTint.ApplyTo(
                columnColour,
                ManiaNoteDisplayTint.DEFAULT_TARGET_GRAYSCALE,
                ManiaNoteDisplayTint.DEFAULT_COLOUR_ALPHA);
        }

        /// <summary>
        ///     Beat-line colour for one line divisor, shaped by the skin-wide colouring algorithm.
        /// </summary>
        public static Color4 ForBeatLineDisplayColour(int lineDivisor, OsuColour colours, ManiaEditorBeatLinePalette? palette, EzColouringMode colouringMode)
        {
            if (colouringMode == EzColouringMode.Direct)
                return ManiaEditorBeatLinePalette.LineColour(lineDivisor, colours, palette);

            return ManiaNoteDisplayTint.GetDivisorNoteColourFromLineDivisor(lineDivisor, colours, palette);
        }
    }
}
