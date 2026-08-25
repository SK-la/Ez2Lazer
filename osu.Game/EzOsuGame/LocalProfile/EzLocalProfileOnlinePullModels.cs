// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public readonly struct EzLocalProfileOnlinePullRequest
    {
        public EzLocalProfileOnlinePullKind Kind { get; init; }

        public RulesetInfo Ruleset { get; init; }

        /// <summary>
        /// Starting pagination offset for <see cref="EzLocalProfileOnlinePullKind.MostPlayed"/> (0 = from the beginning).
        /// After a successful batch the store advances to this value + batch size.
        /// </summary>
        public int MostPlayedStartOffset { get; init; }

        /// <summary>
        /// When true, write API score metadata into the local profile contribution table even if .osr cannot be imported.
        /// </summary>
        public bool IncludeInStatsWithoutImport { get; init; }

        public int MostPlayedBatchSize { get; init; }
    }

    public sealed class EzLocalProfileOnlinePullResult
    {
        public int Candidates { get; set; }
        public int Imported { get; set; }
        public int AlreadyOwned { get; set; }
        public int NoReplay { get; set; }
        public int MissingBeatmap { get; set; }
        public int Failed { get; set; }
        public int StatsRecorded { get; set; }
        public int MostPlayedOffsetAfter { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
