// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Rounded background shell shared by line charts and horizontal bar chart columns.
    /// </summary>
    public partial class EzLocalProfileChartCard : Container
    {
        public const float HORIZONTAL_PADDING = 8f;
        public const float VERTICAL_PADDING = 8f;

        public EzLocalProfileChartCard(LocalisableString title, Drawable body)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 8;

            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = title,
                        Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                        Padding = new MarginPadding
                        {
                            Horizontal = HORIZONTAL_PADDING,
                            Top = VERTICAL_PADDING,
                        },
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding
                        {
                            Horizontal = HORIZONTAL_PADDING,
                            Bottom = VERTICAL_PADDING,
                        },
                        Child = body,
                    },
                }
            };

            body.RelativeSizeAxes = Axes.X;
            Child = flow;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = colours.Background5,
                Depth = float.MaxValue,
            });
        }
    }
}
