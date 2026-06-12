// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public partial class PixivBackground : Graphics.Backgrounds.Background
    {
        private readonly PixivIllustInfo? presetIllust;
        private readonly string? presetResourcePath;

        private PixivIllustInfo illustInfo;
        private string loadedTexturePath = string.Empty;

        public bool HasPixivIllust => illustInfo.IllustId > 0;
        public PixivIllustInfo IllustInfo => illustInfo;

        public PixivBackground(PixivIllustInfo? illust = null, string? resourcePath = null)
            : base(string.Empty)
        {
            presetIllust = illust;
            presetResourcePath = resourcePath;
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures)
        {
            if (presetIllust is { IllustId: > 0 } illust && !string.IsNullOrEmpty(presetResourcePath))
            {
                illustInfo = illust;
                loadedTexturePath = presetResourcePath;
                Sprite.Texture = textures.Get(presetResourcePath);
                return;
            }

            loadedTexturePath = $@"Backgrounds/bg{RNG.Next(1, 6)}";
            Sprite.Texture = textures.Get(loadedTexturePath);
        }

        public override bool Equals(Graphics.Backgrounds.Background other)
        {
            if (other is PixivBackground pixiv)
            {
                if (ReferenceEquals(this, pixiv))
                    return true;

                long leftIllustId = presetIllust?.IllustId ?? illustInfo.IllustId;
                long rightIllustId = pixiv.presetIllust?.IllustId ?? pixiv.illustInfo.IllustId;
                string leftPath = presetResourcePath ?? loadedTexturePath;
                string rightPath = pixiv.presetResourcePath ?? pixiv.loadedTexturePath;

                return leftIllustId > 0
                       && leftIllustId == rightIllustId
                       && leftPath.Length > 0
                       && string.Equals(leftPath, rightPath, System.StringComparison.OrdinalIgnoreCase);
            }

            return base.Equals(other);
        }
    }
}
