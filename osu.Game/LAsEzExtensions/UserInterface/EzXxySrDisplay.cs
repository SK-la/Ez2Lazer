// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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

namespace osu.Game.LAsEzExtensions.UserInterface
{
    /// <summary>
    /// A pill that displays xxy_SR (mania).
    /// Designed to visually match <see cref="StarRatingDisplay"/>, but uses a moon icon.
    /// </summary>
    public partial class EzXxySrDisplay : CompositeDrawable, IHasCurrentValue<double?>
    {
        private readonly Box background;
        private readonly SpriteIcon moonIcon;
        private readonly OsuSpriteText srText;

        private readonly BindableWithCurrent<double?> current = new BindableWithCurrent<double?>();

        public Bindable<double?> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider? colourProvider { get; set; }

        public EzXxySrDisplay(double? initialValue = null)
        {
            Current.Value = initialValue;

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
                                moonIcon = new SpriteIcon
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

            Current.BindValueChanged(v => updateDisplay(v.NewValue), true);
        }

        private void updateDisplay(double? sr)
        {
            if (sr == null)
            {
                srText.Text = "...";

                // Placeholder state: keep the pill background subtle, but ensure icon/text remain visible.
                background.Colour = colourProvider?.Background5 ?? Color4Extensions.FromHex("303d47");
                moonIcon.Colour = colourProvider?.Content2 ?? Color4.White.Opacity(0.9f);
                srText.Colour = colourProvider?.Content2 ?? Color4.White.Opacity(0.9f);
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

            moonIcon.Colour = sr.Value >= OsuColour.STAR_DIFFICULTY_DEFINED_COLOUR_CUTOFF
                ? colours.Orange1
                : colourProvider?.Background5 ?? Color4Extensions.FromHex("303d47");

            srText.Colour = sr.Value >= OsuColour.STAR_DIFFICULTY_DEFINED_COLOUR_CUTOFF
                ? colours.Orange1
                : colourProvider?.Background5 ?? Color4.Black.Opacity(0.75f);
        }
    }
}
