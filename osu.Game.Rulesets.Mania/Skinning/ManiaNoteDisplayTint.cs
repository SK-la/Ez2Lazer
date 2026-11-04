// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Edit.Settings;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mania.Edit;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// Blends a source colour onto note textures prepared for display tinting.
    /// </summary>
    public static class ManiaNoteDisplayTint
    {
        public const double DEFAULT_TARGET_GRAYSCALE = 1.5;

        public const double DEFAULT_COLOUR_ALPHA = 0.8;

        /// <summary>
        /// Note tint aligned to beat snap grid lines at <paramref name="beatDivisor"/>, using <see cref="ManiaEditorBeatLinePalette"/> when provided.
        /// </summary>
        public static Color4 GetBeatLineDivisorNoteColour(
            IBeatmap beatmap,
            double startTime,
            int beatDivisor,
            bool fixedGrid,
            OsuColour colours,
            ManiaEditorBeatLinePalette? palette = null)
            => GetBeatLineDivisorNoteColour(beatmap, startTime, beatDivisor, fixedGrid, colours, palette, DEFAULT_TARGET_GRAYSCALE, DEFAULT_COLOUR_ALPHA);

        public static Color4 GetBeatLineDivisorNoteColour(
            IBeatmap beatmap,
            double startTime,
            int beatDivisor,
            bool fixedGrid,
            OsuColour colours,
            ManiaEditorBeatLinePalette? palette,
            double targetGrayscale,
            double colourAlpha)
        {
            int lineDivisor = ManiaEditorBeatSnapColourIndex.GetLineDivisorAtTime(
                beatmap.ControlPointInfo,
                startTime,
                beatDivisor,
                fixedGrid);

            return GetDivisorNoteColourFromLineDivisor(lineDivisor, colours, palette, targetGrayscale, colourAlpha);
        }

        public static Color4 GetDivisorNoteColourFromLineDivisor(
            int lineDivisor,
            OsuColour colours,
            ManiaEditorBeatLinePalette? palette,
            double targetGrayscale = DEFAULT_TARGET_GRAYSCALE,
            double colourAlpha = DEFAULT_COLOUR_ALPHA)
        {
            Color4 source = ManiaEditorBeatLinePalette.LineColour(lineDivisor, colours, palette);
            return ApplyTo(source, targetGrayscale, colourAlpha);
        }

        public static Color4 ApplyTo(Color4 sourceColour, double targetGrayscale, double colourAlpha)
        {
            float target = (float)targetGrayscale;
            float alpha = (float)colourAlpha;

            return new Color4(
                target + (sourceColour.R - target) * alpha,
                target + (sourceColour.G - target) * alpha,
                target + (sourceColour.B - target) * alpha,
                1f);
        }

        public static Colour4 ApplyTo(Colour4 sourceColour, double targetGrayscale, double colourAlpha)
            => ApplyTo((Color4)sourceColour, targetGrayscale, colourAlpha);
    }
}
