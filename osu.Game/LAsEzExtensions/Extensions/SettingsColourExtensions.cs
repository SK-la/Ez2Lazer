// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.LAsEzExtensions.Configuration;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Extensions
{
    public static partial class SettingsColourExtensions
    {
        public static Container CreateStyledSettingsColour(string label, BindableColour4 current)
        {
            var backgroundBox = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Black.Opacity(0.05f)
            };
            var hoverContainer = new HoverContainer
            {
                RelativeSizeAxes = Axes.X,
                // AutoSizeAxes = Axes.Y,
                Height = 25,
                Margin = new MarginPadding { Top = 2, Bottom = 2 },
                Masking = true,
                CornerRadius = 6,
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Radius = 3f,
                    Colour = Color4.Black.Opacity(0.2f),
                    Offset = new Vector2(0, 1),
                },
                BackgroundBox = backgroundBox,
                Children = new Drawable[]
                {
                    backgroundBox,
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Position = new Vector2(-85, 0),
                        // Margin = new MarginPadding { Left = 10f },
                        Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 16),
                        Colour = Color4.DodgerBlue,
                        Text = label,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 6,
                        Child = new EzSettingsColour
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Scale = new Vector2(0.8f),
                            Current = current,
                        }
                    }
                }
            };

            return hoverContainer;
        }

        private partial class HoverContainer : Container, IHasTooltip
        {
            public Box BackgroundBox { get; set; } = null!;

            public LocalisableString TooltipText { get; set; } = "全局列颜色方案设置";

            protected override bool OnHover(osu.Framework.Input.Events.HoverEvent e)
            {
                BackgroundBox.FadeColour(Color4.White.Opacity(0.1f), 200, Easing.OutQuint);
                return false; // 允许事件继续传递
            }

            protected override void OnHoverLost(osu.Framework.Input.Events.HoverLostEvent e)
            {
                BackgroundBox.FadeColour(Color4.Black.Opacity(0.05f), 200, Easing.OutQuint);
            }
        }
    }
}
