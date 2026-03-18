// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.IO;
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
    public class CollectionZipExporter
    {
        private readonly Storage exportStorage;
        private readonly Storage userFileStorage;
        private readonly BeatmapManager beatmapManager;
        private readonly RealmAccess realm;

        public Action<Notification>? PostNotification { get; set; }

        public CollectionZipExporter(Storage storage, BeatmapManager beatmapManager, RealmAccess realm)
        {
            exportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");
            userFileStorage = storage.GetStorageForDirectory(@"files");
            this.beatmapManager = beatmapManager;
            this.realm = realm;
        }

        public void Export(Live<BeatmapCollection> collection)
            => exportCollection(collection, false, null, Array.Empty<Mod>());

        public void ExportConverted(Live<BeatmapCollection> collection, RulesetInfo ruleset, IReadOnlyList<Mod> mods)
            => exportCollection(collection, true, ruleset, mods);

        private void exportCollection(Live<BeatmapCollection> collection, bool convertWithMods, RulesetInfo? ruleset, IReadOnlyList<Mod> mods)
        {
            var collectionInfo = collection.PerformRead(c => (c.ID, c.Name));

            string itemFilename = string.IsNullOrWhiteSpace(collectionInfo.Name)
                ? "collection"
                : collectionInfo.Name.GetValidFilename();

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
                exportToStream(collectionInfo.ID, outStream, notification, convertWithMods, ruleset, mods, notification.CancellationToken);
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

        private void exportToStream(Guid collectionId, Stream outputStream, ProgressNotification notification, bool convertWithMods, RulesetInfo? ruleset, IReadOnlyList<Mod> mods,
                                    CancellationToken cancellationToken)
        {
            realm.Run(r =>
            {
                var collection = r.Find<BeatmapCollection>(collectionId);

                if (collection == null)
                    return;

                string rootDirectory = string.IsNullOrWhiteSpace(collection.Name)
                    ? "collection"
                    : collection.Name.GetValidFilename();

                var beatmapLookup = collection.BeatmapMD5Hashes.ToHashSet();

                var beatmapSets = r.All<BeatmapInfo>()
                                   .AsEnumerable()
                                   .Where(b => b.BeatmapSet != null && beatmapLookup.Contains(b.MD5Hash))
                                   .GroupBy(b => b.BeatmapSet!)
                                   .Select(g => createExport(g.Key, g.ToList(), convertWithMods, ruleset))
                                   .Where(e => e != null)
                                   .Select(e => e!)
                                   .ToList();

                var zipWriterOptions = new ZipWriterOptions(CompressionType.Deflate)
                {
                    ArchiveEncoding = new ArchiveEncoding()
                };

                using var writer = new ZipWriter(outputStream, zipWriterOptions);

                int totalFiles = beatmapSets.Sum(e => e.FileCount);
                int exportedFiles = 0;

                foreach (var beatmapSet in beatmapSets)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var file in beatmapSet.BeatmapSet.Files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        using var stream = getFileContents(beatmapSet, file, convertWithMods, ruleset, mods);

                        if (stream == null)
                            continue;

                        writer.Write($"{rootDirectory}/{beatmapSet.DirectoryName}/{file.Filename}".Replace('\\', '/'), stream);

                        exportedFiles++;
                        notification.Progress = totalFiles > 0 ? (float)exportedFiles / totalFiles : 1;
                    }
                }
            });
        }

        private CollectionSetExport? createExport(BeatmapSetInfo beatmapSet, List<BeatmapInfo> beatmaps, bool convertWithMods, RulesetInfo? ruleset)
        {
            List<BeatmapInfo> includedBeatmaps = convertWithMods
                ? beatmaps.Where(b => ruleset != null && b.Ruleset.MatchesOnlineID(ruleset)).ToList()
                : beatmaps;

            if (includedBeatmaps.Count == 0)
                return null;

            string directoryName = beatmapSet.GetDisplayString().GetValidFilename();

            if (directoryName.Length > LegacyExporter<BeatmapSetInfo>.MAX_FILENAME_LENGTH)
                directoryName = directoryName.Remove(LegacyExporter<BeatmapSetInfo>.MAX_FILENAME_LENGTH);

            int fileCount = beatmapSet.Files.Count(file =>
                !file.Filename.EndsWith(@".osu", StringComparison.OrdinalIgnoreCase)
                || includedBeatmaps.Any(b => b.Hash == file.File.Hash));

            return new CollectionSetExport(beatmapSet, includedBeatmaps, directoryName, fileCount);
        }

        private Stream? getFileContents(CollectionSetExport export, INamedFileUsage file, bool convertWithMods, RulesetInfo? ruleset, IReadOnlyList<Mod> mods)
        {
            var beatmap = export.Beatmaps.SingleOrDefault(b => b.Hash == file.File.Hash);

            if (beatmap == null)
            {
                return file.Filename.EndsWith(@".osu", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : userFileStorage.GetStream(file.File.GetStoragePath());
            }

            if (!convertWithMods)
                return userFileStorage.GetStream(file.File.GetStoragePath());

            if (ruleset == null)
                return null;

            var workingBeatmap = beatmapManager.GetWorkingBeatmap(beatmap);
            var playableBeatmap = workingBeatmap.GetPlayableBeatmap(beatmap.Ruleset, mods);

            BeatmapExportUtils.ApplyExportMetadata(playableBeatmap, mods);

            var stream = new MemoryStream();
            using (var sw = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                new LegacyBeatmapEncoder(playableBeatmap, workingBeatmap.Skin).Encode(sw);

            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        private record CollectionSetExport(BeatmapSetInfo BeatmapSet, IReadOnlyList<BeatmapInfo> Beatmaps, string DirectoryName, int FileCount);
    }
}
