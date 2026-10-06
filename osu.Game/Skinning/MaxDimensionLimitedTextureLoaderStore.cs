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
    /// 在 <see cref="TextureUpload"/> 阶段限制纹理最长边：超过 <c>maxDimension</c> 时按 <c>resizeMode</c> 缩小后再交给 texture store。
    /// </summary>
    /// <remarks>
    /// 缩小必须发生在创建纹理之前：能否进图集取决于上传尺寸，而显示期的缩放（<see cref="Texture.ScaleAdjust"/> 等）
    /// 只改变绘制与度量，不会减少显存占用。
    /// </remarks>
    public class MaxDimensionLimitedTextureLoaderStore : IResourceStore<TextureUpload>
    {
        // 仅防「大到 GPU 无法加载」的图，默认不该改变正常资源的尺寸。
        private const int default_max_dimension = 8192;

        // 兜底路径沿用历史语义：整段压缩到 (maxDimension, maxDimension) 框内，逐轴独立缩放，
        // 故短边保留原始像素数、长边被压到上限。勿改成 ResizeMode.Max（会把短边一并压掉）或 Crop（会丢内容），
        // 玩家自制皮肤长期依赖该行为；见 MaxDimensionLimitedTextureLoaderStoreTest 的内容级守护。
        private const ResizeMode default_resize_mode = ResizeMode.Stretch;

        private readonly IResourceStore<TextureUpload>? textureStore;
        private readonly int maxDimension;
        private readonly ResizeMode resizeMode;

        /// <summary>
        /// 限制纹理最长边。
        /// </summary>
        /// <param name="textureStore">底层纹理加载器。</param>
        /// <param name="maxDimension">单轴上限（像素）。</param>
        /// <param name="resizeMode">
        /// 超限时的缩放方式。默认 <see cref="ResizeMode.Stretch"/>：逐轴裁到上限、不保证宽高比，与历史兜底行为一致。
        /// 玩家会刻意用超长图（如 10 万像素高）触发兜底缩放，等比缩小会把短边一并压掉，
        /// 使 mania LN 这类依赖原始宽度的贴图渲染异常（两侧出细白边），故兜底路径不能等比。
        /// 需要「源图远大于规范显示尺寸、按最长边等比缩到规范尺寸」时传 <see cref="ResizeMode.Max"/>。
        /// </param>
        public MaxDimensionLimitedTextureLoaderStore(IResourceStore<TextureUpload>? textureStore, int maxDimension = default_max_dimension, ResizeMode resizeMode = default_resize_mode)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDimension, 1);

            this.textureStore = textureStore;
            this.maxDimension = maxDimension;
            this.resizeMode = resizeMode;
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

            int width = textureUpload.Width;
            int height = textureUpload.Height;

            var image = Image.LoadPixelData(textureUpload.Data, width, height);

            // 原始 texture upload 不再返回也不再使用。
            textureUpload.Dispose();

            // 目标框逐轴裁到上限：Stretch 下即逐轴裁剪（历史兜底语义），
            // Max 下为在该框内按最长边等比缩小（见构造函数 resizeMode 说明）。
            image.Mutate(i => i.Resize(new ResizeOptions
            {
                Mode = resizeMode,
                Size = new Size(Math.Min(width, maxDimension), Math.Min(height, maxDimension)),
            }));

            return new TextureUpload(image);
        }

        public Stream? GetStream(string name) => textureStore?.GetStream(name);

        public IEnumerable<string> GetAvailableResources() => textureStore?.GetAvailableResources() ?? Array.Empty<string>();
    }
}
