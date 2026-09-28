// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.EzOsuGame.Textures
{
    /// <summary>
    /// 守卫 <see cref="EzTextureUsage"/> 的图集分页：字形必须自成一套页，
    /// 否则段位图这类大件会把分配器挤到页底，同一套数字被拆到两页上，
    /// 数值每变一次都要在两页之间切换纹理绑定。
    /// </summary>
    [HeadlessTest]
    public partial class TestSceneEzTextureAtlasPartitioning : OsuTestScene
    {
        // 数字字形，外加 EzComboText / EzScoreText 预加载的特殊字形。
        private static readonly string[] glyph_names =
            Enumerable.Range(0, 10).Select(i => i.ToString(CultureInfo.InvariantCulture)).Concat(new[] { "t", "e", "l" }).ToArray();

        private const int badge_size = 512;
        private const int badge_count = 40;
        private const int glyph_size = 96;

        // 字形页尺寸，需与 EzResourceStore 保持一致。
        private const int glyph_atlas_size = 2048;

        private readonly List<string> atlasOverflows = new List<string>();
        private readonly List<Texture> glyphTextures = new List<Texture>();

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private EzResourceStore? resources;
        private Ez2ConfigManager? ezConfig;

        [Test]
        public void TestGlyphSetStaysInOneAtlasPage()
        {
            AddStep("准备资源", createResourceStore);

            AddStep("加载整套段位图", () =>
            {
                atlasOverflows.Clear();

                for (int i = 0; i < badge_count; i++)
                    Assert.That(resources!.Get($"Badges/{i}", EzTextureUsage.Badge), Is.Not.Null, $"Badges/{i} 未加载");
            });

            AddAssert("段位图页确实发生了换页（说明大件真的吃掉了整页）", () => atlasOverflows.Count > 0);

            AddStep("清空换页记录", () => atlasOverflows.Clear());

            AddStep("加载整套字形", () =>
            {
                glyphTextures.Clear();

                foreach (string name in glyph_names)
                {
                    Texture? texture = resources!.Get($"Digits/{name}", EzTextureUsage.Glyph);
                    Assert.That(texture, Is.Not.Null, $"Digits/{name} 未加载");
                    glyphTextures.Add(texture!);
                }
            });

            // 反推字形落在多大的图集页上：TextureRegion 的 UV 以整页为分母，故 页尺寸 = 区域像素宽 / UV 宽。
            AddAssert("整套字形落在同一张字形专用页", () =>
            {
                var pageSizes = glyphTextures.Select(pageSizeOf).Distinct().ToArray();

                Assert.That(pageSizes, Has.Length.EqualTo(1), "字形被拆到了多张图集页");
                Assert.That(pageSizes[0], Is.EqualTo(glyph_atlas_size).Within(0.5f), $"字形没有落在字形专用页（实际页尺寸 {pageSizes[0]}）");

                return true;
            });

            AddAssert("整套字形未触发换页", () =>
            {
                Assert.That(atlasOverflows, Is.Empty, "字形加载过程中发生了换页");
                return true;
            });

            AddAssert("字形页与段位图页、通用页互相独立", () =>
            {
                var glyph = resources!.StoreFor(EzTextureUsage.Glyph);

                Assert.That(glyph, Is.Not.SameAs(resources.StoreFor(EzTextureUsage.Badge)), "字形与段位图共用了同一图集页");
                Assert.That(glyph, Is.Not.SameAs(resources.StoreFor(EzTextureUsage.Atlas)), "字形与通用 UI 共用了同一图集页");

                return true;
            });
        }

        private static float pageSizeOf(Texture texture) => texture.Width / texture.GetTextureRect().Width;

        private void createResourceStore()
        {
            Logger.Enabled = true;
            Logger.NewEntry += onNewEntry;

            ezConfig = new Ez2ConfigManager(LocalStorage);
            resources = new EzResourceStore(ezConfig, renderer, Audio, LocalStorage, null!);

            // 走 EzResourceStore 的真实查找链：EzResources/<path>.png。
            for (int i = 0; i < badge_count; i++)
                writePng($"EzResources/Badges/{i}.png", badge_size);

            foreach (string name in glyph_names)
                writePng($"EzResources/Digits/{name}.png", glyph_size);
        }

        private void writePng(string path, int size)
        {
            using var image = new Image<Rgba32>(size, size);
            using var stream = LocalStorage.GetStream(path, FileAccess.Write);
            image.SaveAsPng(stream);
        }

        private void onNewEntry(LogEntry entry)
        {
            if (entry.Target == LoggingTarget.Performance && entry.Message.Contains("TextureAtlas") && entry.Message.Contains("size exceeded"))
                atlasOverflows.Add(entry.Message);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                Logger.NewEntry -= onNewEntry;

                resources?.Dispose();
                ezConfig?.Dispose();
            }

            base.Dispose(isDisposing);
        }
    }
}
