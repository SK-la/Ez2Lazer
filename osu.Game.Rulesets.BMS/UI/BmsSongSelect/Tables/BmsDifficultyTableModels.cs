// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Tables
{
    public sealed class BmsDifficultyTable
    {
        public string Name { get; init; } = string.Empty;

        public string Url { get; init; } = string.Empty;

        public string Tag { get; init; } = string.Empty;

        public string SourcePath { get; init; } = string.Empty;

        public IReadOnlyList<BmsTableLevel> Levels { get; init; } = Array.Empty<BmsTableLevel>();
    }

    public sealed class BmsTableLevel
    {
        public string Name { get; init; } = string.Empty;

        public IReadOnlyList<BmsTableEntry> Entries { get; init; } = Array.Empty<BmsTableEntry>();
    }

    public sealed class BmsTableEntry
    {
        public string Md5 { get; init; } = string.Empty;

        public string Sha256 { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Artist { get; init; } = string.Empty;

        public string Level { get; init; } = string.Empty;

        /// <summary>Primary chart download / package page URL from the difficulty table.</summary>
        public string Url { get; init; } = string.Empty;

        /// <summary>Optional differential / append package URL.</summary>
        public string AppendUrl { get; init; } = string.Empty;

        public string PreferredHash =>
            !string.IsNullOrEmpty(Sha256) ? Sha256 :
            !string.IsNullOrEmpty(Md5) ? Md5 :
            string.Empty;

        public string PreferredDownloadUrl =>
            !string.IsNullOrWhiteSpace(Url) ? Url.Trim() :
            !string.IsNullOrWhiteSpace(AppendUrl) ? AppendUrl.Trim() :
            string.Empty;

        public bool HasDownloadUrl => !string.IsNullOrWhiteSpace(PreferredDownloadUrl);
    }
}
