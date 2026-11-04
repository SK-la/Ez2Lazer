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
        ///     Snap-divisor note tint from the beat line palette.
        /// </summary>
        public static Color4 ForDivisorNoteDisplayTint(IBeatmap beatmap, double startTime, OsuColour colours, ManiaEditorBeatLinePalette? palette = null)
        {
            return ManiaNoteDisplayTint.GetDivisorNoteColour(beatmap, startTime, colours, palette);
        }
    }
}
