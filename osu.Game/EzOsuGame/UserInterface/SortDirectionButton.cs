// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.UserInterface
{
    public partial class SortDirectionButton : ShearedButton
    {
        private readonly Bindable<SortDirection> direction;
        private readonly Box directionGradient;
        private readonly SpriteIcon sortIcon;
        // private readonly SpriteIcon descendingIcon;

        public SortDirectionButton(Bindable<SortDirection> direction)
        {
            this.direction = direction;
            Name = "Song select sort direction";
            Width = 56;
            Height = 30;
            Text = string.Empty;
            ButtonContent.AutoSizeAxes = Axes.None;
            ButtonContent.RelativeSizeAxes = Axes.Both;
            ButtonContent.Padding = new MarginPadding(3);
            ButtonContent.Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ColumnDimensions = new[] { new Dimension(), new Dimension() },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Child = sortIcon = new SpriteIcon
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Icon = FontAwesome.Solid.SortAmountUp,
                                        Size = new Vector2(16),
                                    },
                                },
                                // new Container
                                // {
                                //     RelativeSizeAxes = Axes.Both,
                                //     Child = descendingIcon = new SpriteIcon
                                //     {
                                //         Anchor = Anchor.Centre,
                                //         Origin = Anchor.Centre,
                                //         Icon = FontAwesome.Solid.SortDown,
                                //         Size = new Vector2(16),
                                //     },
                                // },
                            },
                        },
                    },
                },
            };

            backgroundLayer.Add(directionGradient = new Box
            {
                RelativeSizeAxes = Axes.Both,
            });

            Action = toggleDirection;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            direction.ValueChanged += onDirectionChanged;
            updateDirection();
        }

        private void toggleDirection() =>
            direction.Value = direction.Value == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;

        private void onDirectionChanged(ValueChangedEvent<SortDirection> _) => updateDirection();

        private void updateDirection()
        {
            bool ascending = direction.Value == SortDirection.Ascending;
            var accent = ColourProvider.Highlight1;

            ColourInfo gradient = ascending
                ? ColourInfo.GradientVertical(accent.Opacity(0.2f), accent.Opacity(0f))
                : ColourInfo.GradientVertical(accent.Opacity(0f), accent.Opacity(0.2f));

            directionGradient.TransformTo(nameof(Colour), gradient, 150, Easing.OutQuint);

            sortIcon.Icon = ascending ? FontAwesome.Solid.SortAmountUp : FontAwesome.Solid.SortAmountDown;
            // sortIcon.FadeColour(ascending ? ColourProvider.Highlight1 : ColourProvider.Content2, 150, Easing.OutQuint);
            // descendingIcon.FadeColour(ascending ? ColourProvider.Content2 : ColourProvider.Highlight1, 150, Easing.OutQuint);
            TooltipText = SongSelectStrings.Sort;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && IsLoaded)
                direction.ValueChanged -= onDirectionChanged;

            base.Dispose(isDisposing);
        }
    }
}
