// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
using osuTK;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// Performance-point pill, shaped like <see cref="StarRatingDisplay"/> so it can sit beside a star pill.
    /// </summary>
    /// <remarks>
    /// Null and negative values render the same dash the star pill uses for "not available"; there is no
    /// separate empty state, so a panel never has to decide whether a 0 means zero points or no data.
    /// </remarks>
    public partial class EzDisplayPP : CompositeDrawable, IHasCurrentValue<double?>
    {
        private readonly BindableWithCurrent<double?> current = new BindableWithCurrent<double?>();

        public Bindable<double?> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        private readonly Box background;
        private readonly SpriteIcon icon;
        private readonly OsuSpriteText text;

        public EzDisplayPP(StarRatingDisplaySize size = StarRatingDisplaySize.Regular)
        {
            AutoSizeAxes = Axes.Both;

            MarginPadding margin = size switch
            {
                StarRatingDisplaySize.Small => new MarginPadding { Horizontal = 7f },
                StarRatingDisplaySize.Range => new MarginPadding { Horizontal = 8f },
                _ => new MarginPadding { Horizontal = 8f, Vertical = 2f },
            };

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
                                icon = new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Icon = FontAwesome.Solid.Bolt,
                                    Size = new Vector2(8f),
                                },
                                Empty(),
                                text = new OsuSpriteText
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
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            background.Colour = colours.BlueDarker;
            icon.Colour = colours.YellowLight;
            text.Colour = colours.YellowLight;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Current.BindValueChanged(c => updateDisplay(c.NewValue), true);
        }

        private void updateDisplay(double? value)
        {
            if (value == null || !double.IsFinite(value.Value) || value.Value < 0)
            {
                text.Text = "-";
                return;
            }

            text.Text = value.Value.ToLocalisableString("N0");
        }
    }
}
