// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Database;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Scoring.Lamp;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect
{
    public sealed class BmsBarContext
    {
        public required BMSBeatmapManager BeatmapManager { get; init; }

        public required BmsFolderTree FolderTree { get; init; }

        public required BmsSqlSongQuery SqlQuery { get; init; }

        public required string FilterDatabasePath { get; init; }

        public required BmsAnalyticsSqliteRepository Analytics { get; init; }

        public required BmsLampStore LampStore { get; init; }

        public required RealmAccess Realm { get; init; }

        public BmsRajaKeyModeFilter KeyModeFilter { get; } = new BmsRajaKeyModeFilter();

        public BmsSortPolicy SortPolicy { get; } = new BmsSortPolicy();

        public bool ShowInvisibleCharts { get; set; }

        public IReadOnlyList<BmsRajaSearchEntry> SearchHistory { get; internal set; } = Array.Empty<BmsRajaSearchEntry>();
    }

    public readonly record struct BmsRajaSearchEntry(string Query, DateTime AddedAt);
}
