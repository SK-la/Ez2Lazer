// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.IO;
using osu.Game.Resources;
using osu.Game.Skinning;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// Ez2 资源提供者 - 基于官方 IStorageResourceProvider 接口设计
    ///
    /// 纹理路径（经 <see cref="EzTextureUsage"/> 选择，勿直接持有底层 store）。
    /// </summary>
    public partial class EzResourceStore : Component, IStorageResourceProvider
    {
        // HUD combo（绘制高 25）与 score（绘制高 25）共用。100 是 score 的 4 倍，整套进一页。
        private const int glyph_upload_edge = 128;

        // 段位标规范边长。段位图最多 97 张，最大显示 33px（BadgeSize 22 × 父级 Scale 1.5），
        // 128 约为其 4 倍（覆盖 HiDPI），再大只会多占页。
        private const int dan_badge_texture_size = 128;

        // 外壳页规范边长。光标与 welcome 标题都不随游戏内容变化，源图偏大且全程常驻，
        // 故在加载期压到规范尺寸，保证只占一页。
        // 256 约为菜单光标显示尺寸（900px 视口下约 46px）的 5 倍，覆盖到 4K 仍有余量。
        private const int shell_texture_edge = 256;

        // 选择器缩略图规范边长。卡片预览区最大约 128px，256 覆盖 2x HiDPI。
        private const int thumbnail_texture_edge = 256;

        #region IStorageResourceProvider 实现

        public IRenderer Renderer { get; }

        public AudioManager AudioManager { get; }

        public IResourceStore<byte[]> Files { get; }

        public IResourceStore<byte[]> Resources { get; }

        public RealmAccess RealmAccess { get; }

        /// <summary>
        /// 创建纹理加载器存储（实现 IStorageResourceProvider 接口）
        /// </summary>
        public IResourceStore<TextureUpload> CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore)
        {
            // 返回一个基于传入存储的纹理加载器
            var textureLoader = new TextureLoaderStore(underlyingStore);
            return new MaxDimensionLimitedTextureLoaderStore(textureLoader);
        }

        #endregion

        #region 私有字段

        // 纹理加载器链（对应EzTextureUsage）
        private readonly TextureStore textureStore;
        // 多帧 / 循环动画：非图集 animationSafeStore（稳定缓存纹理，不按引用 Purge）。
        private readonly TextureStore animationSafeStore;
        // 单帧大图：refCountedTextureStore（LargeTextureStore）。
        private readonly LargeTextureStore refCountedTextureStore;
        private readonly TextureStore glyphStore;
        private readonly TextureStore shellStore;
        private readonly TextureStore danStore;
        // 选择器缩略图：引用计数，最后一个引用释放即回收；上传期压到规范边长。
        private readonly LargeTextureStore thumbnailStore;

        // 层1 目录索引：帧查找的文件事实来源（用户 EzResources 与内置根双根合并）
        private readonly EzResourceDirectoryIndex directoryIndex;

        // 样本存储
        private readonly ISampleStore sampleStore;

        // 配置绑定
        private readonly Bindable<string> noteSetName = new Bindable<string>();
        private readonly Bindable<string> stageName = new Bindable<string>();

        // 缓存
        private static readonly ConcurrentDictionary<string, float> note_ratio_cache = new ConcurrentDictionary<string, float>();
        private const float square_ratio_threshold = 0.75f;

        #endregion

        #region 构造函数

        public EzResourceStore(Ez2ConfigManager ezConfig, IRenderer renderer, AudioManager audioManager, Storage storage, RealmAccess realmAccess)
        {
            Renderer = renderer;
            AudioManager = audioManager;
            RealmAccess = realmAccess;

            // 创建用户文件资源存储（指向 EzResources 目录）
            var userStorage = storage.GetStorageForDirectory(EzModifyPath.RESOURCES_PATH);
            Files = new StorageBackedResourceStore(userStorage);

            // 使用游戏内置资源作为回退
            Resources = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), "Resources");

            // 创建组合资源存储：用户 EzResources 优先，内置 Textures/EzResources 回退。
            // 内置图源在 resources 包程序集里，不在 osu.Game.dll 的 Resources 下，故单独挂一层；
            // 外面再套适配器，把 MSBuild 嵌入数字开头目录时的 _6k / _7k 归一，索引与调用方只见逻辑名。
            var embeddedRoot = new EzEmbeddedResourceStore(
                new NamespacedResourceStore<byte[]>(new DllResourceStore(OsuResources.ResourceAssembly), "Textures/EzResources"));

            var combinedStore = new ResourceStore<byte[]>();
            combinedStore.AddStore(Files); // 首先查找用户文件
            combinedStore.AddStore(embeddedRoot); // 找不到时回退到内置图源

            // 层1 目录索引（双根合并，用户优先）
            directoryIndex = new EzResourceDirectoryIndex(userStorage, embeddedRoot);

            // 创建纹理加载器链（遵循官方模式）
            var baseTextureLoader = new TextureLoaderStore(combinedStore);
            IResourceStore<TextureUpload> textureLoaderStore1 = new MaxDimensionLimitedTextureLoaderStore(baseTextureLoader);

            // Atlas：小 UI
            textureStore = new TextureStore(renderer, textureLoaderStore1, true, TextureFilteringMode.Linear, false, 2, null, "EzUi");
            textureStore.AddTextureSource(baseTextureLoader);

            // 动画帧：独立非 atlas 页，scaleAdjust 与默认 TextureStore 一致（2）。
            animationSafeStore = new TextureStore(renderer, textureLoaderStore1, useAtlas: false, scaleAdjust: 2);
            animationSafeStore.AddTextureSource(baseTextureLoader);

            // 单帧大图：引用计数，禁止用于循环动画帧。
            // 此通道保留源图尺寸；需要限制超大用户资源的通道仍使用 textureLoaderStore1。
            refCountedTextureStore = new LargeTextureStore(renderer, new TextureLoaderStore(combinedStore));
            refCountedTextureStore.AddTextureSource(baseTextureLoader);

            // 选择器缩略图：上传前压到规范边长，引用计数，最后一个引用释放即回收。
            var thumbnailLoader = new MaxDimensionLimitedTextureLoaderStore(new TextureLoaderStore(combinedStore), thumbnail_texture_edge, ResizeMode.Max);
            thumbnailStore = new LargeTextureStore(renderer, thumbnailLoader);

            // HUD combo / score 与舞台数字共用。超过 100 的图先缩小再进一页。
            var glyphLoader = new MaxDimensionLimitedTextureLoaderStore(new TextureLoaderStore(combinedStore), glyph_upload_edge, ResizeMode.Max);
            glyphStore = new TextureStore(renderer, glyphLoader, true, TextureFilteringMode.Linear, false, 2, null, "EzGlyph");
            glyphStore.AddTextureSource(baseTextureLoader);

            // 段位标页：加载期把边长压到规范尺寸后再进页，故源图再大也只占一张页。
            // 段位图要按最长边等比缩小（不得拉变形），故显式用 ResizeMode.Max，与兜底路径的逐轴裁剪区分开。
            // 图源与其它池共用同一份 combinedStore（用户 EzResources 优先，其次内置 Textures/EzResources）。
            var danLoader = new MaxDimensionLimitedTextureLoaderStore(new TextureLoaderStore(combinedStore), dan_badge_texture_size, ResizeMode.Max);
            danStore = new TextureStore(renderer, danLoader, true, TextureFilteringMode.Linear, false, 2, null, "EzDan");

            // 外壳页（菜单光标、开场 welcome 标题）：与游戏内容无关，加载期限边，源图再大也只占一张页。
            // 必须带 "Textures" 命名空间：DllResourceStore 把清单名拼成 {程序集}.{各段用点连}，
            // 少了这一段会拼成 osu.Game.Resources.Cursor.menu-cursor，与真实清单名不符，取像静默返回 null。
            var shellLoader = new MaxDimensionLimitedTextureLoaderStore(
                new TextureLoaderStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(OsuResources.ResourceAssembly), @"Textures")),
                shell_texture_edge,
                ResizeMode.Max);
            shellStore = new TextureStore(renderer, shellLoader, true, TextureFilteringMode.Linear, false, 2, 512, "EzShell");

            // 创建样本存储
            sampleStore = audioManager.GetSampleStore(new NamespacedResourceStore<byte[]>(Files, "Samples"));
            sampleStore.AddExtension("ogg");

            // 绑定配置
            ezConfig.BindWith(Ez2Setting.NoteSetName, noteSetName);
            ezConfig.BindWith(Ez2Setting.StageName, stageName);
        }

        #endregion

        #region 纹理获取 API

        /// <summary>
        /// 按用途取一张已解码纹理。动画和字形不要直接拼路径调用：动画走 <see cref="GetAnimation(EzAnimationRequest, bool, bool, double?, bool)"/>，字形走 <see cref="GetGlyph"/>。
        /// </summary>
        /// <param name="path">完整路径（不含后缀）</param>
        /// <param name="usage">纹理用途，决定底层 store</param>
        /// <param name="originalSize">是否跳过该用途的尺寸限制并按源图尺寸加载。</param>
        public Texture? Get(string path, EzTextureUsage usage = EzTextureUsage.Atlas, bool originalSize = false)
        {
            if (originalSize)
                return refCountedTextureStore.Get(path);

            return usage switch
            {
                EzTextureUsage.AnimationSafe => animationSafeStore.Get(path),
                EzTextureUsage.Large => refCountedTextureStore.Get(path),
                EzTextureUsage.Glyph => glyphStore.Get(path),
                EzTextureUsage.Badge => danStore.Get(path),
                EzTextureUsage.Shell => shellStore.Get(path),
                EzTextureUsage.Thumbnail => thumbnailStore.Get(path),
                _ => textureStore.Get(path),
            };
        }

        /// <summary>
        /// 按顺序取第一张字形。路径先经 <see cref="EzResourcePath"/> 规整，用途固定为 <see cref="EzTextureUsage.Glyph"/>。
        /// 字形是单张图，不走动画帧探测。
        /// </summary>
        public Texture? GetGlyph(IReadOnlyList<string>? candidates)
        {
            if (candidates == null)
                return null;

            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                Texture? texture = Get(EzResourcePath.Normalise(candidate), EzTextureUsage.Glyph);

                if (texture != null)
                    return texture;
            }

            return null;
        }

        /// <summary>
        /// 兼容旧调用：<c>true</c> → <see cref="EzTextureUsage.Large"/>，<c>false</c> → <see cref="EzTextureUsage.Atlas"/>。
        /// 多帧动画请改用 <see cref="EzTextureUsage.AnimationSafe"/>。
        /// </summary>
        [Obsolete("请使用 Get(path, EzTextureUsage)。动画帧务必传 AnimationSafe，勿用 Large。")]
        public Texture? Get(string path, bool useLargeStore)
            => Get(path, useLargeStore ? EzTextureUsage.Large : EzTextureUsage.Atlas);

        // 按用途取底层 store，用于测试断言「各档图集页互相独立」。运行时请勿持有，一律走 Get(path, usage)。
        internal ITextureStore StoreFor(EzTextureUsage usage)
        {
            return usage switch
            {
                EzTextureUsage.AnimationSafe => animationSafeStore,
                EzTextureUsage.Large => refCountedTextureStore,
                EzTextureUsage.Glyph => glyphStore,
                EzTextureUsage.Badge => danStore,
                EzTextureUsage.Shell => shellStore,
                EzTextureUsage.Thumbnail => thumbnailStore,
                _ => textureStore,
            };
        }

        /// <summary>
        /// 获取 Note 宽高比（带缓存）。<paramref name="clampSquare"/> 为 <c>false</c> 时不把近方形的 note 压成 1:1。
        /// </summary>
        public float GetNoteRatio(bool forceRecalculate = false, bool clampSquare = true)
        {
            string noteSet = noteSetName.Value;
            float raw = getRawNoteRatio(noteSet, forceRecalculate);

            return clampSquare && raw >= square_ratio_threshold ? 1.0f : raw;
        }

        private float getRawNoteRatio(string noteSet, bool forceRecalculate)
        {
            if (!forceRecalculate && note_ratio_cache.TryGetValue(noteSet, out float ratio))
                return ratio;

            ratio = measureRawNoteRatio(noteSet);
            note_ratio_cache.AddOrUpdate(noteSet, ratio, (_, _) => ratio);
            return ratio;
        }

        private float measureRawNoteRatio(string noteSet)
        {
            try
            {
                string basePath = $"note/{noteSet}/whitenote";

                Texture? texture = Get($"{basePath}/000", EzTextureUsage.AnimationSafe) ??
                                   Get($"{basePath}/001", EzTextureUsage.AnimationSafe);

                if (texture != null)
                    return texture.Height / (float)texture.Width;
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzTextureStore] Error calculating ratio: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, level: LogLevel.Debug);
            }

            return 1.0f;
        }

        #endregion

        #region 样本获取 API

        /// <summary>
        /// 获取音频样本
        /// </summary>
        /// <param name="name">样本名称</param>
        /// <returns>样本对象</returns>
        public ISample GetSample(string name)
        {
            return sampleStore.Get(name);
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 丢弃层1 目录索引、层2 帧解析与 note 比例缓存；各纹理池清未命中（null），动画池另整池 ClearCache 以支持同键覆盖。
        /// </summary>
        /// <remarks>
        /// <see cref="refCountedTextureStore"/> 禁止 <see cref="TextureStore.ClearCache"/>（仍被 Drawable 引用的包装会崩）。
        /// <see cref="animationSafeStore"/> 须在调用方已 refresh、旧 Drawable 下树后再 <c>ClearCache</c>（皮肤编辑器见 <c>refreshScene</c> → 本方法 → 再 refresh）。
        /// 图集四池只 <c>ClearFailedCache</c>；同键覆盖字形/UI 时在无引用冷入口对对应 store 调 <c>ClearCache(disposeAtlas: true)</c>（同 <see cref="Overlays.EzFontSettingsOverlay"/>）。
        /// 只在用户主动入口调用（资源选择器、皮肤编辑器保存/应用等）。
        /// </remarks>
        internal void InvalidateResourceCaches()
        {
            directoryIndex.Invalidate();
            frameSets.Clear();
            note_ratio_cache.Clear();

            textureStore.ClearFailedCache();
            glyphStore.ClearFailedCache();
            danStore.ClearFailedCache();
            shellStore.ClearFailedCache();

            animationSafeStore.ClearFailedCache();
            animationSafeStore.ClearCache();

            refCountedTextureStore.ClearFailedCache();
            thumbnailStore.ClearFailedCache();
        }

        #endregion

        #region 流读取 API

        /// <summary>
        /// 从 EzResources 或内置 Resources 获取资源流。
        /// </summary>
        /// <param name="path">资源路径（可为绝对路径或相对路径）</param>
        /// <returns>可读流，未找到时返回 null</returns>
        public Stream? GetEzResourceStream(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            // 允许直接传入绝对路径。
            if (Path.IsPathRooted(path))
            {
                try
                {
                    return File.Exists(path) ? File.OpenRead(path) : null;
                }
                catch
                {
                    return null;
                }
            }

            foreach (string candidate in getPathCandidates(path))
            {
                Stream? stream = Files.GetStream(candidate);
                if (stream != null)
                    return stream;

                // Resources 已 namespaced 到 "Resources"，因此这里传相对路径。
                stream = Resources.GetStream(candidate);
                if (stream != null)
                    return stream;
            }

            return null;
        }

        private static IEnumerable<string> getPathCandidates(string originalPath)
        {
            string normalized = originalPath.Replace('\\', '/').TrimStart('/');

            const string ez_prefix = "EzResources/";

            if (normalized.StartsWith(ez_prefix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(ez_prefix.Length);

            yield return normalized;

            // 兼容某些内置资源仍保留 EzResources 前缀的情况。
            yield return ez_prefix + normalized;
        }

        #endregion

        #region 资源释放

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                textureStore.Dispose();
                animationSafeStore.Dispose();
                refCountedTextureStore.Dispose();
                glyphStore.Dispose();
                shellStore.Dispose();
                danStore.Dispose();
                thumbnailStore.Dispose();
                sampleStore.Dispose();

                if (Files is IDisposable filesDisposable)
                    filesDisposable.Dispose();
            }

            base.Dispose(isDisposing);
        }

        #endregion
    }
}
