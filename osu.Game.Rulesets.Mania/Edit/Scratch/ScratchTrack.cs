// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Edit.Scratch
{
    /// <summary>
    /// A single background (scratch) track.
    /// </summary>
    /// <remarks>
    /// <see cref="Number"/> is track data, intentionally decoupled from the track's position in <see cref="ScratchTrackSet"/>.
    /// </remarks>
    public class ScratchTrack
    {
        public int Number { get; set; }

        public ScratchTrack(int number)
        {
            Number = number;
        }
    }
}
