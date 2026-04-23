// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.IO.Archives;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace osu.Game.EzOsuGame.Overlays
{
    public class VisibleBeatmapZipExporter
    {
        private readonly Storage exportStorage;
        private readonly Storage userFileStorage;
        private readonly BeatmapManager beatmapManager;
        private readonly RealmAccess realm;

        public Action<Notification>? PostNotification { get; set; }

        public VisibleBeatmapZipExporter(Storage storage, BeatmapManager beatmapManager, RealmAccess realm)
        {
            exportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");
            userFileStorage = storage.GetStorageForDirectory(@"files");
            this.beatmapManager = beatmapManager;
            this.realm = realm;
        }

        public void Export(string name, IReadOnlyList<BeatmapInfo> visibleBeatmaps) => exportBeatmaps(name, visibleBeatmaps, false, null, Array.Empty<Mod>());

        public void ExportConverted(string name, IReadOnlyList<BeatmapInfo> visibleBeatmaps, RulesetInfo ruleset, IReadOnlyList<Mod> mods) =>
            exportBeatmaps(name, visibleBeatmaps, true, ruleset, mods);

        private void exportBeatmaps(string name, IReadOnlyList<BeatmapInfo> visibleBeatmaps, bool convertWithMods, RulesetInfo? ruleset, IReadOnlyList<Mod> mods)
        {
            if (visibleBeatmaps.Count == 0)
            {
                PostNotification?.Invoke(new SimpleNotification
                {
                    Text = "No filtered beatmaps to export.",
                });
                return;
            }

            string itemFilename = string.IsNullOrWhiteSpace(name)
                ? "filtered-beatmaps"
                : name.GetValidFilename();

            if (itemFilename.Length > LegacyExporter<BeatmapSetInfo>.MAX_FILENAME_LENGTH - 4)
                itemFilename = itemFilename.Remove(LegacyExporter<BeatmapSetInfo>.MAX_FILENAME_LENGTH - 4);

            IEnumerable<string> existingExports = exportStorage
                                                  .GetFiles(string.Empty, $"{itemFilename}*.zip")
                                                  .Concat(exportStorage.GetDirectories(string.Empty));

            string filename = NamingUtils.GetNextBestFilename(existingExports, $"{itemFilename}.zip");

            ProgressNotification notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = NotificationsStrings.FileExportOngoing(itemFilename),
            };

            PostNotification?.Invoke(notification);

            try
            {
                using var outStream = exportStorage.CreateFileSafely(filename);

                if (!exportToStream(itemFilename, visibleBeatmaps, outStream, notification, convertWithMods, ruleset, mods, notification.CancellationToken))
                {
                    notification.State = ProgressNotificationState.Cancelled;
                    exportStorage.Delete(filename);

                    PostNotification?.Invoke(new SimpleErrorNotification
                    {
                        Text = "No files were exported for the current filtered beatmaps.",
                    });

                    return;
                }
            }
            catch
            {
                notification.State = ProgressNotificationState.Cancelled;
                exportStorage.Delete(filename);
                throw;
            }

            notification.CompletionText = NotificationsStrings.FileExportFinished(itemFilename);
            notification.CompletionClickAction = () => exportStorage.PresentFileExternally(filename);
            notification.State = ProgressNotificationState.Completed;
        }

        private bool exportToStream(string rootDirectory,
                                    IReadOnlyList<BeatmapInfo> visibleBeatmaps,
                                    Stream outputStream,
                                    ProgressNotification notification,
                                    bool convertWithMods,
                                    RulesetInfo? ruleset,
                                    IReadOnlyList<Mod> mods,
                                    CancellationToken cancellationToken)
        {
            var beatmapIds = visibleBeatmaps.Select(b => b.ID).ToHashSet();

            var beatmapSets = realm.Run(r => r.All<BeatmapInfo>()
                                              .AsEnumerable()
                                              .Where(b => beatmapIds.Contains(b.ID) && b.BeatmapSet != null)
                                              .GroupBy(b => b.BeatmapSet!)
                                              .Select(g => createExport(g.Key, g.ToList(), convertWithMods, ruleset))
                                              .Where(e => e != null)
                                              .Select(e => e!)
                                              .ToList());

            if (beatmapSets.Count == 0)
                return false;

            var convertedBeatmaps = convertWithMods
                ? realm.Run(r => r.All<BeatmapInfo>()
                                  .AsEnumerable()
                                  .Where(b => beatmapSets.SelectMany(s => s.Files)
                                                         .Select(f => f.BeatmapId)
                                                         .OfType<Guid>()
                                                         .Contains(b.ID))
                                  .Detach()
                                  .ToDictionary(b => b.ID))
                : new Dictionary<Guid, BeatmapInfo>();

            var zipWriterOptions = new ZipWriterOptions(CompressionType.Deflate)
            {
                ArchiveEncoding = new ArchiveEncoding()
            };

            using var writer = new ZipWriter(outputStream, zipWriterOptions);

            int totalFiles = beatmapSets.Sum(e => e.FileCount);
            int processedFiles = 0;
            int exportedFiles = 0;

            foreach (var beatmapSet in beatmapSets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var file in beatmapSet.Files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var stream = getFileContents(file, convertedBeatmaps, convertWithMods, ruleset, mods);

                    processedFiles++;
                    notification.Progress = totalFiles > 0 ? (float)processedFiles / totalFiles : 1;

                    if (stream == null)
                        continue;

                    writer.Write($"{rootDirectory}/{beatmapSet.DirectoryName}/{file.Filename}".Replace('\\', '/'), stream);

                    exportedFiles++;
                }
            }

            return exportedFiles > 0;
        }

        private static VisibleSetExport? createExport(BeatmapSetInfo beatmapSet, List<BeatmapInfo> beatmaps, bool convertWithMods, RulesetInfo? ruleset)
        {
            List<BeatmapInfo> includedBeatmaps = convertWithMods
                ? beatmaps.Where(b => ruleset != null && b.AllowGameplayWithRuleset(ruleset, true)).ToList()
                : beatmaps;

            if (includedBeatmaps.Count == 0)
                return null;

            string directoryName = beatmapSet.GetDisplayString().GetValidFilename();

            if (directoryName.Length > LegacyExporter<BeatmapSetInfo>.MAX_FILENAME_LENGTH)
                directoryName = directoryName.Remove(LegacyExporter<BeatmapSetInfo>.MAX_FILENAME_LENGTH);

            var files = beatmapSet.Files
                                  .Select(file => createFileExport(file, includedBeatmaps))
                                  .Where(file => file != null)
                                  .Select(file => file!)
                                  .ToList();

            if (files.Count == 0)
                return null;

            return new VisibleSetExport(directoryName, files);
        }

        private static FileExport? createFileExport(INamedFileUsage file, IReadOnlyList<BeatmapInfo> includedBeatmaps)
        {
            bool isBeatmapFile = file.Filename.EndsWith(@".osu", StringComparison.OrdinalIgnoreCase);

            BeatmapInfo? beatmap = includedBeatmaps.SingleOrDefault(b => matchesFile(b, file));

            if (isBeatmapFile && beatmap == null)
                return null;

            return new FileExport(file.Filename, file.File.GetStoragePath(), beatmap?.ID);
        }

        private Stream? getFileContents(FileExport file, IReadOnlyDictionary<Guid, BeatmapInfo> convertedBeatmaps, bool convertWithMods, RulesetInfo? ruleset, IReadOnlyList<Mod> mods)
        {
            if (file.BeatmapId == null)
                return userFileStorage.GetStream(file.StoragePath);

            if (!convertWithMods)
                return userFileStorage.GetStream(file.StoragePath);

            if (ruleset == null)
                return null;

            if (!convertedBeatmaps.TryGetValue(file.BeatmapId.Value, out var beatmap))
                return null;

            var workingBeatmap = beatmapManager.GetWorkingBeatmap(beatmap);
            var playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset, mods);

            BeatmapExportUtils.ApplyExportMetadata(playableBeatmap, mods);

            return BeatmapExportUtils.EncodeToStream(playableBeatmap, workingBeatmap.Skin);
        }

        private static bool matchesFile(BeatmapInfo beatmap, INamedFileUsage file)
        {
            if (!string.IsNullOrEmpty(beatmap.File?.Filename) && string.Equals(beatmap.File.Filename, file.Filename, StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrEmpty(beatmap.Hash) && beatmap.Hash == file.File.Hash;
        }

        private record VisibleSetExport(string DirectoryName, IReadOnlyList<FileExport> Files)
        {
            public int FileCount => Files.Count;
        }

        private record FileExport(string Filename, string StoragePath, Guid? BeatmapId);
    }
}
