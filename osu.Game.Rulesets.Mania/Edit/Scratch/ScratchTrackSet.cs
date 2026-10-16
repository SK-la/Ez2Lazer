// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;

namespace osu.Game.Rulesets.Mania.Edit.Scratch
{
    /// <summary>
    /// Ordered, session-level collection of background (scratch) tracks.
    /// </summary>
    public class ScratchTrackSet
    {
        /// <summary>
        /// Track number reserved for the main audio track.
        /// </summary>
        public const int MAIN_AUDIO_TRACK_NUMBER = 1;

        public BindableList<ScratchTrack> Tracks { get; } = new BindableList<ScratchTrack>();

        /// <summary>
        /// Appends an empty track to the right. The assigned number continues past the highest number currently in use.
        /// </summary>
        public ScratchTrack Append()
        {
            var track = new ScratchTrack(nextNumber());
            Tracks.Add(track);
            return track;
        }

        /// <summary>
        /// Removes the rightmost track.
        /// </summary>
        /// <returns>Whether a track was removed.</returns>
        public bool RemoveLast()
        {
            if (Tracks.Count == 0)
                return false;

            Tracks.RemoveAt(Tracks.Count - 1);
            return true;
        }

        private int nextNumber()
        {
            int next = 1;

            foreach (var track in Tracks)
                next = Math.Max(next, track.Number + 1);

            return next;
        }
    }
}
