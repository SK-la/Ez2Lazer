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
        /// Starting pagination offset (0 = from the beginning). Applies to both BP and most-played batches of <see cref="EzLocalProfileOnlinePullService.BATCH_SIZE"/>.
        /// </summary>
        public int StartOffset { get; init; }

        /// <summary>
        /// When true, write API score metadata into the local profile contribution table even if .osr cannot be imported.
        /// </summary>
        public bool IncludeInStatsWithoutImport { get; init; }

        /// <summary>
        /// When true, download missing beatmapsets (throttled) and add each map into the BP / most-played collection.
        /// </summary>
        public bool DownloadMissingBeatmaps { get; init; }

        public int BatchSize { get; init; }
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
        public int MapsDownloaded { get; set; }
        public int MapsAlreadyLocal { get; set; }
        public int CollectionAdds { get; set; }
        public int OffsetAfter { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
