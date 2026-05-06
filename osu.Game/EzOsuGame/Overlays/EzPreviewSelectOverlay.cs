// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Ez 资源大图选择覆盖层（GameTheme / NoteSet / Stage）。
    /// </summary>
    public partial class EzPreviewSelectOverlay : OsuFocusedOverlayContainer, IEzResourcePickerOverlay
    {
        private const float cell_w = 108;
        private const float cell_h = 128;

        private FillFlowContainer galleryFlow = null!;
        private OsuSpriteText titleText = null!;
        private OsuSpriteText emptyHint = null!;
        private Box selectedPreviewHighlight = null!;
        private OsuSpriteText selectedLabel = null!;
        private string? pendingSelectionKey;

        private EzResourcePickerDescriptor? session;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private EzResourceProvider resourceProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public EzPreviewSelectOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Black,
                        Alpha = 0.75f
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(0.67f),
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Masking = true,
                        CornerRadius = 10,
                        Child = new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            RowDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(),
                                new Dimension(GridSizeMode.AutoSize)
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 52,
                                        Padding = new MarginPadding { Horizontal = 16 },
                                        Children = new Drawable[]
                                        {
                                            new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Colour = colours.GreySeaFoamDark
                                            },
                                            selectedPreviewHighlight = new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Alpha = 0
                                            },
                                            titleText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                RelativePositionAxes = Axes.Y,
                                                Y = 0.5f,
                                                Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold)
                                            },
                                            new IconButton
                                            {
                                                Anchor = Anchor.CentreRight,
                                                Origin = Anchor.CentreRight,
                                                RelativePositionAxes = Axes.Y,
                                                Y = 0.5f,
                                                Icon = FontAwesome.Solid.Times,
                                                Colour = colours.GreySeaFoamDarker,
                                                Scale = new Vector2(0.85f),
                                                Action = cancelAndHide
                                            }
                                        }
                                    }
                                },
                                new Drawable[]
                                {
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Children = new Drawable[]
                                        {
                                            new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Colour = colours.GreySeaFoamDarker
                                            },
                                            new BasicScrollContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                ClampExtension = 20,
                                                Padding = new MarginPadding(16),
                                                Child = new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Full,
                                                    Spacing = new Vector2(12),
                                                    Children = new Drawable[]
                                                    {
                                                        emptyHint = new OsuSpriteText
                                                        {
                                                            Alpha = 0,
                                                            AlwaysPresent = true,
                                                            Font = OsuFont.GetFont(size: 16),
                                                            Colour = colours.GreySeaFoam
                                                        },
                                                        galleryFlow = new FillFlowContainer
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Full,
                                                            Spacing = new Vector2(12)
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new Drawable[]
                                {
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 60,
                                        Padding = new MarginPadding { Horizontal = 16, Vertical = 12 },
                                        Children = new Drawable[]
                                        {
                                            new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Colour = colours.GreySeaFoamDark
                                            },
                                            selectedLabel = new OsuSpriteText
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                RelativePositionAxes = Axes.Y,
                                                Y = 0.5f,
                                                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold)
                                            },
                                            new FillFlowContainer
                                            {
                                                Anchor = Anchor.CentreRight,
                                                Origin = Anchor.CentreRight,
                                                RelativePositionAxes = Axes.Y,
                                                Y = 0.5f,
                                                AutoSizeAxes = Axes.Both,
                                                Direction = FillDirection.Horizontal,
                                                Spacing = new Vector2(8, 0),
                                                Children = new[]
                                                {
                                                    createFooterButton("取消", cancelAndHide),
                                                    createFooterButton("确认", commitAndHide)
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        public void Present(EzResourcePickerDescriptor descriptor)
        {
            Schedule(() =>
            {
                session = descriptor;
                pendingSelectionKey = descriptor.CurrentKey;
                rebuild();

                if (State.Value == Visibility.Hidden)
                    Show();
            });
        }

        private void rebuild()
        {
            galleryFlow.Clear();

            if (session == null)
                return;

            titleText.Text = session.Title;
            updateSelectionDisplay();

            if (session.Items.Count == 0)
            {
                emptyHint.Text = @"没有可选资源（请检查 EzResources 目录）";
                emptyHint.FadeIn(100);
            }
            else
            {
                emptyHint.FadeOut(100);

                foreach (string key in session.Items)
                {
                    bool selected = string.Equals(pendingSelectionKey, key, StringComparison.Ordinal);
                    galleryFlow.Add(new DelayedLoadWrapper(() => createCell(key, selected), 50)
                    {
                        Size = new Vector2(cell_w, cell_h)
                    });
                }
            }
        }

        private Drawable createCell(string key, bool selected)
        {
            return new PreviewSelectCell(resourceProvider, storage, session!, key, selected, colours, () =>
            {
                pendingSelectionKey = key;
                rebuild();
            });
        }

        private Drawable createFooterButton(string label, Action action)
            => new OsuAnimatedButton
            {
                Width = 84,
                Height = 34,
                Action = action
            }.With(b => b.Add(new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = label,
                Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold)
            }));

        private void commitAndHide()
        {
            if (session != null && !string.IsNullOrEmpty(pendingSelectionKey))
                session.Commit(pendingSelectionKey);

            Hide();
        }

        private void cancelAndHide()
        {
            pendingSelectionKey = session?.CurrentKey;
            Hide();
        }

        private void updateSelectionDisplay()
        {
            if (string.IsNullOrEmpty(pendingSelectionKey))
            {
                selectedPreviewHighlight.Alpha = 0;
                selectedLabel.Text = "当前：未选择";
                return;
            }

            selectedPreviewHighlight.Colour = colours.Yellow.Opacity(0.08f);
            selectedPreviewHighlight.Alpha = 1;
            selectedLabel.Text = $"当前预选：{pendingSelectionKey}";
        }

        protected override void PopIn() => this.FadeIn(200, Easing.OutQuint);

        protected override void PopOut()
        {
            base.PopOut();
            this.FadeOut(200, Easing.OutQuint);
        }

        private sealed partial class PreviewSelectCell : CompositeDrawable
        {
            private readonly EzResourceProvider provider;
            private readonly Storage cellStorage;
            private readonly EzResourcePickerDescriptor descriptor;
            private readonly string key;
            private readonly bool selected;
            private readonly OsuColour osuColour;
            private readonly Action onCommit;

            private Box? hoverBox;

            public PreviewSelectCell(EzResourceProvider provider, Storage cellStorage, EzResourcePickerDescriptor descriptor, string key, bool selected,
                                     OsuColour osuColour, Action onCommit)
            {
                this.provider = provider;
                this.cellStorage = cellStorage;
                this.descriptor = descriptor;
                this.key = key;
                this.selected = selected;
                this.osuColour = osuColour;
                this.onCommit = onCommit;

                Size = new Vector2(cell_w, cell_h);
                Masking = true;
                CornerRadius = 8;

                Anchor = Anchor.TopLeft;
                Origin = Anchor.TopLeft;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var tex = EzResourceDiscovery.TryGetPreviewTexture(provider, cellStorage, descriptor.Category, key);

                hoverBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = osuColour.YellowLighter,
                    Alpha = selected ? 0.25f : 0,
                };

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = osuColour.GreySeaFoamDark },
                    hoverBox,
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(6),
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 4),
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 78,
                                Masking = true,
                                CornerRadius = 6,
                                Children = new Drawable[]
                                {
                                    new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.Black.Opacity(0.35f) },
                                    tex != null
                                        ? new Sprite
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            FillMode = FillMode.Fit,
                                            Texture = tex
                                        }
                                        : new OsuSpriteText
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Text = @"—",
                                            Font = OsuFont.GetFont(size: 28)
                                        }
                                }
                            },
                            new OsuSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold),
                                Text = key
                            }
                        }
                    }
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverBox?.FadeTo(selected ? 0.3f : 0.18f, 150, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverBox?.FadeTo(selected ? 0.25f : 0f, 150, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                onCommit();
                return true;
            }
        }
    }
}
