// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// 模仿 StarRatingDisplay 形式的显示XxySR
    /// </summary>
    public partial class EzDisplayXxySR : CompositeDrawable, IHasCurrentValue<EzAnalysisResult>
    {
        private readonly bool animated;
        private readonly Box background;
        private readonly SpriteIcon srIcon;
        private readonly OsuSpriteText srText;

        private readonly BindableWithCurrent<EzAnalysisResult> current = new BindableWithCurrent<EzAnalysisResult>();

        public Bindable<EzAnalysisResult> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        // public Color4 DisplayedDifficultyColour => background.Colour;
        // public Color4 DisplayedDifficultyTextColour => srText.Colour;
        // private readonly Bindable<double> displayedStars = new BindableDouble();
        // public IBindable<double> DisplayedStars => displayedStars;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public EzDisplayXxySR(EzAnalysisResult ezAnalysisResult, bool animated = false)
        {
            // this.animated = animated;

            Current.Value = ezAnalysisResult;

            AutoSizeAxes = Axes.Both;

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
                        Margin = new MarginPadding { Horizontal = 7f },
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
                // if (animated)
                //     // Animation roughly matches `StarCounter`'s implementation.
                //     this.TransformBindableTo(displayedStars, c.NewValue.XxySr, 100 + 80 * Math.Abs(c.NewValue.XxySr - c.OldValue.XxySr), Easing.OutQuint);
                // else
                //     displayedStars.Value = c.NewValue.XxySr;
                updateDisplay(c.NewValue.ManiaAttributes?.XxySr);
            }, true);

            // displayedStars.Value = Current.Value.ManiaAttributes?.XxySr;
            //
            // displayedStars.BindValueChanged(s =>
            // {
            //     srText.Text = s.NewValue < 0 ? "-" : s.NewValue.FormatStarRating();
            //
            //     background.Colour = colours.ForStarDifficulty(s.NewValue);
            //
            //     srIcon.Colour = colours.ForStarDifficultyText(s.NewValue);
            //     srText.Colour = colours.ForStarDifficultyText(s.NewValue);
            // }, true);
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
                // Debug: show 4 decimal places for easier detection of value reuse.
                // Keep the same "never round up" behaviour as FormatUtils.FormatStarRating().
                srText.Text = sr.Value.FloorToDecimalDigits(4).ToLocalisableString("0.0000");
#else
                // Release: match official star formatting (2 decimal places).
                srText.Text = sr.Value.FormatStarRating();
#endif
            }

            background.Colour = colours.ForStarDifficulty(sr.Value);

            srIcon.Colour = colours.ForStarDifficultyText(sr.Value);
            srText.Colour = colours.ForStarDifficultyText(sr.Value);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                Current.UnbindAll();

            base.Dispose(isDisposing);
        }
    }
}
