// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.EzOsuGame.Textures
{
    /// <summary>
    /// 守卫三模板解析：子目录 / 同层前缀分级 / 单图回退，以及层1 的两条硬约束——
    /// 取像键不带后缀（否则与既有调用点各解一遍码）、用户根覆盖内置根。
    /// </summary>
    /// <remarks>
    /// 帧以不同边长区分，故断言的是「取到了哪几帧、顺序如何」，而不是只看帧数。
    /// </remarks>
    [HeadlessTest]
    public partial class TestSceneEzResourceAnimationResolution : OsuTestScene
    {
        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private EzResourceStore? resources;
        private Ez2ConfigManager? ezConfig;

        [Test]
        public void Template3SubdirectoryWinsAndSharesCacheKey()
        {
            AddStep("准备资源", createResourceStore);

            AddStep("子目录帧 000..003", () =>
            {
                for (int i = 0; i < 4; i++)
                    writePng($"EzResources/note/circle/whitenote/{i:D3}.png", 10 + i);
            });

            AddAssert("层2 取到子目录整段，顺序即帧号", () =>
            {
                Assert.That(widths(getFrames("note/circle/whitenote")), Is.EqualTo(new[] { 10, 11, 12, 13 }));
                return true;
            });

            AddAssert("层1 取像键不带后缀", () =>
            {
                Assert.That(resources!.ListImageKeys("note/circle/whitenote"),
                    Is.EqualTo(new[] { "note/circle/whitenote/000", "note/circle/whitenote/001", "note/circle/whitenote/002", "note/circle/whitenote/003" }));
                return true;
            });

            AddAssert("与既有直取路径命中同一张纹理（不重复解码）", () =>
            {
                Assert.That(resources!.Get("note/circle/whitenote/000", EzTextureUsage.AnimationSafe), Is.SameAs(getFrames("note/circle/whitenote")[0]));
                return true;
            });
        }

        [Test]
        public void Template1PrefixGrading()
        {
            AddStep("准备资源", createResourceStore);

            AddStep("各前缀写法", () =>
            {
                writePng("EzResources/Grade/Dash-0.png", 20);
                writePng("EzResources/Grade/Dash-1.png", 21);
                writePng("EzResources/Grade/Dash_0.png", 30);
                writePng("EzResources/Grade/Dash_1.png", 31);
                writePng("EzResources/Grade/Under_0.png", 40);
                writePng("EzResources/Grade/Under_1.png", 41);
                writePng("EzResources/Grade/Bare0.png", 50);
                writePng("EzResources/Grade/Bare1.png", 51);

                // 资源名后紧跟字母数字不算同族：Loose 不吃 Loose2_。
                writePng("EzResources/Grade/Loose-0.png", 70);
                writePng("EzResources/Grade/Loose-1.png", 71);
                writePng("EzResources/Grade/Loose2_0.png", 80);
                writePng("EzResources/Grade/Loose2_1.png", 81);
            });

            AddAssert("'-' 优先于 '_'", () => expect(new[] { 20, 21 }, "Grade/Dash"));
            AddAssert("只有 '_' 时取 '_'", () => expect(new[] { 40, 41 }, "Grade/Under"));
            AddAssert("联接符只认 -/_：{name}{数值} 不算帧", () => expect(System.Array.Empty<int>(), "Grade/Bare"));
            AddAssert("资源名后紧跟字母数字的组被排除", () => expect(new[] { 70, 71 }, "Grade/Loose"));
        }

        [Test]
        public void Template3SubdirectoryFrameSeries()
        {
            AddStep("准备资源", createResourceStore);

            AddStep("子目录各类帧名", () =>
            {
                // 纯数字（note 的 000.png 形态）。
                writePng("EzResources/Sub/Note/000.png", 10);
                writePng("EzResources/Sub/Note/001.png", 11);

                // {name}/frame[连接符][数值]。
                writePng("EzResources/Sub/Judge/frame_0.png", 20);
                writePng("EzResources/Sub/Judge/frame_1.png", 21);
                writePng("EzResources/Sub/Flat/frame-0.png", 30);
                writePng("EzResources/Sub/Flat/frame-1.png", 31);

                // 无连接符的 {name}{数值} 不算帧组。
                writePng("EzResources/Sub/Bad/Bad0.png", 40);
                writePng("EzResources/Sub/Ghost/Ghost1.png", 41);
            });

            AddAssert("纯数字文件名成帧", () => expect(new[] { 10, 11 }, "Sub/Note"));
            AddAssert("{name}/frame_0 成帧", () => expect(new[] { 20, 21 }, "Sub/Judge"));
            AddAssert("{name}/frame-0 成帧", () => expect(new[] { 30, 31 }, "Sub/Flat"));
            AddAssert("子目录内无连接符不算帧", () => expect(System.Array.Empty<int>(), "Sub/Bad"));
            AddAssert("子目录内 {name}{数值} 不算帧", () => expect(System.Array.Empty<int>(), "Sub/Ghost"));
        }

        [Test]
        public void SubdirectoryNameSuffixedFramesDoNotShadowFlatSeparatedSet()
        {
            AddStep("准备资源", createResourceStore);

            AddStep("复现现场：同层 '-' 序列 + 子目录里的同名无分隔符单图", () =>
            {
                writePng("EzResources/Column/ColumnLight-0.png", 900);
                writePng("EzResources/Column/ColumnLight-1.png", 901);
                writePng("EzResources/Column/ColumnLight/ColumnLight1.png", 1);
                writePng("EzResources/Column/ColumnLight/1ColumnLight.png", 2);
                writePng("EzResources/Column/ColumnLight/S-Light.png", 3);
            });

            AddAssert("取同层 '-' 序列，子目录里的 {name}{数值} 不参与", () => expect(new[] { 900, 901 }, "Column/ColumnLight"));
        }

        [Test]
        public void NameSuffixedFramesRequireSeparatorOnlyInDefaults()
        {
            AddStep("准备资源", createResourceStore);

            AddStep("同层只有无分隔符的 {name}{数值}", () =>
            {
                writePng("EzResources/Bare/Name0.png", 950);
                writePng("EzResources/Bare/Name1.png", 951);
            });

            AddAssert("默认三模板不加载", () => expect(System.Array.Empty<int>(), "Bare/Name"));

            AddAssert("显式模板 {name}{0} 仍可取", () => expect(new[] { 950, 951 }, "Bare/Name", template: "{name}{0}"));
        }

        [Test]
        public void FrameWindowTruncatesAtGap()
        {
            AddStep("准备资源", createResourceStore);

            AddStep("窗口样本（含断号）", () =>
            {
                for (int i = 0; i < 5; i++)
                    writePng($"EzResources/Window/Run-{i}.png", 100 + i);

                writePng("EzResources/Window/Broken-0.png", 200);
                writePng("EzResources/Window/Broken-1.png", 201);
                writePng("EzResources/Window/Broken-3.png", 203);
            });

            AddAssert("起始帧与帧数上限生效", () => expect(new[] { 102, 103 }, "Window/Run", startIndex: 2, maxFrames: 2));
            AddAssert("遇断号即停", () => expect(new[] { 200, 201 }, "Window/Broken"));
        }

        [Test]
        public void SingleImageFallbackAndExplicitTemplate()
        {
            AddStep("准备资源", createResourceStore);

            AddStep("单图与显式模板样本", () =>
            {
                writePng("EzResources/Alone/Still.png", 300);
                writePng("EzResources/Judge/Cool/frame_0.png", 400);
                writePng("EzResources/Judge/Cool/frame_1.png", 401);
                writePng("EzResources/Judge/Single_overlay.png", 500);
                writePng("EzResources/Judge/Suffixed0.png", 600);
                writePng("EzResources/Judge/Suffixed1.png", 601);
            });

            AddAssert("单图回退为 1 帧", () => expect(new[] { 300 }, "Alone/Still"));
            AddAssert("关闭单图回退后为 0 帧", () => expect(System.Array.Empty<int>(), "Alone/Still", allowSingleFallback: false));
            AddAssert("显式 {name}/frame_{0} 命中子目录", () => expect(new[] { 400, 401 }, "Judge/Cool", template: "{name}/frame_{0}"));
            AddAssert("{name}_overlay 表达单图", () => expect(new[] { 500 }, "Judge/Single", template: "{name}_overlay"));
            AddAssert("含序号但不含 {name} 时自动前置资源名", () => expect(new[] { 600, 601 }, "Judge/Suffixed", template: "{0}"));
        }

        [Test]
        public void NameLookupIsCaseInsensitive()
        {
            AddStep("准备资源", createResourceStore);
            AddStep("磁盘上是全大写名", () => writePng("EzResources/Theme/one/KOOL.png", 700));

            AddAssert("大小写不同也能解析", () => expect(new[] { 700 }, "Theme/one/Kool"));
            AddAssert("磁盘上写了后缀就按后缀查", () => expect(new[] { 700 }, "Theme/one/KOOL.png"));
            AddAssert("后缀不折叠：查 KOOL.jpg 取不到", () => expect(System.Array.Empty<int>(), "Theme/one/KOOL.jpg"));
        }

        [Test]
        public void UserRootOverridesEmbeddedRoot()
        {
            AddStep("准备资源", createResourceStore);

            AddAssert("内置段位图可解析（包内 _6k 与逻辑 6k 归一）", () =>
            {
                Assert.That(resources!.Get("Dans/6k/7", EzTextureUsage.AnimationSafe), Is.Not.Null, "内置段位图未解析");
                return true;
            });

            AddStep("用户根放同名覆盖图（边长 3，明显区别于内置）", () =>
            {
                writePng("EzResources/Dans/6k/7.png", 3);
                resources!.InvalidateResourceCaches();
            });

            AddAssert("用户根覆盖内置根", () => expect(new[] { 3 }, "Dans/6k/7"));
        }

        [Test]
        public void InvalidationMakesNewFramesVisible()
        {
            AddStep("准备资源", createResourceStore);

            AddAssert("尚无该目录时为空", () => expect(System.Array.Empty<int>(), "Late/Entry"));

            AddStep("事后放入帧", () =>
            {
                writePng("EzResources/Late/Entry-0.png", 800);
                writePng("EzResources/Late/Entry-1.png", 801);
            });

            AddAssert("不失效则仍被解析缓存挡住", () => expect(System.Array.Empty<int>(), "Late/Entry"));

            AddStep("显式失效", () => resources!.InvalidateResourceCaches());

            AddAssert("失效后新帧可见", () => expect(new[] { 800, 801 }, "Late/Entry"));
        }

        /// <summary>断言某路径解析出的帧宽（即帧序）；便于把「取到哪几帧」写成一行。</summary>
        private bool expect(int[] expectedWidths, string path, int startIndex = 0, int maxFrames = 240, string? template = null, bool allowSingleFallback = true)
        {
            Assert.That(widths(getFrames(path, startIndex, maxFrames, template, allowSingleFallback)), Is.EqualTo(expectedWidths));
            return true;
        }

        private Texture[] getFrames(string path, int startIndex = 0, int maxFrames = 240, string? template = null, bool allowSingleFallback = true)
        {
            return resources!.GetTextureFrames(new EzAnimationRequest
            {
                Path = path,
                StartIndex = startIndex,
                MaxFrames = maxFrames,
                FrameTemplate = template,
                AllowSingleFallback = allowSingleFallback,
            });
        }

        private static int[] widths(Texture[] frames)
        {
            int[] widths = new int[frames.Length];

            for (int i = 0; i < frames.Length; i++)
                widths[i] = frames[i].Width;

            return widths;
        }

        private void createResourceStore()
        {
            ezConfig = new Ez2ConfigManager(LocalStorage);
            resources = new EzResourceStore(ezConfig, renderer, Audio, LocalStorage, null!);
        }

        private void writePng(string path, int size)
        {
            using var image = new Image<Rgba32>(size, size);
            using var stream = LocalStorage.GetStream(path, FileAccess.Write);
            image.SaveAsPng(stream);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                resources?.Dispose();
                ezConfig?.Dispose();
            }

            base.Dispose(isDisposing);
        }
    }
}
