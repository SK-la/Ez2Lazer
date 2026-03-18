// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
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
    public class SelectedBeatmapExporter
    {
        private readonly Storage exportStorage;
        private readonly Storage userFileStorage;
        private readonly BeatmapManager beatmapManager;
        private readonly RealmAccess realm;

        public Action<Notification>? PostNotification { get; set; }

        public SelectedBeatmapExporter(Storage storage, BeatmapManager beatmapManager, RealmAccess realm)
        {
            exportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");
            userFileStorage = storage.GetStorageForDirectory(@"files");
            this.beatmapManager = beatmapManager;
            this.realm = realm;
        }

        public void ExportBeatmapAsOsu(BeatmapInfo beatmapInfo, RulesetInfo ruleset, IReadOnlyList<Mod> mods)
        {
            var beatmap = realm.Run(r => r.FindWithRefresh<BeatmapInfo>(beatmapInfo.ID)?.Detach());

            if (beatmap == null)
            {
                PostNotification?.Invoke(new SimpleErrorNotification { Text = "Failed to load selected beatmap for export." });
                return;
            }

            string itemFilename = Path.GetFileNameWithoutExtension((beatmap.File?.Filename ?? createBeatmapFilenameFromMetadata(beatmap)).GetValidFilename());

            if (itemFilename.Length > 200)
                itemFilename = itemFilename.Remove(200);

            if (string.IsNullOrWhiteSpace(itemFilename))
                itemFilename = "beatmap";

            IEnumerable<string> existingExports = exportStorage
                                                  .GetFiles(string.Empty, $"{itemFilename}*.osu")
                                                  .Concat(exportStorage.GetDirectories(string.Empty));

            string filename = NamingUtils.GetNextBestFilename(existingExports, $"{itemFilename}.osu");

            ProgressNotification notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = NotificationsStrings.FileExportOngoing(itemFilename),
            };

            PostNotification?.Invoke(notification);

            try
            {
                var workingBeatmap = beatmapManager.GetWorkingBeatmap(beatmap);
                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset, mods);

                BeatmapExportUtils.ApplyExportMetadata(playableBeatmap, mods);

                using var outStream = exportStorage.CreateFileSafely(filename);
                using var writer = new StreamWriter(outStream, Encoding.UTF8, 1024, true);
                new LegacyBeatmapEncoder(playableBeatmap, workingBeatmap.Skin).Encode(writer);
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

        public void ExportBeatmapSetAsOsz(BeatmapInfo beatmapInfo, RulesetInfo ruleset, IReadOnlyList<Mod> mods)
        {
            var beatmap = realm.Run(r => r.FindWithRefresh<BeatmapInfo>(beatmapInfo.ID)?.Detach());

            if (beatmap?.BeatmapSet == null)
            {
                PostNotification?.Invoke(new SimpleErrorNotification { Text = "Failed to load selected beatmap set for export." });
                return;
            }

            exportArchive(beatmap.BeatmapSet, ruleset, mods, @".osz", useFixedEncoding: true);
        }

        private void exportArchive(BeatmapSetInfo beatmapSet, RulesetInfo ruleset, IReadOnlyList<Mod> mods, string extension, bool useFixedEncoding)
        {
            string itemFilename = beatmapSet.GetDisplayString().GetValidFilename();

            if (itemFilename.Length > LegacyExporter<BeatmapSetInfo>.MAX_FILENAME_LENGTH - extension.Length)
                itemFilename = itemFilename.Remove(LegacyExporter<BeatmapSetInfo>.MAX_FILENAME_LENGTH - extension.Length);

            IEnumerable<string> existingExports = exportStorage
                                                  .GetFiles(string.Empty, $"{itemFilename}*{extension}")
                                                  .Concat(exportStorage.GetDirectories(string.Empty));

            string filename = NamingUtils.GetNextBestFilename(existingExports, $"{itemFilename}{extension}");

            ProgressNotification notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = NotificationsStrings.FileExportOngoing(itemFilename),
            };

            PostNotification?.Invoke(notification);

            try
            {
                using var outStream = exportStorage.CreateFileSafely(filename);

                var zipWriterOptions = new ZipWriterOptions(CompressionType.Deflate)
                {
                    ArchiveEncoding = useFixedEncoding ? ZipArchiveReader.DEFAULT_ENCODING : new ArchiveEncoding()
                };

                using var writer = new ZipWriter(outStream, zipWriterOptions);

                var files = beatmapSet.Files.ToList();
                int exported = 0;

                foreach (var file in files)
                {
                    using var stream = getConvertedFileContents(beatmapSet, file, ruleset, mods);

                    if (stream != null)
                        writer.Write(file.Filename, stream);

                    exported++;
                    notification.Progress = (float)exported / files.Count;
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

        private Stream? getConvertedFileContents(BeatmapSetInfo beatmapSet, INamedFileUsage file, RulesetInfo ruleset, IReadOnlyList<Mod> mods)
        {
            var beatmap = beatmapSet.Beatmaps.SingleOrDefault(b => b.Hash == file.File.Hash);

            if (beatmap == null)
            {
                return file.Filename.EndsWith(@".osu", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : userFileStorage.GetStream(file.File.GetStoragePath());
            }

            if (!beatmap.AllowGameplayWithRuleset(ruleset, true))
                return null;

            var workingBeatmap = beatmapManager.GetWorkingBeatmap(beatmap);
            var playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset, mods);

            BeatmapExportUtils.ApplyExportMetadata(playableBeatmap, mods);

            var stream = new MemoryStream();
            using (var sw = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                new LegacyBeatmapEncoder(playableBeatmap, workingBeatmap.Skin).Encode(sw);

            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        private static string createBeatmapFilenameFromMetadata(BeatmapInfo beatmap)
        {
            var metadata = beatmap.Metadata;
            return $"{metadata.Artist} - {metadata.Title} ({metadata.Author.Username}) [{beatmap.DifficultyName}].osu";
        }
    }
}