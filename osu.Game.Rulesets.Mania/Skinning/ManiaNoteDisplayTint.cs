// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Screens.Edit;
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

        public static Color4 GetDivisorNoteColour(IBeatmap beatmap, double startTime, OsuColour colours)
            => GetDivisorNoteColour(beatmap, startTime, colours, DEFAULT_TARGET_GRAYSCALE, DEFAULT_COLOUR_ALPHA);

        public static Color4 GetDivisorNoteColour(IBeatmap beatmap, double startTime, OsuColour colours, double targetGrayscale, double colourAlpha)
        {
            int snapDivisor = beatmap.ControlPointInfo.GetClosestBeatDivisor(startTime);
            return ApplyTo(BindableBeatDivisor.GetColourFor(snapDivisor, colours), targetGrayscale, colourAlpha);
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
