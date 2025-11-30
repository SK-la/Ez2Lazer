// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Extensions
{
    public static class OsuSpriteTextExtensions
    {
        public static Container WithUnderline(this OsuSpriteText text, Color4? lineColor = null)
        {
            Color4 color = lineColor ?? Color4.DodgerBlue;

            return new Container
            {
                AutoSizeAxes = Axes.Both,
                Margin = new MarginPadding { Bottom = 5 },
                Children = new Drawable[]
                {
                    text,
                    new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Width = 25,
                        Height = 2,
                        CornerRadius = 1,
                        Masking = true,
                        Margin = new MarginPadding { Top = 2 },
                        Colour = color.Opacity(0.8f),
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                        }
                    }
                }
            };
        }
    }
}
