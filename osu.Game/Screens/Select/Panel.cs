// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.EzOsuGame.Acrylic;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.UI;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public abstract partial class Panel : PoolableDrawable, ICarouselPanel, IHasContextMenu
    {
        public const float CORNER_RADIUS = 10;

        private const float active_x_offset = 25f;

        protected const float DURATION = 400;

        protected float PanelXOffset { get; init; }

        private Container backgroundContainer = null!;
        private Container iconContainer = null!;

        private Drawable activationFlash = null!;
        private Drawable hoverLayer = null!;

        private Drawable keyboardSelectionLayer = null!;

        private PulsatingBox selectionLayer = null!;
        private SelectionGlowPulser selectionGlowPulser = null!;

        /// <summary>
        /// Full-bleed acrylic glass (N). Visibility is driven by <see cref="acrylicUiEnabled"/>;
        /// subclasses hide classic colour overlays via <see cref="EzAcrylicOverlayAlpha.BindHiddenWhenAcrylic"/>.
        /// </summary>
        protected EzAcrylicPanelBackground PanelGlass { get; private set; } = null!;

        public Container TopLevelContent { get; private set; } = null!;

        private Container contentPaddingContainer = null!;
        protected Container Content { get; private set; } = null!;

        public Drawable Background
        {
            set => backgroundContainer.Child = value;
        }

        public Drawable Icon
        {
            set => iconContainer.Child = value;
        }

        private Color4? accentColour;

        public Color4? AccentColour
        {
            get => accentColour;
            set
            {
                if (value == accentColour)
                    return;

                accentColour = value;
                updateAccentColour();
            }
        }

        public sealed override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            if (item == null)
                return TopLevelContent.ReceivePositionalInputAt(screenSpacePos);

            var inputRectangle = TopLevelContent.DrawRectangle;

            // Cover the gaps introduced by the spacing between panels so that user mis-aims don't result in no-ops.
            inputRectangle = inputRectangle.Inflate(new MarginPadding
            {
                Top = item.CarouselInputLenienceAbove,
                Bottom = item.CarouselInputLenienceBelow,
            });

            return inputRectangle.Contains(TopLevelContent.ToLocalSpace(screenSpacePos));
        }

        [Resolved]
        private BeatmapCarousel? carousel { get; set; }

        private Bindable<bool> acrylicUiEnabled = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuColour colours, Ez2ConfigManager ezConfig)
        {
            acrylicUiEnabled = ezConfig.GetBindable<bool>(Ez2Setting.AcrylicUiEnabled);
            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;

            RelativeSizeAxes = Axes.X;
            Height = CarouselItem.DEFAULT_HEIGHT;

            InternalChild = TopLevelContent = new Container
            {
                Masking = true,
                CornerRadius = CORNER_RADIUS,
                RelativeSizeAxes = Axes.Both,
                X = CORNER_RADIUS,
                Children = new[]
                {
                    // Full-bleed under accent strip + Content so Content's left corner radius
                    // reveals the same glass (no (| crescent seam against a strip-only background).
                    PanelGlass = new EzAcrylicPanelBackground(EzAcrylicStyle.PanelVeil, EzAcrylicStyle.PanelFrameBufferScale)
                    {
                        AcrylicCaptureVisible = true,
                        Alpha = 0,
                    },
                    backgroundContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    iconContainer = new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                    },
                    contentPaddingContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = Content = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            CornerRadius = CORNER_RADIUS,
                            Masking = true,
                        },
                    },
                    hoverLayer = new Box
                    {
                        Alpha = 0,
                        Colour = colours.Blue.Opacity(0.1f),
                        Blending = BlendingParameters.Additive,
                        RelativeSizeAxes = Axes.Both,
                    },
                    selectionLayer = new PulsatingBox
                    {
                        Alpha = 0,
                        RelativeSizeAxes = Axes.Both,
                        Width = 0.8f,
                        Blending = BlendingParameters.Additive,
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                    },
                    selectionGlowPulser = new SelectionGlowPulser
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    keyboardSelectionLayer = new Box
                    {
                        Alpha = 0,
                        Colour = ColourInfo.GradientHorizontal(colourProvider.Highlight1.Opacity(0.1f), colourProvider.Highlight1.Opacity(0.4f)),
                        Blending = BlendingParameters.Additive,
                        RelativeSizeAxes = Axes.Both,
                    },
                    activationFlash = new Box
                    {
                        Colour = Color4.White.Opacity(0.4f),
                        Blending = BlendingParameters.Additive,
                        Alpha = 0f,
                        RelativeSizeAxes = Axes.Both,
                    },
                    new HoverSounds(),
                }
            };

            selectionGlowPulser.GlowTarget = TopLevelContent;
            selectionGlowPulser.GetGlowColour = () => accentColour ?? Color4Extensions.FromHex(@"4EBFFF");
            // Acrylic-only outer glow pulse; classic UI uses PulsatingBox instead.
            selectionGlowPulser.ShouldPulse = () => acrylicUiEnabled.Value && (Expanded.Value || Selected.Value);
        }

        public partial class PulsatingBox : BeatSyncedContainer
        {
            public int FlashOffset;

            private readonly Box box;

            public PulsatingBox()
            {
                EarlyActivationMilliseconds = 40;

                InternalChildren = new Drawable[]
                {
                    box = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };
            }

            protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
            {
                base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);

                if (beatIndex % Math.Pow(2, FlashOffset) != 0)
                    return;

                double length = timingPoint.BeatLength;

                while (length < 250)
                    length *= 2;

                box
                    .FadeTo(0.5f, 40, Easing.Out)
                    .Then()
                    .FadeTo(0.2f, length, Easing.Out);
            }
        }

        /// <summary>
        /// BPM-syncs an outer glow on <see cref="GlowTarget"/> (acrylic selection feedback).
        /// </summary>
        public partial class SelectionGlowPulser : BeatSyncedContainer
        {
            public int FlashOffset;

            public Container GlowTarget { get; set; } = null!;

            public Func<Color4>? GetGlowColour { get; set; }

            public Func<bool>? ShouldPulse { get; set; }

            public SelectionGlowPulser()
            {
                EarlyActivationMilliseconds = 40;
            }

            protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
            {
                base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);

                if (ShouldPulse?.Invoke() != true)
                    return;

                if (beatIndex % Math.Pow(2, FlashOffset) != 0)
                    return;

                double length = timingPoint.BeatLength;

                while (length < 250)
                    length *= 2;

                var colour = GetGlowColour?.Invoke() ?? Color4.White;

                GlowTarget
                    .FadeEdgeEffectTo(colour.Opacity(0.25f), 40, Easing.Out)
                    .Then()
                    .FadeEdgeEffectTo(colour.Opacity(0.4f), length, Easing.Out);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Expanded.BindValueChanged(_ =>
            {
                updateSelectedState();
                updateXOffset();
            });

            Selected.BindValueChanged(_ =>
            {
                updateSelectedState();
                updateXOffset();
            }, true);

            KeyboardSelected.BindValueChanged(selected =>
            {
                if (selected.NewValue)
                {
                    keyboardSelectionLayer.FadeIn(80, Easing.Out)
                                          .Then()
                                          .FadeTo(0.5f, 2000, Easing.OutQuint);
                }
                else
                    keyboardSelectionLayer.FadeOut(1000, Easing.OutQuint);

                updateXOffset();
            }, true);

            // ON: show glass N. OFF: hide N. Classic overlays are hidden separately per-subclass.
            acrylicUiEnabled.BindValueChanged(e =>
            {
                PanelGlass.ClearTransforms();
                PanelGlass.Alpha = e.NewValue ? 1f : 0f;
                updateSelectedState(animated: false);
            }, true);
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            // Slightly offset the flash animation based on the panel depth.
            // This assumes a minimum depth of -2 (groups).
            selectionLayer.FlashOffset = -Item!.DepthLayer;
            selectionGlowPulser.FlashOffset = -Item!.DepthLayer;

            updateAccentColour();

            updateXOffset(animated: false);
            updateSelectedState(animated: false);

            this.FadeIn(DURATION, Easing.OutQuint);
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            Hide();

            // Important to set this to null to handle reuse scenarios correctly, see `Item` implementation.
            item = null;
        }

        protected override bool OnClick(ClickEvent e)
        {
            // Item may be set to null before actual `FreeAfterUse`.
            // This is because Carousel knows to do this ahead of time and let the drawable fade/animate away.
            // See https://github.com/ppy/osu/blob/033e13cb3b79e6195ddcd9f659b04095aa52fd2f/osu.Game/Graphics/Carousel/Carousel.cs#L1132-L1135.
            if (item != null)
                carousel?.Activate(item);

            return true;
        }

        private void updateAccentColour()
        {
            var backgroundColour = accentColour ?? Color4.White;

            selectionLayer.Colour = ColourInfo.GradientHorizontal(backgroundColour.Opacity(0), backgroundColour.Opacity(0.3f));

            updateSelectedState(animated: false);
        }

        private void updateSelectedState(bool animated = true)
        {
            bool selectedOrExpanded = Expanded.Value || Selected.Value;
            double duration = animated ? DURATION : 0;

            if (acrylicUiEnabled.Value)
            {
                // Acrylic: hollow outer glow on selection; no drop shadows (they flicker on blur).
                selectionLayer.FadeOut(animated ? 200 : 0, Easing.OutQuint);

                if (selectedOrExpanded)
                {
                    TopLevelContent.EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Glow,
                        Radius = 3,
                        Roundness = 1f,
                        Hollow = true,
                    };
                    var edgeEffectColour = accentColour ?? Color4Extensions.FromHex(@"4EBFFF");
                    TopLevelContent.FadeEdgeEffectTo(edgeEffectColour.Opacity(0.5f), duration, Easing.OutQuint);
                }
                else
                {
                    TopLevelContent.EdgeEffect = new EdgeEffectParameters();
                    TopLevelContent.FadeEdgeEffectTo(Color4.Transparent, duration, Easing.OutQuint);
                }
            }
            else
            {
                // Classic: right-edge BPM flash + coloured/drop shadows.
                var edgeEffectColour = accentColour ?? Color4Extensions.FromHex(@"4EBFFF");

                if (selectedOrExpanded)
                {
                    TopLevelContent.EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Radius = 2f,
                        Hollow = true,
                    };
                }
                else
                {
                    TopLevelContent.EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Radius = 4f,
                        Hollow = true,
                        Offset = new Vector2(0f, 1f),
                    };
                }

                TopLevelContent.FadeEdgeEffectTo(selectedOrExpanded ? edgeEffectColour.Opacity(0.8f) : Color4.Black.Opacity(0.2f), duration, Easing.OutQuint);

                if (selectedOrExpanded)
                    selectionLayer.FadeIn(100, Easing.OutQuint);
                else
                    selectionLayer.FadeOut(200, Easing.OutQuint);
            }
        }

        private void updateXOffset(bool animated = true)
        {
            float x = PanelXOffset + CORNER_RADIUS;

            if (!Expanded.Value && !Selected.Value)
            {
                if (this is PanelBeatmap || this is PanelBeatmapStandalone)
                    x += active_x_offset * 2;
                else
                    x += active_x_offset * 4;
            }

            if (!KeyboardSelected.Value)
                x += active_x_offset;

            TopLevelContent.MoveToX(x, animated ? DURATION : 0, Easing.OutQuint);
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverLayer.FadeIn(100, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverLayer.FadeOut(1000, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        protected override void Update()
        {
            base.Update();
            contentPaddingContainer.Padding = contentPaddingContainer.Padding with { Left = iconContainer.DrawWidth };
        }

        /// <summary>
        /// Under acrylic, keep an accent column that fades rightward into the frosted card so the
        /// icon-strip join and Content corner radius do not read as a hard seam.
        /// </summary>
        protected void ApplyAcrylicIconStripBackground(Box background, bool acrylicEnabled, Color4 stripColour)
        {
            if (acrylicEnabled)
            {
                background.RelativeSizeAxes = Axes.Y;
                background.Height = 1;
                background.Width = Math.Max(iconContainer.DrawWidth, 1f) + CORNER_RADIUS;
                background.Colour = ColourInfo.GradientHorizontal(stripColour.Opacity(0.8f), stripColour.Opacity(0).Darken(0.8f));
            }
            else
            {
                if (background.RelativeSizeAxes != Axes.Both)
                {
                    background.RelativeSizeAxes = Axes.Both;
                    background.Width = 1;
                }

                background.Colour = stripColour;
            }
        }

        public abstract MenuItem[]? ContextMenuItems { get; }

        #region ICarouselPanel

        private CarouselItem? item;

        public CarouselItem? Item
        {
            get => item;
            set
            {
                if (ReferenceEquals(item, value))
                    return;

                // If a new item is set and we already have an item, this is a special case of reuse.
                // See https://github.com/ppy/osu/blob/033e13cb3b79e6195ddcd9f659b04095aa52fd2f/osu.Game/Graphics/Carousel/Carousel.cs#L1071
                // To keep things simple, assume that we need to do a full refresh.
                //
                // In the future, this could be more contextual and check whether the associated model has actually changed.
                if (item != null && value != null)
                {
                    item = value;
                    PrepareForUse();
                }
                else
                    item = value;
            }
        }

        public BindableBool Selected { get; } = new BindableBool();
        public BindableBool Expanded { get; } = new BindableBool();
        public BindableBool KeyboardSelected { get; } = new BindableBool();

        public double DrawYPosition { get; set; }

        public virtual void Activated()
        {
            // Under acrylic, keep selection feedback to the hollow outer rim only —
            // a full-panel white flash reads as the whole card brightening.
            if (acrylicUiEnabled.Value)
                return;

            activationFlash.FadeOutFromOne(1000, Easing.OutQuint);
        }

        #endregion
    }
}
