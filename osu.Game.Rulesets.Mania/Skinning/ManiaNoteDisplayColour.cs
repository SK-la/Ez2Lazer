// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Edit.Settings;
using osu.Game.Graphics;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning
{
    public static class ManiaNoteDisplayColour
    {
        /// <summary>
        ///     Column-assigned note tint (Column bindables).
        /// </summary>
        public static Colour4 ForColumnNoteDisplayTint(Colour4 columnColour)
        {
            return ManiaNoteDisplayTint.ApplyTo(
                columnColour,
                ManiaNoteDisplayTint.DEFAULT_TARGET_GRAYSCALE,
                ManiaNoteDisplayTint.DEFAULT_COLOUR_ALPHA);
        }

        /// <summary>
        ///     Beat-line note tint from <c>BeatmapEditor.json</c> (or official defaults when a palette entry is unset).
        /// </summary>
        public static Color4 ForBeatLineDivisorNoteDisplayTint(
            IBeatmap beatmap,
            double startTime,
            int beatDivisor,
            bool fixedGrid,
            OsuColour colours,
            ManiaEditorBeatLinePalette? palette = null)
        {
            return ManiaNoteDisplayTint.GetBeatLineDivisorNoteColour(beatmap, startTime, beatDivisor, fixedGrid, colours, palette);
        }
    }
}
