// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Audio.Track;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.Storyboards
{
    /// <summary>
    /// Decides how storyboard audio in a beatmap is wired into the volume chain.
    /// </summary>
    /// <remarks>
    /// In rulesets whose notes are themselves audio (mania, BMS) a storyboard sample sits on the same timeline the
    /// notes do, which is the only signal available to tell such a sample apart from a storyboard that merely has
    /// background sound on these rulesets: both arrive as plain <see cref="StoryboardSampleInfo"/>.
    /// </remarks>
    public static class StoryboardSampleInfoExtensions
    {
        private const string mania_ruleset_short_name = "mania";
        private const string bms_ruleset_short_name = "bms";

        /// <summary>
        /// Whether storyboard audio in this beatmap is note audio, i.e. plays on the timeline the notes play on and
        /// therefore follows the hitsound volume.
        /// </summary>
        public static bool IsNoteAudioIn(IRulesetInfo? ruleset)
        {
            if (ruleset == null)
                return false;

            return string.Equals(ruleset.ShortName, mania_ruleset_short_name, StringComparison.Ordinal)
                   || string.Equals(ruleset.ShortName, bms_ruleset_short_name, StringComparison.Ordinal);
        }

        /// <summary>
        /// Whether the storyboard carries the beatmap's background music, i.e. the beatmap has no audio track of its
        /// own and the music is played back as storyboard samples at their own offsets.
        /// </summary>
        /// <remarks>
        /// A virtual track is the signal: such a beatmap resolves to <see cref="TrackVirtual"/> because there is no
        /// audio file to play, so the storyboard is the only remaining source of music. These samples are background
        /// sound, not note audio, and belong under the music volume.
        /// </remarks>
        public static bool CarriesBackgroundMusicIn(IWorkingBeatmap beatmap)
            => IsNoteAudioIn(beatmap.BeatmapInfo.Ruleset) && beatmap.TrackLoaded && beatmap.Track is TrackVirtual;
    }
}
