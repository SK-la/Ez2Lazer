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
    /// Resolves column or timing-based tint for textured tap notes.
    /// </summary>
    public static class ManiaNoteDisplayColour
    {
        public static Color4 ResolveTimingColour(bool timingBasedNoteColouringEnabled, IBeatmap beatmap, double startTime, OsuColour colours)
        {
            if (!timingBasedNoteColouringEnabled)
                return Color4.White;

            int snapDivisor = beatmap.ControlPointInfo.GetClosestBeatDivisor(startTime);
            return BindableBeatDivisor.GetColourFor(snapDivisor, colours);
        }

        public static Colour4 ResolveColumnColour(Colour4 columnColour) => columnColour;
    }
}
