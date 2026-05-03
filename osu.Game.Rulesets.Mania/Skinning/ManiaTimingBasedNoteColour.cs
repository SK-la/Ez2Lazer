// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Screens.Edit;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning
{
    public static class ManiaTimingBasedNoteColour
    {
        public static Color4 GetColourFor(IBeatmap beatmap, double startTime, OsuColour colours, double targetGrayscale, double colourAlpha)
        {
            int snapDivisor = beatmap.ControlPointInfo.GetClosestBeatDivisor(startTime);
            return ApplyTo(BindableBeatDivisor.GetColourFor(snapDivisor, colours), targetGrayscale, colourAlpha);
        }

        public static Color4 ApplyTo(Color4 timingBasedColour, double targetGrayscale, double colourAlpha)
        {
            // Equivalent to drawing an alpha-tinted layer on top of an opaque grayscale base,
            // but computed directly to avoid a second proxy/container draw path.
            float target = (float)targetGrayscale;
            float alpha = (float)colourAlpha;

            return new Color4(
                target + (timingBasedColour.R - target) * alpha,
                target + (timingBasedColour.G - target) * alpha,
                target + (timingBasedColour.B - target) * alpha,
                1f);
        }
    }
}
