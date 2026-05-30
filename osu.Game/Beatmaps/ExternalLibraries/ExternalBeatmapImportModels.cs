// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    public readonly record struct ExternalBeatmapFileEntry(string RelativeFilename, string Sha256Hash);

    public sealed class ExternalBeatmapDifficultyImportModel
    {
        public required string ChartRelativePath { get; init; }

        public required string Md5Hash { get; init; }

        public required string Sha256Hash { get; init; }

        public required BeatmapDifficulty Difficulty { get; init; }

        public required BeatmapMetadata Metadata { get; init; }

        public required RulesetInfo Ruleset { get; init; }

        public string DifficultyName { get; init; } = string.Empty;

        public double Length { get; init; }

        public double BPM { get; init; }
    }

    public sealed class ExternalBeatmapSetImportModel
    {
        public Guid SetId { get; init; } = Guid.NewGuid();

        public required string SetHash { get; init; }

        public required string ExternalContentRoot { get; init; }

        public required RulesetInfo Ruleset { get; init; }

        public DateTimeOffset DateAdded { get; init; } = DateTimeOffset.UtcNow;

        public List<ExternalBeatmapFileEntry> Files { get; init; } = new List<ExternalBeatmapFileEntry>();

        public List<ExternalBeatmapDifficultyImportModel> Beatmaps { get; init; } = new List<ExternalBeatmapDifficultyImportModel>();
    }
}
