// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Layout;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.EzOsuGame.UserInterface
{
    public partial class EzManiaModeFlow : CompositeDrawable
    {
        public float BlockMinWidth { get; set; } = 110f;

        private const float min_gap = 10f;
        private const float label_value_spacing = 4f;
        private const float layout_hysteresis = 20f;
        private const float stacked_row_spacing = 2f;
        private static readonly float block_row_height = OsuFont.Style.Caption1.Size;

        private readonly GridContainer layoutGrid;
        private readonly ModeBlock blockA;
        private readonly ModeBlock blockB;
        private readonly OsuSpriteText widthMeasureText;

        private readonly LayoutValue drawSizeLayout = new LayoutValue(Invalidation.DrawSize);

        private float availableWidth;
        private bool displayedHorizontal = true;

        private IBindable<EzEnumHitMode> maniaHitModeBindable = null!;
        private IBindable<EzEnumHealthMode> maniaHealthModeBindable = null!;

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

            blockA = new ModeBlock();
            blockB = new ModeBlock();

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

            AddLayout(drawSizeLayout);
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            maniaHitModeBindable = ezConfig.GetBindable<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            maniaHealthModeBindable = ezConfig.GetBindable<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => updateDisplay(), true);
            maniaHitModeBindable.BindValueChanged(_ => updateDisplay(), true);
            maniaHealthModeBindable.BindValueChanged(_ => updateDisplay(), true);

            updateBlockWidth();
        }

        public void SetAvailableWidth(float width)
        {
            if (availableWidth == width)
                return;

            availableWidth = width;
            drawSizeLayout.Invalidate();
        }

        protected override void Update()
        {
            base.Update();

            if (!drawSizeLayout.IsValid)
            {
                updateLayout();
                drawSizeLayout.Validate();
            }
        }

        private void updateDisplay()
        {
            bool isMania = ruleset.Value.OnlineID == 3;

            if (isMania)
            {
                blockA.SetValues("Hit", EzManiaScoreModeExtensions.GetHitModeDisplayName((int)maniaHitModeBindable.Value));
                blockB.SetValues("HP", EzManiaScoreModeExtensions.GetHealthModeDisplayName((int)maniaHealthModeBindable.Value));

                Show();
                this.FadeIn(200, Easing.OutQuint);
            }
            else
            {
                this.FadeOut(200, Easing.OutQuint);
                Hide();
            }

            updateBlockWidth();
            drawSizeLayout.Invalidate();
        }

        private void updateBlockWidth()
        {
            float maxContentWidth = 0f;

            widthMeasureText.Text = "Hit";
            float hitLabelWidth = widthMeasureText.DrawWidth;
            widthMeasureText.Text = "HP";
            float hpLabelWidth = widthMeasureText.DrawWidth;
            float maxLabelWidth = Math.Max(hitLabelWidth, hpLabelWidth);

            foreach (EzEnumHitMode mode in Enum.GetValues<EzEnumHitMode>())
            {
                widthMeasureText.Text = EzManiaScoreModeExtensions.GetHitModeDisplayName((int)mode);
                maxContentWidth = Math.Max(maxContentWidth, maxLabelWidth + label_value_spacing + widthMeasureText.DrawWidth);
            }

            foreach (EzEnumHealthMode mode in Enum.GetValues<EzEnumHealthMode>())
            {
                widthMeasureText.Text = EzManiaScoreModeExtensions.GetHealthModeDisplayName((int)mode);
                maxContentWidth = Math.Max(maxContentWidth, maxLabelWidth + label_value_spacing + widthMeasureText.DrawWidth);
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
            drawSizeLayout.Invalidate();
        }

        private void updateLayout()
        {
            if (VerticalLayoutWidth <= 0)
                return;

            bool horizontal = availableWidth >= HorizontalLayoutWidth - layout_hysteresis;

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
        }

        private void applyHorizontalLayout()
        {
            layoutGrid.RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) };
            layoutGrid.ColumnDimensions = createHorizontalColumns(VerticalLayoutWidth);
            layoutGrid.Content = createHorizontalContent();
        }

        private void applyVerticalLayout()
        {
            layoutGrid.ColumnDimensions = new[] { new Dimension(GridSizeMode.Absolute, VerticalLayoutWidth) };
            layoutGrid.RowDimensions = new[]
            {
                new Dimension(GridSizeMode.AutoSize),
                new Dimension(GridSizeMode.Absolute, stacked_row_spacing),
                new Dimension(GridSizeMode.AutoSize),
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

        private partial class ModeBlock : CompositeDrawable
        {
            private readonly OsuSpriteText labelText;
            private readonly TruncatingSpriteText valueText;

            public ModeBlock()
            {
                Height = block_row_height;
                Masking = true;

                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(label_value_spacing, 0f),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Children = new Drawable[]
                    {
                        labelText = new OsuSpriteText
                        {
                            Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        valueText = new TruncatingSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Font = OsuFont.Style.Caption1,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                labelText.Colour = colourProvider.Content2;
                valueText.Colour = colourProvider.Content1;
            }

            protected override void Update()
            {
                base.Update();

                valueText.MaxWidth = Math.Max(Width - labelText.DrawWidth - label_value_spacing, 0);
            }

            public void SetValues(string label, string value)
            {
                labelText.Text = label;
                valueText.Text = value;
            }
        }
    }
}
