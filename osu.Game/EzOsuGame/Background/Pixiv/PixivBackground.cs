// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public partial class PixivBackground : Graphics.Backgrounds.Background
    {
        private PixivIllustInfo illustInfo;
        private string loadedTexturePath = string.Empty;

        public PixivBackground()
            : base(string.Empty)
        {
        }

        [BackgroundDependencyLoader]
        private void load(PixivBackgroundCoordinator coordinator, LargeTextureStore textures, OsuGame? game)
        {
            if (coordinator.TryResolveBackground(out illustInfo, out string resourcePath, out string? error))
            {
                loadedTexturePath = resourcePath;
                Sprite.Texture = textures.Get(resourcePath);
                addAttribution(game);
            }
            else
            {
                coordinator.LogFailure("Background load", error);
                loadedTexturePath = $@"Backgrounds/bg{RNG.Next(1, 6)}";
                Sprite.Texture = textures.Get(loadedTexturePath);
            }
        }

        private void addAttribution(OsuGame? game)
        {
            if (illustInfo.IllustId <= 0)
                return;

            string label = illustInfo.AttributionLabel;
            string artworkUrl = $"https://www.pixiv.net/artworks/{illustInfo.IllustId}";

            var attribution = new PixivAttributionBadge(label, () => game?.OpenUrlExternally(artworkUrl));
            AddInternal(attribution);
        }

        public override bool Equals(Graphics.Backgrounds.Background other)
        {
            if (other is PixivBackground pixiv)
                return pixiv.loadedTexturePath == loadedTexturePath;

            return base.Equals(other);
        }

        private partial class PixivAttributionBadge : OsuClickableContainer
        {
            private readonly OsuSpriteText label;

            public PixivAttributionBadge(string text, System.Action? onClick)
            {
                Anchor = Anchor.BottomLeft;
                Origin = Anchor.BottomLeft;
                Position = new Vector2(30, 30);
                AutoSizeAxes = Axes.Both;
                Action = onClick;
                TooltipText = "Open on Pixiv";

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
                            Text = text,
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
}
