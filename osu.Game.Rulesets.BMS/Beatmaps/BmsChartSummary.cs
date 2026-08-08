// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    public enum BmsChartSort
    {
        Title,
        Level,
        Artist,
        Folder,
    }

    public readonly record struct BmsChartPageCursor(string SortKey, Guid BeatmapId);

    public sealed record BmsChartQuery(
        string? FolderPath = null,
        string? SearchText = null,
        IReadOnlyCollection<int>? KeyCounts = null,
        BmsChartSort Sort = BmsChartSort.Title);

    public sealed record BmsChartSummary(
        Guid BeatmapId,
        Guid SetId,
        string PathKey,
        string ChartPath,
        string FolderPath,
        string FileName,
        string Title,
        string Artist,
        int PlayLevel,
        int KeyCount,
        double Bpm,
        int TotalNotes,
        int PreviewTime);

    public sealed record BmsChartSummaryPage(IReadOnlyList<BmsChartSummary> Items, BmsChartPageCursor? NextCursor);

    public sealed record BmsFolderSummary(string FolderPath, string Name);
}
