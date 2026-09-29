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

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// Ez2 资源提供者 - 基于官方 IStorageResourceProvider 接口设计
    ///
    /// 纹理路径（经 <see cref="EzTextureUsage"/> 选择，勿直接持有底层 store）；
    /// 字形与段位标各自拥有独立页，不与其它纹理挤占（见 <see cref="EzTextureUsage.Glyph"/> / <see cref="EzTextureUsage.Badge"/>）：
    /// 1. <see cref="EzTextureUsage.Atlas"/> — 小 UI
    /// 2. <see cref="EzTextureUsage.AnimationSafe"/> — 多帧/循环动画（非 atlas，Dispose 为空操作）
    /// 3. <see cref="EzTextureUsage.Large"/> — 单帧大图（refcount，禁止给 TextureAnimation）
    /// 4. <see cref="EzTextureUsage.Glyph"/> — 位图字形（数字等成套纹理，必须同页）
    /// 5. <see cref="EzTextureUsage.Badge"/> — 段位标（加载期限边后进页，成套共用）
    /// </summary>
    public partial class EzResourceStore : Component, IStorageResourceProvider
    {
        // 字形页尺寸。取值只为「一整套字形必然放得下」留余量：同页是硬要求，尺寸只是实现手段。
        private const int glyph_atlas_size = 2048;

        // 段位标规范边长。段位图最多 97 张，最大显示 33px（BadgeSize 22 × 父级 Scale 1.5），
        // 128 约为其 4 倍（覆盖 HiDPI），再大只会多占页。
        private const int dan_badge_texture_size = 128;

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

        private readonly Ez2ConfigManager ezConfig;
        private readonly Storage storage;

        // 纹理加载器链（五个路径，见 EzTextureUsage）
        private readonly TextureStore textureStore;
        private readonly TextureStore animationSafeStore;
        private readonly LargeTextureStore largeTextureStore;
        private readonly TextureStore glyphStore;
        private readonly TextureStore danStore;

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
            this.ezConfig = ezConfig;
            this.storage = storage;
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
            textureStore = new TextureStore(renderer, textureLoaderStore1);
            textureStore.AddTextureSource(baseTextureLoader);

            // 动画安全：非 atlas、Dispose 为空操作。scaleAdjust 与默认 TextureStore 一致（2），避免资源显示放大一倍。
            animationSafeStore = new TextureStore(renderer, textureLoaderStore1, useAtlas: false, scaleAdjust: 2);
            animationSafeStore.AddTextureSource(baseTextureLoader);

            // 单帧大图：refcount，禁止循环动画
            largeTextureStore = new LargeTextureStore(renderer, textureLoaderStore1);
            largeTextureStore.AddTextureSource(baseTextureLoader);

            // 字形页：独占一页，保证同一套字形（数字 0-9 等）永不被其它纹理挤到两页上。
            glyphStore = new TextureStore(renderer, textureLoaderStore1, preferredAtlasSize: glyph_atlas_size);
            glyphStore.AddTextureSource(baseTextureLoader);

            // 段位标页：加载期把边长压到规范尺寸后再进页，故源图再大也只占一张页。
            // 图源与其它池共用同一份 combinedStore（用户 EzResources 优先，其次内置 Textures/EzResources）。
            var danLoader = new MaxDimensionLimitedTextureLoaderStore(new TextureLoaderStore(combinedStore), dan_badge_texture_size);
            danStore = new TextureStore(renderer, danLoader);

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
        /// 按用途获取纹理（推荐入口）。
        /// </summary>
        /// <param name="path">完整路径（不含后缀）</param>
        /// <param name="usage">纹理用途，决定底层 store</param>
        public Texture? Get(string path, EzTextureUsage usage = EzTextureUsage.Atlas)
        {
            return usage switch
            {
                EzTextureUsage.AnimationSafe => animationSafeStore.Get(path),
                EzTextureUsage.Large => largeTextureStore.Get(path),
                EzTextureUsage.Glyph => glyphStore.Get(path),
                EzTextureUsage.Badge => danStore.Get(path),
                _ => textureStore.Get(path),
            };
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
                EzTextureUsage.Large => largeTextureStore,
                EzTextureUsage.Glyph => glyphStore,
                EzTextureUsage.Badge => danStore,
                _ => textureStore,
            };
        }

        /// <summary>
        /// 获取 Note 宽高比（带缓存）
        /// </summary>
        public float GetNoteRatio(bool forceRecalculate = false)
        {
            string noteSet = noteSetName.Value;

            if (forceRecalculate || !note_ratio_cache.TryGetValue(noteSet, out float ratio))
            {
                ratio = calculateNoteRatio(noteSet);
                note_ratio_cache.AddOrUpdate(noteSet, ratio, (_, _) => ratio);
            }

            return ratio;
        }

        private float calculateNoteRatio(string noteSet)
        {
            try
            {
                string basePath = $"note/{noteSet}/whitenote";

                Texture? texture = Get($"{basePath}/000", EzTextureUsage.AnimationSafe) ??
                                   Get($"{basePath}/001", EzTextureUsage.AnimationSafe);

                if (texture != null)
                {
                    float calculatedRatio = texture.Height / (float)texture.Width;
                    return calculatedRatio >= square_ratio_threshold ? 1.0f : calculatedRatio;
                }
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
        /// 丢弃层1 目录索引与层2 帧解析缓存，使新放进 <c>EzResources</c> 的图（含新增帧）重新可见。
        /// </summary>
        /// <remarks>
        /// 只在冷的、用户主动的入口调用。纹理池只清动画帧与静态大图两池：字形页与通用图集页里的纹理
        /// 可能正被绘制（TextStyle 反复复用），整体 <c>ClearCache</c> 会在用户操作时把已绘制的图集区域释放掉。
        /// </remarks>
        internal void InvalidateResourceCaches()
        {
            directoryIndex.Invalidate();
            frameSets.Clear();

            animationSafeStore.ClearCache();
            largeTextureStore.ClearCache();
        }

        /// <summary>
        /// 构建 Note 组件路径
        /// </summary>
        public string BuildNotePath(string component)
        {
            return $"note/{noteSetName.Value}/{component}";
        }

        /// <summary>
        /// 构建 Stage 组件路径
        /// </summary>
        public string BuildStagePath(string component)
        {
            return $"Stage/{stageName.Value}/Stage/{component}";
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
                largeTextureStore.Dispose();
                glyphStore.Dispose();
                danStore.Dispose();
                sampleStore.Dispose();

                if (Files is IDisposable filesDisposable)
                    filesDisposable.Dispose();
            }

            base.Dispose(isDisposing);
        }

        #endregion
    }
}
