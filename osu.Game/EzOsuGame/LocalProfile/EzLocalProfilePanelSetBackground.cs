// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Overlays;
using osu.Game.Screens.Select;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Song-select style panel background for the local profile drill card.
    /// Unlike <see cref="PanelSetBackground"/>, always loads cover art (no acrylic/carousel deferral).
    /// </summary>
    public partial class EzLocalProfilePanelSetBackground : Container
    {
        private WorkingBeatmap? working;
        private Sprite? sprite;
        private CancellationTokenSource? loadCancellation;

        public WorkingBeatmap? Beatmap
        {
            get => working;
            set
            {
                if (getBackgroundFileHash(working) == getBackgroundFileHash(value))
                    return;

                working = value;

                loadCancellation?.Cancel();
                loadCancellation = null;

                sprite?.Expire();
                sprite = null;

                if (IsLoaded)
                    beginLoad();
            }
        }

        public EzLocalProfilePanelSetBackground()
        {
            RelativeSizeAxes = Axes.Both;
            CornerRadius = Panel.CORNER_RADIUS;
            Masking = true;
            MaskingSmoothness = 2f;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChild = new Container
            {
                Depth = 1,
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientHorizontal(colourProvider.Background3, colourProvider.Background4),
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Shear = new Vector2(0.8f, 0),
                        Children = new[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.Black.Opacity(0.5f),
                                Width = 0.4f,
                            },
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.5f), Color4.Black.Opacity(0.3f)),
                                Width = 0.2f,
                            },
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.3f), Color4.Black.Opacity(0.2f)),
                                Width = 0.45f,
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            beginLoad();
        }

        private void beginLoad()
        {
            if (working == null || loadCancellation != null)
                return;

            loadCancellation = new CancellationTokenSource();

            LoadComponentAsync(new PanelSetBackground.PanelBeatmapBackground(working)
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                FillMode = FillMode.Fill,
            }, s =>
            {
                AddInternal(sprite = s);
                sprite.FadeInFromZero(400, Easing.OutQuint);
            }, loadCancellation.Token);
        }

        private static string? getBackgroundFileHash(WorkingBeatmap? working)
            => working?.BeatmapSetInfo.GetFile(working.Metadata.BackgroundFile)?.File.Hash;
    }
}
