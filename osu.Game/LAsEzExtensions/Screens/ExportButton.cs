// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.LAsEzExtensions.Screens
{
    public partial class ExportButton : GrayButton, IHasPopover
    {
        private readonly BeatmapInfo beatmapInfo;

        public ExportButton(BeatmapInfo beatmapInfo)
            : base(FontAwesome.Solid.Download)
        {
            this.beatmapInfo = beatmapInfo;

            Size = new Vector2(75, 30);
            TooltipText = "Export";
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Action = this.ShowPopover;
        }

        public Popover GetPopover() => new ExportPopover(beatmapInfo);

        private partial class ExportPopover : OsuPopover
        {
            private readonly BeatmapInfo beatmapInfo;

            [Resolved]
            private BeatmapManager beatmapManager { get; set; } = null!;

            public ExportPopover(BeatmapInfo beatmapInfo)
                : base(false)
            {
                this.beatmapInfo = beatmapInfo;

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

            private void exportOszLegacy()
            {
                if (beatmapInfo.BeatmapSet == null)
                    return;

                beatmapManager.ExportLegacy(beatmapInfo.BeatmapSet);
            }

            private void exportOlz()
            {
                if (beatmapInfo.BeatmapSet == null)
                    return;

                beatmapManager.Export(beatmapInfo.BeatmapSet);
            }

            private void exportOsu()
            {
                if (beatmapInfo.BeatmapSet == null)
                    return;

                beatmapManager.ExportLegacy(beatmapInfo);
            }
        }
    }
}
