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
    /// 守卫 <see cref="EzTextureUsage"/> 的图集归属：字形必须共存于同一张专用页，
    /// 段位标必须在加载期压到规范边长后进自己的专用页（既不能绕过图集，也不能挤占通用页）。
    /// </summary>
    [HeadlessTest]
    public partial class TestSceneEzTextureAtlasPartitioning : OsuTestScene
    {
        // 数字字形，外加 EzComboText / EzScoreText 预加载的特殊字形。
        private static readonly string[] glyph_names =
            Enumerable.Range(0, 10).Select(i => i.ToString(CultureInfo.InvariantCulture)).Concat(new[] { "t", "e", "l" }).ToArray();

        // EzResources/Dans 里实际存在的尺寸组合，含 1024 这一档——它已超过 1024 页的可放宽度。
        private static readonly int[] badge_sizes = { 1024, 560, 320, 280 };

        private const int glyph_size = 96;
        private const int ui_size = 400;
        private const int ui_count = 12;

        // 段位标规范边长，需与 EzResourceStore 保持一致。
        private const int badge_texture_size = 128;

        // 字形页尺寸，需与 EzResourceStore 保持一致。
        private const int glyph_atlas_size = 2048;

        private readonly List<string> atlasOverflows = new List<string>();
        private readonly List<Texture> glyphTextures = new List<Texture>();
        private readonly List<(Texture texture, int expectedSize)> badgeTextures = new List<(Texture, int)>();

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private EzResourceStore? resources;
        private Ez2ConfigManager? ezConfig;

        [Test]
        public void TestGlyphSetStaysInOneAtlasPage()
        {
            AddStep("准备资源", createResourceStore);

            AddStep("用通用 UI 纹理把通用页灌到换页", () =>
            {
                atlasOverflows.Clear();

                for (int i = 0; i < ui_count; i++)
                    Assert.That(resources!.Get($"Ui/{i}", EzTextureUsage.Atlas), Is.Not.Null, $"Ui/{i} 未加载");
            });

            AddAssert("通用页确实发生了换页（说明压力足以挤占共享页）", () => atlasOverflows.Count > 0);

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

            AddAssert("字形页与通用页互相独立", () =>
            {
                var glyph = resources!.StoreFor(EzTextureUsage.Glyph);

                Assert.That(glyph, Is.Not.SameAs(resources.StoreFor(EzTextureUsage.Atlas)), "字形与通用 UI 共用了同一图集页");

                return true;
            });
        }

        [Test]
        public void TestBadgesFitIntoDedicatedAtlasPage()
        {
            AddStep("准备资源", createResourceStore);

            AddStep("加载各档真实尺寸的段位图", () =>
            {
                atlasOverflows.Clear();
                badgeTextures.Clear();

                for (int i = 0; i < badge_sizes.Length; i++)
                {
                    Texture? texture = resources!.Get($"Badges/{i}", EzTextureUsage.Badge);
                    Assert.That(texture, Is.Not.Null, $"Badges/{i} 未加载");
                    badgeTextures.Add((texture!, badge_sizes[i]));
                }
            });

            AddAssert("各档源图在加载期被压到规范边长", () =>
            {
                foreach ((Texture texture, int sourceSize) in badgeTextures)
                {
                    Assert.That(texture.Width, Is.EqualTo(badge_texture_size), $"{sourceSize}px 源图未被压到规范边长");
                    Assert.That(texture.Height, Is.EqualTo(badge_texture_size), $"{sourceSize}px 源图未被压到规范边长");
                }

                return true;
            });

            AddAssert("段位图确实落在图集页上（而非绕过图集）", () =>
            {
                var pageSizes = badgeTextures.Select(t => pageSizeOf(t.texture)).Distinct().ToArray();

                Assert.That(pageSizes, Has.Length.EqualTo(1), "段位图被拆到了多张图集页");
                Assert.That(pageSizes[0], Is.EqualTo(1024).Within(0.5f), $"段位图没有落在图集页（实际页尺寸 {pageSizes[0]}）");

                return true;
            });

            AddAssert("段位图加载过程中没有图集换页", () =>
            {
                Assert.That(atlasOverflows, Is.Empty, "段位图加载过程中发生了换页");
                return true;
            });

            AddAssert("段位页与字形页、通用页互相独立", () =>
            {
                Assert.That(resources!.StoreFor(EzTextureUsage.Badge), Is.Not.SameAs(resources.StoreFor(EzTextureUsage.Glyph)));
                Assert.That(resources.StoreFor(EzTextureUsage.Badge), Is.Not.SameAs(resources.StoreFor(EzTextureUsage.Atlas)));

                return true;
            });
        }

        [Test]
        public void TestBadgeScalingIsBoundedAndProportional()
        {
            AddStep("准备资源", createResourceStore);

            AddStep("加载超限的非方形段位图与小于规范边长的段位图", () =>
            {
                atlasOverflows.Clear();

                Texture? wide = resources!.Get("Badges/Wide", EzTextureUsage.Badge);
                Texture? small = resources!.Get("Badges/Small", EzTextureUsage.Badge);

                Assert.That(wide, Is.Not.Null, "非方形段位图未加载");
                Assert.That(small, Is.Not.Null, "小于规范边长的段位图未加载");

                // 512x256 超限 → 等比压到 128x64（不得拉成 128x128）。
                Assert.That(wide!.Width, Is.EqualTo(badge_texture_size), "非方形段位图未按最长边缩小");
                Assert.That(wide.Height, Is.EqualTo(badge_texture_size / 2), "非方形段位图未保持宽高比");

                // 48x48 已在规范内 → 不得放大。
                Assert.That(small!.Width, Is.EqualTo(48), "小于规范边长的段位图被放大");
                Assert.That(small.Height, Is.EqualTo(48), "小于规范边长的段位图被放大");
            });
        }

        [Test]
        public void TestBundledBadgeResolvesThroughBadgePage()
        {
            AddStep("准备资源", createResourceStore);

            AddStep("按内置命名加载段位图（用户目录下没有该图）", () =>
            {
                atlasOverflows.Clear();

                // 内置图源在 resources 包程序集里（Textures/EzResources/Dans/_6k/…），
                // 不走段位页就只剩原图（1024 > 页可放上限）这一条路。
                Texture? texture = resources!.Get("Dans/_6k/7", EzTextureUsage.Badge);

                Assert.That(texture, Is.Not.Null, "内置段位图未通过段位页解析");
                Assert.That(texture!.Width, Is.EqualTo(badge_texture_size), "内置段位图未被压到规范边长");
                Assert.That(pageSizeOf(texture), Is.EqualTo(1024).Within(0.5f), "内置段位图没有落在图集页");
            });

            AddAssert("内置段位图加载过程中没有图集换页", () =>
            {
                Assert.That(atlasOverflows, Is.Empty, "内置段位图加载过程中发生了换页");
                return true;
            });
        }

        /// <summary>
        /// 独立纹理的 UV 覆盖整张纹理（页尺寸 == 纹理宽度）；图集区域则以整页为分母。
        /// </summary>
        private static float pageSizeOf(Texture texture) => texture.Width / texture.GetTextureRect().Width;

        private void createResourceStore()
        {
            Logger.Enabled = true;
            Logger.NewEntry += onNewEntry;

            ezConfig = new Ez2ConfigManager(LocalStorage);
            resources = new EzResourceStore(ezConfig, renderer, Audio, LocalStorage, null!);

            // 走 EzResourceStore 的真实查找链：EzResources/<path>.png。
            for (int i = 0; i < badge_sizes.Length; i++)
                writePng($"EzResources/Badges/{i}.png", badge_sizes[i]);

            // 非方形（超限，应等比缩小）与小于规范边长（不得放大）的边界样本。
            writePng("EzResources/Badges/Wide.png", 512, 256);
            writePng("EzResources/Badges/Small.png", 48);

            for (int i = 0; i < ui_count; i++)
                writePng($"EzResources/Ui/{i}.png", ui_size);

            foreach (string name in glyph_names)
                writePng($"EzResources/Digits/{name}.png", glyph_size);
        }

        private void writePng(string path, int size) => writePng(path, size, size);

        private void writePng(string path, int width, int height)
        {
            using var image = new Image<Rgba32>(width, height);
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
