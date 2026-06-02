// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Rulesets.BMS.Localization;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Two-phase BMS library import: SQLite index scan, then Realm catalog sync (off UI thread).
    /// </summary>
    public static class BmsLibraryImportPipeline
    {
        /// <summary>Progress fraction reserved for the disk scan / SQLite index phase.</summary>
        public const double SCAN_PROGRESS_PORTION = 0.85;

        public readonly struct ImportProgress
        {
            public ImportProgress(double progress, string statusMessage)
            {
                Progress = progress;
                StatusMessage = statusMessage;
            }

            public double Progress { get; }
            public string StatusMessage { get; }
        }

        public readonly struct ImportResult
        {
            public ImportResult(int songCount, int chartCount)
            {
                SongCount = songCount;
                ChartCount = chartCount;
            }

            public int SongCount { get; }
            public int ChartCount { get; }
        }

        /// <summary>
        /// Run scan then Realm sync. Safe to call from a background thread.
        /// </summary>
        public static async Task<ImportResult> RunAsync(
            BMSBeatmapManager manager,
            Storage storage,
            RealmAccess realm,
            RulesetInfo bmsRulesetInfo,
            IReadOnlyList<string> paths,
            Action<ImportProgress>? reportProgress = null,
            CancellationToken cancellationToken = default)
        {
            reportProgress?.Invoke(new ImportProgress(0, BmsStrings.IMPORT_INDEXING.ToString()));

            await manager.ScanLibraryAsync(paths, cancellationToken).ConfigureAwait(false);

            reportProgress?.Invoke(new ImportProgress(SCAN_PROGRESS_PORTION, BmsStrings.IMPORT_WRITING_CATALOG.ToString()));

            await Task.Run(
                () => BMSOsuLibrarySynchronizer.Synchronize(manager, storage, realm, bmsRulesetInfo),
                cancellationToken).ConfigureAwait(false);

            int songs = manager.LibraryCache?.Songs.Count ?? 0;
            int charts = manager.LibraryCache?.TotalCharts ?? 0;

            reportProgress?.Invoke(new ImportProgress(1, BmsStrings.Import_Complete(songs, charts)));

            return new ImportResult(songs, charts);
        }

        /// <summary>
        /// Maps scan progress (0–1) into the first phase of combined import progress.
        /// </summary>
        public static double MapScanProgress(double scanProgress) => scanProgress * SCAN_PROGRESS_PORTION;
    }
}
