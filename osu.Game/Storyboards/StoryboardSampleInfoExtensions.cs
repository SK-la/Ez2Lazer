// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets;

namespace osu.Game.Storyboards
{
    /// <summary>
    /// Decides whether storyboard audio in a beatmap belongs to the note track rather than to the background.
    /// </summary>
    /// <remarks>
    /// In rulesets whose notes are themselves audio (mania, BMS) a storyboard sample sits on the same timeline the
    /// notes do and is there to be hit along with them, so it follows the hitsound volume the notes play under.
    /// Rulesets that own a separate music track keep the upstream treatment, where storyboard audio is background
    /// sound under the game-wide sample volume.
    /// </remarks>
    public static class StoryboardSampleInfoExtensions
    {
        private const string mania_ruleset_short_name = "mania";
        private const string bms_ruleset_short_name = "bms";

        public static bool IsNoteAudioIn(IRulesetInfo? ruleset)
        {
            if (ruleset == null)
                return false;

            return string.Equals(ruleset.ShortName, mania_ruleset_short_name, StringComparison.Ordinal)
                   || string.Equals(ruleset.ShortName, bms_ruleset_short_name, StringComparison.Ordinal);
        }
    }
}
