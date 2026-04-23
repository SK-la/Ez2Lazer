// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Utils;
using osuTK;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// 模仿 StarRatingDisplay 形式的显示XxySR
    /// </summary>
    public partial class EzDisplaySR : CompositeDrawable, IHasCurrentValue<EzManiaSummary>
    {
        private readonly bool animated;
        private readonly Box background;
        private readonly SpriteIcon srIcon;
        private readonly OsuSpriteText srText;

        private readonly BindableWithCurrent<EzManiaSummary> current = new BindableWithCurrent<EzManiaSummary>();

        public Bindable<EzManiaSummary> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        private readonly Bindable<double> displayedStars = new Bindable<double>();

        // 对外提供当前数值
        // public IBindable<double?> DisplayedStars => displayedStars;
        // public Color4 DisplayedDifficultyColour => background.Colour;
        // public Color4 DisplayedDifficultyTextColour => srText.Colour;

        private double sr;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public EzDisplaySR(EzManiaSummary ezAnalysisResult, StarRatingDisplaySize size = StarRatingDisplaySize.Regular, bool animated = false)
        {
            this.animated = animated;

            Current.Value = ezAnalysisResult;

            AutoSizeAxes = Axes.Both;

            MarginPadding margin = default;

            switch (size)
            {
                case StarRatingDisplaySize.Small:
                    margin = new MarginPadding { Horizontal = 7f };
                    break;

                case StarRatingDisplaySize.Range:
                    margin = new MarginPadding { Horizontal = 8f };
                    break;

                case StarRatingDisplaySize.Regular:
                    margin = new MarginPadding { Horizontal = 8f, Vertical = 2f };
                    break;
            }

            InternalChild = new CircularContainer
            {
                Masking = true,
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    new GridContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Margin = margin,
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.Absolute, 3f),
                            new Dimension(GridSizeMode.AutoSize, minSize: 25f),
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new[]
                            {
                                srIcon = new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Icon = FontAwesome.Solid.Moon,
                                    Size = new Vector2(8f),
                                },
                                Empty(),
                                srText = new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Margin = new MarginPadding { Bottom = 1.5f },
                                    Spacing = new Vector2(-1.4f),
                                    Font = OsuFont.Torus.With(size: 14.4f, weight: FontWeight.Bold, fixedWidth: true),
                                    Shadow = false,
                                },
                            }
                        }
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(c =>
            {
                sr = c.NewValue.XxySr ?? 0;

                if (animated)
                {
                    double diff = sr - c.OldValue.XxySr ?? 0;
                    this.TransformBindableTo(displayedStars, sr, 100 + 80 * Math.Abs(diff), Easing.OutQuint);
                }
                else
                    displayedStars.Value = sr;

                // updateDisplay(c.NewValue.ManiaSummary?.XxySr);
            }, true);

            displayedStars.Value = Current.Value.XxySr ?? 0;

            displayedStars.BindValueChanged(s =>
            {
                updateDisplay(s.NewValue);
            }, true);
        }

        private void updateDisplay(double? sr)
        {
            if (sr == null)
            {
                // Hide();
                srText.Text = "/";
                //
                // // Placeholder state: keep the pill background subtle, but ensure icon/text remain visible.
                // background.Colour = colourProvider?.Background5 ?? Color4Extensions.FromHex("303d47");
                // srIcon.Colour = colourProvider?.Content2 ?? Color4.White.Opacity(0.9f);
                // srText.Colour = colourProvider?.Content2 ?? Color4.White.Opacity(0.9f);
                return;
            }

            if (sr.Value < 0)
            {
                srText.Text = "-";
            }
            else
            {
#if DEBUG
                srText.Text = sr.Value.FloorToDecimalDigits(4).ToLocalisableString("0.0000");
#else
                srText.Text = sr.Value.FormatStarRating();
#endif
            }

            background.Colour = colours.ForStarDifficulty(sr.Value);

            srIcon.Colour = colours.ForStarDifficultyText(sr.Value);
            srText.Colour = colours.ForStarDifficultyText(sr.Value);
        }
    }
}
