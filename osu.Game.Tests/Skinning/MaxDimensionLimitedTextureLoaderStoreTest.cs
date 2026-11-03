// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Skinning;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Tests.Skinning
{
    /// <summary>
    /// 守卫兜底路径的缩放语义：超长图（玩家可刻意构造，如 10 万像素高）必须逐轴压到上限、保留原始宽度，
    /// 且整段内容等比压缩而非中心裁剪。这是玩家自制皮肤长期依赖的既有行为，不得改成等比（会压掉短边）或裁剪（会丢内容）。
    /// </summary>
    [TestFixture]
    public class MaxDimensionLimitedTextureLoaderStoreTest
    {
        private const int default_max_dimension = 8192;

        [Test]
        public void TestDefaultModeClampsPerAxisAndKeepsWidth()
        {
            using var loader = new MaxDimensionLimitedTextureLoaderStore(new StubTextureStore("tall", new Image<Rgba32>(40, 100_000)));

            using var result = loader.Get("tall");

            Assert.That(result.Width, Is.EqualTo(40), "超长图的短边被缩放，不再是原始宽度");
            Assert.That(result.Height, Is.EqualTo(default_max_dimension), "超长图未被压到单轴上限");
        }

        [Test]
        public void TestDefaultModeCompressesWholeContentRatherThanCentreCrop()
        {
            const int max = 64;

            // 纵向渐变：R 通道随行号线性变化，用首末行的亮度区分「整段压缩」与「中心裁剪」。
            // 图像所有权交给 StubTextureStore（即 TextureUpload），此处不再重复释放。
            var image = new Image<Rgba32>(2, 4096);

            for (int y = 0; y < image.Height; y++)
            {
                var colour = new Rgba32((byte)(y * 255 / (image.Height - 1)), 0, 0, 255);

                for (int x = 0; x < image.Width; x++)
                    image[x, y] = colour;
            }

            using var loader = new MaxDimensionLimitedTextureLoaderStore(new StubTextureStore("ramp", image), max);
            using var result = loader.Get("ramp");

            var data = result.Data;

            Assert.That(result.Height, Is.EqualTo(max));
            Assert.That(data[0].R, Is.LessThan(40), "首行不是源图顶端，内容被中心裁剪了");
            Assert.That(data[(result.Height - 1) * result.Width].R, Is.GreaterThan(215), "末行不是源图底端，内容被中心裁剪了");
        }

        [Test]
        public void TestDefaultModeLeavesWithinLimitUntouched()
        {
            var upload = new TextureUpload(new Image<Rgba32>(64, 64));
            using var loader = new MaxDimensionLimitedTextureLoaderStore(new StubTextureStore("small", upload));

            using var result = loader.Get("small");

            Assert.That(result, Is.SameAs(upload), "未超限的纹理被无谓地重建");
        }

        [Test]
        public void TestMaxModePreservesAspectForOversizedNonSquare()
        {
            using var loader = new MaxDimensionLimitedTextureLoaderStore(new StubTextureStore("wide", new Image<Rgba32>(512, 256)), 128, ResizeMode.Max);

            using var result = loader.Get("wide");

            Assert.That(result.Width, Is.EqualTo(128));
            Assert.That(result.Height, Is.EqualTo(64), "等比缩放没有保持宽高比");
        }

        private class StubTextureStore : IResourceStore<TextureUpload>
        {
            private readonly string name;
            private readonly TextureUpload upload;

            public StubTextureStore(string name, Image<Rgba32> image)
                : this(name, new TextureUpload(image))
            {
            }

            public StubTextureStore(string name, TextureUpload upload)
            {
                this.name = name;
                this.upload = upload;
            }

            public TextureUpload Get(string name) => name == this.name ? upload : null!;

            public Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Get(name));

            public Stream? GetStream(string name) => null;

            public IEnumerable<string> GetAvailableResources() => new[] { name };

            public void Dispose() => upload.Dispose();
        }
    }
}
