// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Skinning
{
    /// <summary>
    /// 在 <see cref="TextureUpload"/> 阶段限制纹理最长边：超过 <c>maxDimension</c> 时等比缩小后再交给 texture store。
    /// </summary>
    /// <remarks>
    /// 缩小必须发生在创建纹理之前：能否进图集取决于上传尺寸，而显示期的缩放（<see cref="Texture.ScaleAdjust"/> 等）
    /// 只改变绘制与度量，不会减少显存占用。
    /// </remarks>
    public class MaxDimensionLimitedTextureLoaderStore : IResourceStore<TextureUpload>
    {
        // 仅防「大到 GPU 无法加载」的图，默认不该改变正常资源的尺寸。
        private const int default_max_dimension = 8192;

        private readonly IResourceStore<TextureUpload>? textureStore;
        private readonly int maxDimension;

        public MaxDimensionLimitedTextureLoaderStore(IResourceStore<TextureUpload>? textureStore, int maxDimension = default_max_dimension)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDimension, 1);

            this.textureStore = textureStore;
            this.maxDimension = maxDimension;
        }

        public void Dispose()
        {
            textureStore?.Dispose();
        }

        public TextureUpload Get(string name)
        {
            var textureUpload = textureStore?.Get(name);

            // NRT not enabled on framework side classes (IResourceStore / TextureLoaderStore), welp.
            if (textureUpload == null)
                return null!;

            return limitTextureUploadSize(textureUpload);
        }

        public async Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = new CancellationToken())
        {
            // NRT not enabled on framework side classes (IResourceStore / TextureLoaderStore), welp.
            if (textureStore == null)
                return null!;

            var textureUpload = await textureStore.GetAsync(name, cancellationToken).ConfigureAwait(false);

            if (textureUpload == null)
                return null!;

            return await Task.Run(() => limitTextureUploadSize(textureUpload), cancellationToken).ConfigureAwait(false);
        }

        private TextureUpload limitTextureUploadSize(TextureUpload textureUpload)
        {
            // 部分用户会自制尺寸夸张的皮肤元素，大到 GPU 无法加载（连带多数图片编辑器也打不开）。
            // 这类图先缩到可用尺寸再上传。
            if (textureUpload.Width <= maxDimension && textureUpload.Height <= maxDimension)
                return textureUpload;

            var image = Image.LoadPixelData(textureUpload.Data, textureUpload.Width, textureUpload.Height);

            // 原始 texture upload 不再返回也不再使用。
            textureUpload.Dispose();

            // ResizeMode.Max：只在 (maxDimension, maxDimension) 框内等比缩到最长边贴合，不改变宽高比。
            image.Mutate(i => i.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxDimension, maxDimension),
            }));

            return new TextureUpload(image);
        }

        public Stream? GetStream(string name) => textureStore?.GetStream(name);

        public IEnumerable<string> GetAvailableResources() => textureStore?.GetAvailableResources() ?? Array.Empty<string>();
    }
}
