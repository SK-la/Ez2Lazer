// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Screens.Edit;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning
{
    public static class ManiaNoteDisplayColour
    {
        /// <summary>
        /// Column-assigned note tint (Column bindables). PR #14 adds texture-preserving ApplyTo here.
        /// </summary>
        public static Color4 ForColumnNoteDisplayTint(Colour4 columnColour) => columnColour;

        /// <summary>
        /// Snap-divisor note tint from the beat line palette. PR #14 adds texture-preserving ApplyTo here.
        /// </summary>
        public static Color4 ForDivisorNoteDisplayTint(IBeatmap beatmap, double startTime, OsuColour colours)
        {
            int snapDivisor = beatmap.ControlPointInfo.GetClosestBeatDivisor(startTime);
            return BindableBeatDivisor.GetColourFor(snapDivisor, colours);
        }
    }
}
