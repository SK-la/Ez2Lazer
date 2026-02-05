// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
using osu.Game.Extensions;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.IO;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Play;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.LAsEzExtensions.Screens
{
    public partial class ExportButton : GrayButton, IHasPopover
    {
        private readonly BeatmapInfo beatmapInfo;
        private readonly Player? player;

        public ExportButton(BeatmapInfo beatmapInfo, Player? player)
            : base(FontAwesome.Solid.Download)
        {
            this.beatmapInfo = beatmapInfo;
            this.player = player;

            Size = new Vector2(75, 30);
            TooltipText = "Export";
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Action = this.ShowPopover;
        }

        public Popover GetPopover() => new ExportPopover(beatmapInfo, player);

        private partial class ExportPopover : OsuPopover
        {
            private readonly BeatmapInfo beatmapInfo;
            private readonly Player? player;

            [Resolved]
            private BeatmapManager beatmapManager { get; set; } = null!;

            [Resolved]
            private Storage storage { get; set; } = null!;

            public ExportPopover(BeatmapInfo beatmapInfo, Player? player)
                : base(false)
            {
                this.beatmapInfo = beatmapInfo;
                this.player = player;

                Body.CornerRadius = 4;
                AllowableAnchors = new[] { Anchor.TopCentre };
            }

            [BackgroundDependencyLoader]
            private void load()
            {
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

            private void exportOsz()
            {
                // kept for backward-compatibility: export single beatmap file from storage via legacy exporter
                try
                {
                    beatmapManager.ExportLegacy(beatmapInfo);
                }
                catch
                {
                }
            }

            private void exportOszLegacy()
            {
                if (beatmapInfo.BeatmapSet == null)
                    return;

                try
                {
                    // Export whole set as legacy .osz
                    beatmapManager.ExportLegacy(beatmapInfo.BeatmapSet);
                }
                catch
                {
                }
            }

            private void exportOlz()
            {
                if (beatmapInfo.BeatmapSet == null)
                    return;

                try
                {
                    // Export whole set as lazer .olz
                    beatmapManager.Export(beatmapInfo.BeatmapSet);
                }
                catch
                {
                }
            }

            private void exportOsu()
            {
                // If gameplay beatmap exists (post-convert + mods), export that one directly.
                var gameplayBeatmap = player?.GameplayState?.Beatmap;

                if (gameplayBeatmap != null)
                {
                    var exportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");

                    string itemFilename = exportFilenameFromMetadata(gameplayBeatmap.BeatmapInfo);
                    const int max_filename_len = 200;
                    if (itemFilename.Length > max_filename_len)
                        itemFilename = itemFilename.Remove(max_filename_len);

                    var existing = exportStorage.GetFiles(string.Empty, $"{itemFilename}*.osu").Concat(exportStorage.GetDirectories(string.Empty));
                    string filename = NamingUtils.GetNextBestFilename(existing, $"{itemFilename}.osu");

                    var notification = new ProgressNotification
                    {
                        State = ProgressNotificationState.Active,
                        Text = NotificationsStrings.FileExportOngoing(itemFilename),
                    };

                    beatmapManager.PostNotification?.Invoke(notification);

                    try
                    {
                        using var outStream = exportStorage.CreateFileSafely(filename);
                        using var sw = new StreamWriter(outStream, Encoding.UTF8, 1024, true);
                        new LegacyBeatmapEncoder(gameplayBeatmap, null).Encode(sw);

                        notification.CompletionText = NotificationsStrings.FileExportFinished(itemFilename);
                        notification.CompletionClickAction = () => exportStorage.PresentFileExternally(filename);
                        notification.State = ProgressNotificationState.Completed;
                        beatmapManager.PostNotification?.Invoke(notification);
                    }
                    catch
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        beatmapManager.PostNotification?.Invoke(notification);

                        // cleanup attempt
                        try
                        {
                             /* best-effort deletion not available without filename reference scope */
                        }
                        catch { }

                        throw;
                    }

                    return;
                }

                // Fallback: export stored beatmap file via BeatmapManager (will export .osu from storage)
                try
                {
                    beatmapManager.ExportLegacy(beatmapInfo);
                }
                catch
                {
                }
            }

            private static string exportFilenameFromMetadata(BeatmapInfo b)
            {
                var metadata = b.Metadata;
                return $"{metadata.Artist} - {metadata.Title} ({metadata.Author.Username}) [{b.DifficultyName}]".GetValidFilename();
            }
        }
    }
}
