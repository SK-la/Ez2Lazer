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
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.IO;
using osu.Game.IO.Archives;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Footer;
using osu.Game.Utils;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace osu.Game.EzOsuGame.Statistics
{
    public partial class ExportButton : ScreenFooterButton, IHasPopover
    {
        private readonly BeatmapInfo beatmapInfo;
        private readonly IReadOnlyList<Mod> mods;
        private readonly ScoreInfo? scoreInfo;

        public ExportButton(BeatmapInfo beatmapInfo, IReadOnlyList<Mod> mods, ScoreInfo? scoreInfo = null)
        {
            this.beatmapInfo = beatmapInfo;
            this.mods = mods;
            this.scoreInfo = scoreInfo;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Icon = FontAwesome.Solid.FileExport;
            AccentColour = colours.Orange1;
            Text = ExportStrings.EXPORT_BUTTON;
            TooltipText = ExportStrings.EXPORT_BUTTON_TOOLTIP;
            Action = this.ShowPopover;
        }

        public Popover GetPopover() => new ExportPopover(beatmapInfo, mods, scoreInfo);

        private partial class ExportPopover : OsuPopover
        {
            private readonly BeatmapInfo beatmapInfo;
            private readonly IReadOnlyList<Mod> mods;
            private readonly ScoreInfo? scoreInfo;

            private RulesetInfo exportRuleset => scoreInfo?.Ruleset ?? beatmapInfo.Ruleset;

            [Resolved]
            private BeatmapManager beatmapManager { get; set; } = null!;

            [Resolved]
            private ScoreManager scoreManager { get; set; } = null!;

            [Resolved(canBeNull: true)]
            private INotificationOverlay? notifications { get; set; }

            private Storage exportStorage = null!;
            private Storage userFileStorage = null!;

            public ExportPopover(BeatmapInfo beatmapInfo, IReadOnlyList<Mod> mods, ScoreInfo? scoreInfo)
                : base(false)
            {
                this.beatmapInfo = beatmapInfo;
                this.mods = mods;
                this.scoreInfo = scoreInfo;

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
                            new OsuMenuItem(ExportStrings.EXPORT_OSU, MenuItemType.Standard, () => Task.Run(exportOsu)),
                            new OsuMenuItem(ExportStrings.EXPORT_OSZ, MenuItemType.Standard, () => Task.Run(exportOszLegacy)),
                            new OsuMenuItem(ExportStrings.EXPORT_OLZ, MenuItemType.Standard, () => Task.Run(exportOlz)),
                        }.Concat(createReplayExportItems()).ToArray(),
                        MaxHeight = 375,
                    },
                };
            }

            private IEnumerable<OsuMenuItem> createReplayExportItems()
            {
                if (scoreInfo == null || !scoreInfo.Ruleset.IsLegacyRuleset() ||
                    !scoreInfo.Files.Any(f => f.Filename.EndsWith(".osr", StringComparison.OrdinalIgnoreCase) ||
                                              f.Filename.EndsWith(EzHighPrecisionReplayFrames.EXTENSION, StringComparison.OrdinalIgnoreCase)))
                    return [];

                var exporter = new ScoreReplayExporter(exportStorage, userFileStorage, scoreManager, beatmapManager)
                {
                    PostNotification = notification => notifications?.Post(notification),
                };

                return
                [
                    new OsuMenuItem(ExportStrings.EXPORT_OSR, MenuItemType.Standard, () => Task.Run(() => scoreManager.Export(scoreInfo))),
                    new OsuMenuItem(ExportStrings.EXPORT_EZOSR, MenuItemType.Standard, () => Task.Run(() => exporter.Export(scoreInfo))),
                ];
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
                    var playableBeatmap = workingBeatmap.GetPlayableBeatmap(exportRuleset, mods);

                    BeatmapExportUtils.ApplyExportMetadata(playableBeatmap, mods);

                    using (var outStream = exportStorage.CreateFileSafely(filename))
                    using (var writer = new StreamWriter(outStream, Encoding.UTF8, 1024, true))
                        new LegacyBeatmapEncoder(playableBeatmap, workingBeatmap.Skin, workingBeatmap.Storyboard).Encode(writer);
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
                        ArchiveEncoding = useFixedEncoding ? ZipArchiveReader.DEFAULT_ENCODING : new ArchiveEncoding { Default = Encoding.UTF8, Password = Encoding.UTF8 }
                    };

                    using var writer = new ZipWriter(outStream, zipWriterOptions);

                    var files = beatmapSet.Files.ToList();
                    int exported = 0;

                    foreach (var file in files)
                    {
                        using var stream = getConvertedFileContents(file);

                        if (stream != null)
                            writer.Write(FilesystemSanityCheckHelpers.GetSafeArchiveEntryName(file.Filename), stream);

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

            private Stream? getConvertedFileContents(INamedFileUsage file)
            {
                if (!file.Filename.EndsWith(@".osu", StringComparison.OrdinalIgnoreCase))
                    return userFileStorage.GetStream(file.File.GetStoragePath());

                if (file.File.Hash != beatmapInfo.Hash)
                    return null;

                var workingBeatmap = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(exportRuleset, mods);

                BeatmapExportUtils.ApplyExportMetadata(playableBeatmap, mods);

                var stream = new MemoryStream();
                using (var sw = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                    new LegacyBeatmapEncoder(playableBeatmap, workingBeatmap.Skin, workingBeatmap.Storyboard).Encode(sw);

                stream.Seek(0, SeekOrigin.Begin);
                return stream;
            }

            private static string createBeatmapFilenameFromMetadata(BeatmapInfo beatmap)
            {
                var metadata = beatmap.Metadata;
                return $"{metadata.Artist} - {metadata.Title} ({metadata.Author.Username}) [{beatmap.DifficultyName}].osu";
            }
        }

        private static class ExportStrings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_OSU = new EzLocalizationManager.EzLocalisableString(
                "导出本次游玩谱面为 .osu", "Export played beatmap as .osu");

            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_OSZ = new EzLocalizationManager.EzLocalisableString(
                "导出本次游玩谱包为 .osz", "Export played beatmap set as .osz");

            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_OLZ = new EzLocalizationManager.EzLocalisableString(
                "导出本次游玩谱包为 .olz（lazer）", "Export played beatmap set as .olz (lazer)");

            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_OSR = new EzLocalizationManager.EzLocalisableString(
                "导出本次游玩回放为 .osr（普通精度）", "Export played replay as .osr (standard precision)");

            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_EZOSR = new EzLocalizationManager.EzLocalisableString(
                "导出本次游玩回放为 .ezosr（高精度）", "Export played replay as .ezosr (high precision)");

            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_BUTTON = new EzLocalizationManager.EzLocalisableString(
                "导出", "Export");

            internal static readonly EzLocalizationManager.EzLocalisableString EXPORT_BUTTON_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
                "导出本次游玩的谱面或回放", "Export the played beatmap or replay");
        }
    }
}
