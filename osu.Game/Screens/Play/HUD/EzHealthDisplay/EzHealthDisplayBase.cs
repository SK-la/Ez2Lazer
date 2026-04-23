// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame;

namespace osu.Game.Screens.Play.HUD.EzHealthDisplay
{
    public abstract partial class EzHealthDisplayBase : HealthDisplay
    {
        protected EzLocalTextureFactory TextureFactory { get; private set; } = null!;

        protected string TexturePrefix { get; set; } = "health/";

        protected Container Content { get; private set; } = null!;

        protected abstract string[] TextureSuffixes { get; }

        [BackgroundDependencyLoader]
        private void load(EzLocalTextureFactory textureFactory)
        {
            TextureFactory = textureFactory;

            AutoSizeAxes = Axes.Y;

            InternalChild = Content = new Container
            {
                RelativeSizeAxes = Axes.Both,
            };

            LoadTextures();
        }

        protected virtual void LoadTextures()
        {
            foreach (string suffix in TextureSuffixes)
            {
                var texture = TextureFactory.CreateAnimation(TexturePrefix + suffix);

                Content.Add(texture);
            }
        }
    }
}
