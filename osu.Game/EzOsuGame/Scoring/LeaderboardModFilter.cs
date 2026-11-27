// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Selects mod categories that participate in selected-mod leaderboard matching.
    /// </summary>
    [Flags]
    public enum LeaderboardModFilter
    {
        /// <summary>Use the original selected-mod matching.</summary>
        [LocalisableDescription(typeof(EzSongSelectStrings), nameof(EzSongSelectStrings.LEADERBOARD_MOD_FILTER_EXACT))]
        None = 0,

        /// <summary>Playback-rate changes.</summary>
        [LocalisableDescription(typeof(EzSongSelectStrings), nameof(EzSongSelectStrings.LEADERBOARD_MOD_FILTER_RATE))]
        Rate = 1 << 0,

        /// <summary>Difficulty and judgement changes.</summary>
        [LocalisableDescription(typeof(EzSongSelectStrings), nameof(EzSongSelectStrings.LEADERBOARD_MOD_FILTER_DIFFICULTY))]
        Difficulty = 1 << 1,

        /// <summary>Playable beatmap conversion.</summary>
        [LocalisableDescription(typeof(EzSongSelectStrings), nameof(EzSongSelectStrings.LEADERBOARD_MOD_FILTER_CONVERSION))]
        Conversion = 1 << 2,

        /// <summary>Match all mods except the exceptions from every category.</summary>
        [LocalisableDescription(typeof(EzSongSelectStrings), nameof(EzSongSelectStrings.LEADERBOARD_MOD_FILTER_ALL))]
        All = Rate | Difficulty | Conversion,
    }

    internal static class LeaderboardModFilterExtensions
    {
        public static Mod[] GetRelevantMods(this IEnumerable<Mod> mods, LeaderboardModFilter filter)
            => EzLocalScoreQueries.GetRelevantLeaderboardMods(mods, filter);

        public static bool HasMatchingMods(this IReadOnlyList<Mod> scoreMods, IReadOnlyList<Mod> selectedMods, LeaderboardModFilter filter)
            => EzLocalScoreQueries.MatchesLeaderboardMods(scoreMods, selectedMods, filter);

        public static bool JudgeModeMatches(int scoreJudgeMode, EzEnumJudgeMode? selectedJudgeMode, LeaderboardModFilter filter)
            => EzLocalScoreQueries.JudgeModeMatches(scoreJudgeMode, selectedJudgeMode, filter);
    }
}
