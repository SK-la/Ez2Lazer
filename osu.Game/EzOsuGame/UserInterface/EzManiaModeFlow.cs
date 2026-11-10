// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.EzOsuGame.UI;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Input;

namespace osu.Game.EzOsuGame.UserInterface
{
    public partial class EzManiaModeFlow : CompositeDrawable
    {
        public float BlockMinWidth { get; set; } = 110f;

        private const float min_gap = 10f;
        private const float label_value_spacing = 4f;
        private const float stacked_row_spacing = 2f;
        private const float block_content_horizontal_padding = 8f;
        private static readonly float block_row_height = OsuFont.Style.Caption1.Size;

        private readonly GridContainer layoutGrid;
        private readonly AltEnumModeBlock<EzEnumJudgeMode> blockA;
        private readonly AltEnumModeBlock<EzEnumHealthMode> blockB;
        private readonly OsuSpriteText widthMeasureText;

        private float availableWidth;
        private bool displayedHorizontal = true;
        private float lastAppliedWidth = -1f;

        private Bindable<EzEnumJudgeMode> maniaJudgeModeBindable = null!;
        private Bindable<EzEnumHealthMode> maniaHealthModeBindable = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        /// <summary>
        /// Horizontal layout total width: block A + min gap + block B.
        /// </summary>
        public float HorizontalLayoutWidth => VerticalLayoutWidth * 2 + min_gap;

        /// <summary>
        /// Vertical layout total width: single block width.
        /// </summary>
        public float VerticalLayoutWidth { get; private set; }

        public EzManiaModeFlow()
        {
            Alpha = 0f;

            blockA = new AltEnumModeBlock<EzEnumJudgeMode>("Hit", EzScoreInfoExtensions.GetJudgeModeDisplayName);
            blockB = new AltEnumModeBlock<EzEnumHealthMode>("HP", EzScoreInfoExtensions.GetHealthModeDisplayName);

            InternalChildren = new Drawable[]
            {
                layoutGrid = new GridContainer
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Content = createHorizontalContent(),
                    ColumnDimensions = createHorizontalColumns(0),
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                },
                widthMeasureText = new OsuSpriteText
                {
                    Alpha = 0,
                    AlwaysPresent = true,
                    Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            maniaJudgeModeBindable = ezConfig.GetBindable<EzEnumJudgeMode>(Ez2Setting.ManiaJudgeMode);
            maniaHealthModeBindable = ezConfig.GetBindable<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);

            blockA.Attach(maniaJudgeModeBindable);
            blockB.Attach(maniaHealthModeBindable);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => updateDisplay(), true);
            maniaJudgeModeBindable.BindValueChanged(_ => updateDisplay(), true);
            maniaHealthModeBindable.BindValueChanged(_ => updateDisplay(), true);

            updateBlockWidth();
            // Defer layout application to the scheduler to avoid interacting with
            // the calling measurement pass. This prevents measurement -> layout
            // loops when callers perform measurements and immediately call
            // SetAvailableWidth.
            Scheduler.AddOnce(updateLayout);
        }

        // The flow measures its available width directly from its parent each
        // frame in `Update()`. Previously an external caller would provide the
        // available width via `SetAvailableWidth`, which introduced synchronous
        // measurement->layout coupling and could lead to feedback loops. By
        // owning the measurement we simplify usage and avoid needing an
        // external API.

        protected override void Update()
        {
            base.Update();

            float parentAvailable;
            if (Parent is Container c)
                parentAvailable = Math.Max(0, c.DrawWidth - c.Padding.TotalHorizontal);
            else
                parentAvailable = DrawWidth;

            const float epsilon = 0.05f;
            if (Math.Abs(availableWidth - parentAvailable) < epsilon)
                return;

            availableWidth = parentAvailable;
            Scheduler.AddOnce(updateLayout);
        }

        private void updateDisplay()
        {
            bool isMania = ruleset.Value.OnlineID == 3;

            if (isMania)
            {
                blockA.SetValues("Hit", EzScoreInfoExtensions.GetJudgeModeDisplayName((int)maniaJudgeModeBindable.Value));
                blockB.SetValues("HP", EzScoreInfoExtensions.GetHealthModeDisplayName((int)maniaHealthModeBindable.Value));

                Show();
                this.FadeIn(200, Easing.OutQuint);
            }
            else
            {
                this.FadeOut(200, Easing.OutQuint);
                Hide();
            }

            updateBlockWidth();
        }

        private void updateBlockWidth()
        {
            float maxContentWidth = 0f;

            widthMeasureText.Text = "Hit";
            float hitLabelWidth = widthMeasureText.DrawWidth;
            widthMeasureText.Text = "HP";
            float hpLabelWidth = widthMeasureText.DrawWidth;
            float maxLabelWidth = Math.Max(hitLabelWidth, hpLabelWidth);

            foreach (EzEnumJudgeMode mode in Enum.GetValues<EzEnumJudgeMode>())
            {
                widthMeasureText.Text = EzScoreInfoExtensions.GetJudgeModeDisplayName((int)mode);
                maxContentWidth = Math.Max(maxContentWidth, maxLabelWidth + label_value_spacing + widthMeasureText.DrawWidth + block_content_horizontal_padding);
            }

            foreach (EzEnumHealthMode mode in Enum.GetValues<EzEnumHealthMode>())
            {
                widthMeasureText.Text = EzScoreInfoExtensions.GetHealthModeDisplayName((int)mode);
                maxContentWidth = Math.Max(maxContentWidth, maxLabelWidth + label_value_spacing + widthMeasureText.DrawWidth + block_content_horizontal_padding);
            }

            if (maxContentWidth <= 0)
            {
                SchedulerAfterChildren.AddOnce(updateBlockWidth);
                return;
            }

            float newBlockWidth = Math.Max(BlockMinWidth, maxContentWidth);

            if (newBlockWidth == VerticalLayoutWidth)
                return;

            VerticalLayoutWidth = newBlockWidth;
            blockA.Width = VerticalLayoutWidth;
            blockB.Width = VerticalLayoutWidth;
            // When the block width changes, schedule a layout recalculation.
            Scheduler.AddOnce(updateLayout);
        }

        private void updateLayout()
        {
            if (VerticalLayoutWidth <= 0)
                return;

            bool horizontal = availableWidth >= HorizontalLayoutWidth - 20f;

            // Avoid reapplying layout if nothing significant changed. This
            // reduces churn in the draw/measure passes.
            float targetWidth = horizontal ? HorizontalLayoutWidth : VerticalLayoutWidth;
            if (displayedHorizontal == horizontal && Math.Abs(lastAppliedWidth - targetWidth) < 0.1f)
                return;

            if (horizontal != displayedHorizontal)
            {
                if (horizontal)
                    applyHorizontalLayout();
                else
                    applyVerticalLayout();

                displayedHorizontal = horizontal;
            }
            else if (displayedHorizontal)
                layoutGrid.ColumnDimensions = createHorizontalColumns(VerticalLayoutWidth);
            else
                layoutGrid.ColumnDimensions = new[] { new Dimension(GridSizeMode.Absolute, VerticalLayoutWidth) };

            applyComponentSize();
            lastAppliedWidth = targetWidth;
        }

        private void applyHorizontalLayout()
        {
            layoutGrid.RowDimensions = new[]
            {
                new Dimension(GridSizeMode.AutoSize)
            };
            layoutGrid.ColumnDimensions = createHorizontalColumns(VerticalLayoutWidth);
            layoutGrid.Content = createHorizontalContent();
        }

        private void applyVerticalLayout()
        {
            layoutGrid.RowDimensions = new[]
            {
                new Dimension(GridSizeMode.AutoSize),
                new Dimension(GridSizeMode.Absolute, stacked_row_spacing),
                new Dimension(GridSizeMode.AutoSize),
            };
            layoutGrid.ColumnDimensions = new[]
            {
                new Dimension(GridSizeMode.Absolute, VerticalLayoutWidth)
            };
            layoutGrid.Content = createVerticalContent();
        }

        private void applyComponentSize()
        {
            if (displayedHorizontal)
            {
                Width = HorizontalLayoutWidth;
                Height = block_row_height;
            }
            else
            {
                Width = VerticalLayoutWidth;
                Height = block_row_height * 2 + stacked_row_spacing;
            }

            layoutGrid.Width = Width;
            layoutGrid.Height = Height;
        }

        private Drawable[][] createHorizontalContent() => new[]
        {
            new[] { blockA, Empty(), blockB },
        };

        private Drawable[][] createVerticalContent() => new[]
        {
            new Drawable[] { blockA },
            new[] { Empty() },
            new Drawable[] { blockB },
        };

        private static Dimension[] createHorizontalColumns(float width) => new[]
        {
            new Dimension(GridSizeMode.Absolute, width),
            new Dimension(GridSizeMode.Absolute, min_gap),
            new Dimension(GridSizeMode.Absolute, width),
        };

        private partial class AltEnumModeBlock<TEnum> : OsuClickableContainer, IHasPopover
            where TEnum : struct, Enum
        {
            private readonly MarginPadding blockPadding = new MarginPadding { Horizontal = 4, Vertical = 1 };

            private readonly string fixedLabel;
            private readonly Func<int, string> getDisplayName;

            private Bindable<TEnum>? bindable;
            private bool altHighlightActive;

            private OverlayColourProvider colourProvider = null!;
            private Box background = null!;
            private OsuSpriteText labelText = null!;
            private TruncatingSpriteText valueText = null!;

            public AltEnumModeBlock(string fixedLabel, Func<int, string> getDisplayName)
            {
                this.fixedLabel = fixedLabel;
                this.getDisplayName = getDisplayName;

                Height = block_row_height;
            }

            public void Attach(Bindable<TEnum> current) => bindable = current;

            public void SetValues(string label, string value)
            {
                labelText.Text = label;
                valueText.Text = value;
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (!altHighlightActive || !e.CurrentState.Keyboard.Keys.IsPressed(Key.LAlt))
                    return false;

                this.ShowPopover();
                return true;
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (!altHighlightActive)
                    return false;

                refreshColours();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                refreshColours();
                base.OnHoverLost(e);
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                this.colourProvider = colourProvider;

                CornerRadius = EzUIStyleV1.CORNER_RADIUS_CHIP;
                Masking = true;

                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.Transparent,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(label_value_spacing),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Padding = blockPadding,
                            Children = new Drawable[]
                            {
                                labelText = new OsuSpriteText
                                {
                                    Text = fixedLabel,
                                    Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Colour = colourProvider.Content2,
                                },
                                valueText = new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Font = OsuFont.Style.Caption1,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Colour = colourProvider.Content1,
                                },
                            },
                        },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                EzDisplayTagAltHighlight.ActiveChanged += onAltHighlightChanged;
                onAltHighlightChanged(EzDisplayTagAltHighlight.Active);
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                if (isDisposing)
                    EzDisplayTagAltHighlight.ActiveChanged -= onAltHighlightChanged;
            }

            public Popover GetPopover()
            {
                if (bindable == null)
                    throw new InvalidOperationException();

                return new ModeEnumPopover(bindable, getDisplayName);
            }

            private void onAltHighlightChanged(bool active)
            {
                if (altHighlightActive == active)
                    return;

                altHighlightActive = active;
                refreshColours();
            }

            private void refreshColours()
            {
                if (!IsLoaded)
                    return;

                Colour4 backgroundColour = !altHighlightActive
                    ? Colour4.Transparent
                    : IsHovered
                        ? colourProvider.Background4
                        : colourProvider.Background3;

                background.FadeColour(backgroundColour, 200, Easing.OutQuint);
                labelText.FadeColour(altHighlightActive ? colourProvider.Content1 : colourProvider.Content2, 200, Easing.OutQuint);
            }

            private partial class ModeEnumPopover : OsuPopover
            {
                public ModeEnumPopover(Bindable<TEnum> current, Func<int, string> getDisplayName)
                    : base(false)
                {
                    Body.CornerRadius = 4;
                    AllowableAnchors = new[] { Anchor.BottomCentre, Anchor.TopCentre };

                    Children = new[]
                    {
                        new OsuMenu(Direction.Vertical, true)
                        {
                            Items = Enum.GetValues<TEnum>()
                                        .Select(mode => new OsuMenuItem(
                                            getDisplayName(Convert.ToInt32(mode)),
                                            MenuItemType.Standard,
                                            () => current.Value = mode))
                                        .ToArray(),
                            MaxHeight = 375,
                        },
                    };
                }
            }
        }
    }
}
