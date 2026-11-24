// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Imaging;
using osu.Framework.IO.Stores;
using osu.Game.Utils;

namespace osu.Game.Skinning
{
    public class LegacyTextureLoaderStore : IResourceStore<TextureUpload>
    {
        private readonly IResourceStore<TextureUpload>? wrappedStore;

        public LegacyTextureLoaderStore(IResourceStore<TextureUpload>? wrappedStore)
        {
            this.wrappedStore = wrappedStore;
        }

        public TextureUpload Get(string name)
        {
            switch (name)
            {
                case @"button":
                    return spliceHorizontalSprites(name)!;
            }

            var textureUpload = wrappedStore?.Get(name);

            if (textureUpload == null)
                return null!;

            return shouldConvertToGrayscale(name)
                ? convertToGrayscale(textureUpload)
                : textureUpload;
        }

        public Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = new CancellationToken())
        {
            switch (name)
            {
                case @"button":
                    return Task.Run(() => spliceHorizontalSprites(name), cancellationToken)!;
            }

            var textureUpload = wrappedStore?.Get(name);

            if (textureUpload == null)
                return null!;

            return shouldConvertToGrayscale(name)
                ? Task.Run(() => convertToGrayscale(textureUpload), cancellationToken)
                : Task.FromResult(textureUpload);
        }

        // https://github.com/peppy/osu-stable-reference/blob/013c3010a9d495e3471a9c59518de17006f9ad89/osu!/Graphics/Textures/TextureManager.cs#L91-L96
        private static readonly string[] grayscale_sprites =
        {
            @"taiko-bar-right",
            @"taikobigcircle",
            @"taikohitcircle",
            @"taikohitcircleoverlay"
        };

        private bool shouldConvertToGrayscale(string name)
        {
            foreach (string grayscaleSprite in grayscale_sprites)
            {
                // unfortunately at this level of lookup we can encounter `@2x` scale suffixes in the name,
                // so straight equality cannot be used.
                if (name.Equals(grayscaleSprite, StringComparison.OrdinalIgnoreCase)
                    || name.Equals($@"{grayscaleSprite}@2x", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Splices <c>left</c>, <c>middle</c>, and <c>right</c> asset for the asset with a given <paramref name="name"/>
        /// into one sprite which can be used with <see cref="NineSliceSprite"/>.
        /// </summary>
        private TextureUpload? spliceHorizontalSprites(string name)
        {
            var left = wrappedStore?.Get($@"{name}-left@2x");
            var middle = wrappedStore?.Get($@"{name}-middle@2x");
            var right = wrappedStore?.Get($@"{name}-right@2x");

            if (left == null && middle == null && right == null)
            {
                left = wrappedStore?.Get($@"{name}-left");
                middle = wrappedStore?.Get($@"{name}-middle");
                right = wrappedStore?.Get($@"{name}-right");
            }

            if (left == null && middle == null && right == null)
                return null;

            int width = (left?.Width ?? 0) + (middle?.Width ?? 0) + (right?.Width ?? 0);
            int height = Math.Max(Math.Max(left?.Height ?? 0, middle?.Height ?? 0), right?.Height ?? 0);
            if (width == 0 || height == 0)
                return null;

            var spliced = new RgbaImage(width, height);

            if (left != null)
            {
                using var leftPixels = new RgbaImage(left.Width, left.Height, left.Data);
                ImageProcessing.DrawImage(spliced, leftPixels, 0, 0);
            }

            if (middle != null)
            {
                using var middlePixels = new RgbaImage(middle.Width, middle.Height, middle.Data);
                ImageProcessing.DrawImage(spliced, middlePixels, left?.Width ?? 0, 0);
            }

            if (right != null)
            {
                using var rightPixels = new RgbaImage(right.Width, right.Height, right.Data);
                ImageProcessing.DrawImage(spliced, rightPixels, (left?.Width ?? 0) + (middle?.Width ?? 0), 0);
            }

            return new TextureUpload(spliced);
        }

        private TextureUpload convertToGrayscale(TextureUpload textureUpload)
        {
            using var image = new RgbaImage(textureUpload.Width, textureUpload.Height, textureUpload.Data);

            // stable uses `0.299 * r + 0.587 * g + 0.114 * b`
            // (https://github.com/peppy/osu-stable-reference/blob/013c3010a9d495e3471a9c59518de17006f9ad89/osu!/Graphics/Textures/pTexture.cs#L138-L153)
            // which matches mode BT.601 (https://en.wikipedia.org/wiki/Grayscale#Luma_coding_in_video_systems)
            foreach (ref Rgba32 pixel in image.PixelSpan)
            {
                byte grey = (byte)Math.Clamp((int)MathF.Round(pixel.R * 0.299f + pixel.G * 0.587f + pixel.B * 0.114f), 0, 255);
                pixel.R = grey;
                pixel.G = grey;
                pixel.B = grey;
            }

            textureUpload.Dispose();
            return new TextureUpload(image.Clone());
        }

        public Stream? GetStream(string name) => wrappedStore?.GetStream(name);

        public IEnumerable<string> GetAvailableResources() => wrappedStore?.GetAvailableResources() ?? Array.Empty<string>();

        public void Dispose()
        {
            wrappedStore?.Dispose();
        }
    }
}
