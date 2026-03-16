// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.IO.Archives;
using osu.Game.IO;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;
using osuTK;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace osu.Game.EzOsuGame.Statistics
{
    public partial class ExportButton : GrayButton, IHasPopover
    {
        private readonly BeatmapInfo beatmapInfo;
        private readonly IReadOnlyList<Mod> mods;

        public ExportButton(BeatmapInfo beatmapInfo, IReadOnlyList<Mod> mods)
            : base(FontAwesome.Solid.Download)
        {
            this.beatmapInfo = beatmapInfo;
            this.mods = mods;

            Size = new Vector2(75, 30);
            TooltipText = "Export";
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Action = this.ShowPopover;
        }

        public Popover GetPopover() => new ExportPopover(beatmapInfo, mods);

        private partial class ExportPopover : OsuPopover
        {
            private readonly BeatmapInfo beatmapInfo;
            private readonly IReadOnlyList<Mod> mods;

            [Resolved]
            private BeatmapManager beatmapManager { get; set; } = null!;

            [Resolved(canBeNull: true)]
            private INotificationOverlay? notifications { get; set; }

            private Storage exportStorage = null!;
            private Storage userFileStorage = null!;

            public ExportPopover(BeatmapInfo beatmapInfo, IReadOnlyList<Mod> mods)
                : base(false)
            {
                this.beatmapInfo = beatmapInfo;
                this.mods = mods;

                Body.CornerRadius = 4;
                AllowableAnchors = new[] { Anchor.TopCentre };
            }

            [BackgroundDependencyLoader]
            private void load(Storage storage)
            {
                exportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");
                userFileStorage = storage.GetStorageForDirectory(@"files");

                Children = new[]
                {
                    new OsuMenu(Direction.Vertical, true)
                    {
                        Items = new[]
                        {
                            new OsuMenuItem("Export as .osu", MenuItemType.Standard, () => Task.Run(exportOsu)),
                            new OsuMenuItem("Export as .osz (legacy)", MenuItemType.Standard, () => Task.Run(exportOszLegacy)),
                            new OsuMenuItem("Export as .olz (lazer)", MenuItemType.Standard, () => Task.Run(exportOlz)),
                        },
                        MaxHeight = 375,
                    },
                };
            }

            private void exportOszLegacy()
            {
                if (beatmapInfo.BeatmapSet == null)
                    return;

                exportArchive(beatmapInfo.BeatmapSet, @".osz", useFixedEncoding: true);
            }

            private void exportOlz()
            {
                if (beatmapInfo.BeatmapSet == null)
                    return;

                exportArchive(beatmapInfo.BeatmapSet, @".olz", useFixedEncoding: false);
            }

            private void exportOsu()
            {
                if (beatmapInfo.BeatmapSet == null)
                    return;

                string itemFilename = Path.GetFileNameWithoutExtension((beatmapInfo.File?.Filename ?? createBeatmapFilenameFromMetadata(beatmapInfo)).GetValidFilename());

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

                notifications?.Post(notification);

                try
                {
                    var workingBeatmap = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                    var playableBeatmap = workingBeatmap.GetPlayableBeatmap(beatmapInfo.Ruleset, mods);

                    using (var outStream = exportStorage.CreateFileSafely(filename))
                    using (var writer = new StreamWriter(outStream, Encoding.UTF8, 1024, true))
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

            private void exportArchive(BeatmapSetInfo beatmapSet, string extension, bool useFixedEncoding)
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

                notifications?.Post(notification);

                try
                {
                    using var outStream = exportStorage.CreateFileSafely(filename);

                    var zipWriterOptions = new ZipWriterOptions(CompressionType.Deflate)
                    {
                        ArchiveEncoding = useFixedEncoding ? ZipArchiveReader.DEFAULT_ENCODING : new ArchiveEncoding(Encoding.UTF8, Encoding.UTF8)
                    };

                    using var writer = new ZipWriter(outStream, zipWriterOptions);

                    var files = beatmapSet.Files.ToList();
                    int exported = 0;

                    foreach (var file in files)
                    {
                        using var stream = getConvertedFileContents(beatmapSet, file);

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

            private Stream? getConvertedFileContents(BeatmapSetInfo beatmapSet, INamedFileUsage file)
            {
                var beatmap = beatmapSet.Beatmaps.SingleOrDefault(b => b.Hash == file.File.Hash);

                // Do not include any original beatmap .osu in archive.
                if (beatmap == null)
                {
                    return file.Filename.EndsWith(@".osu", StringComparison.OrdinalIgnoreCase)
                        ? null
                        : userFileStorage.GetStream(file.File.GetStoragePath());
                }

                var workingBeatmap = beatmapManager.GetWorkingBeatmap(beatmap);
                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(beatmap.Ruleset, mods);

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
}
