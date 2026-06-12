// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public partial class PixivAttributionBadge : OsuClickableContainer
    {
        private readonly OsuSpriteText label;

        public override bool HandlePositionalInput => true;

        public PixivAttributionBadge(PixivIllustInfo illust, Action openArtworkUrl)
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            Position = new Vector2(30, 30);
            AutoSizeAxes = Axes.Both;
            Action = openArtworkUrl;
            TooltipText = "Open on Pixiv";
            Depth = float.MinValue;

            Child = new Container
            {
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0.65f,
                        Colour = Color4.Black,
                    },
                    label = new OsuSpriteText
                    {
                        Margin = new MarginPadding { Horizontal = 14, Vertical = 8 },
                        Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 18),
                        Text = illust.AttributionLabel,
                        Colour = Color4.White,
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            label.FadeColour(Colour4.FromHex(@"B0D4FF"), 150, Easing.OutQuint);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            label.FadeColour(Color4.White, 150, Easing.OutQuint);
            base.OnHoverLost(e);
        }
    }
}
