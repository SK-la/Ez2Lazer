// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.IO;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// Ez2 资源提供者 - 基于官方 IStorageResourceProvider 接口设计
    ///
    /// 设计理念：
    /// 1. 遵循官方 Skin 系统的资源管理模式
    /// 2. 提供统一的资源访问接口（纹理、样本）
    /// 3. 支持动态配置切换（note set、stage 等）
    /// 4. 与现有 EzLocalTextureFactory 共存，逐步迁移
    /// </summary>
    public partial class EzResourceProvider : Component, IStorageResourceProvider
    {
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

        // 纹理加载器链
        private readonly TextureStore textureStore;
        private readonly LargeTextureStore largeTextureStore;

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

        public EzResourceProvider(Ez2ConfigManager ezConfig, IRenderer renderer, AudioManager audioManager, Storage storage, RealmAccess realmAccess)
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

            // 创建组合资源存储：用户文件优先，DLL 回退
            var combinedStore = new ResourceStore<byte[]>();
            combinedStore.AddStore(Files);        // 首先查找用户文件
            combinedStore.AddStore(Resources);    // 找不到时回退到 DLL

            // 创建纹理加载器链（遵循官方模式）
            var baseTextureLoader = new TextureLoaderStore(combinedStore);
            IResourceStore<TextureUpload> textureLoaderStore1 = new MaxDimensionLimitedTextureLoaderStore(baseTextureLoader);

            // 创建纹理存储
            textureStore = new TextureStore(renderer, textureLoaderStore1);
            textureStore.AddTextureSource(baseTextureLoader);

            largeTextureStore = new LargeTextureStore(renderer, textureLoaderStore1);
            largeTextureStore.AddTextureSource(baseTextureLoader);

            // 创建样本存储
            sampleStore = audioManager.GetSampleStore(new NamespacedResourceStore<byte[]>(Files, "Samples"));
            sampleStore.AddExtension("ogg");

            // 绑定配置
            ezConfig.BindWith(Ez2Setting.NoteSetName, noteSetName);
            ezConfig.BindWith(Ez2Setting.StageName, stageName);
        }

        #endregion

        #region 纹理获取 API

        // TODO：添加新API，支持纹理或动画纹理，获取资源时，优先检查文件名格式，满足-#或_#，#为0表示列索引开头第一帧，且必须有下一帧时才按动画加载。
        // 同步问题是返回动画纹理时，调用方改成Drawable？相关属性定义也要修改。

        /// <summary>
        /// 获取纹理（自定义路径）
        /// </summary>
        /// <param name="path">完整路径</param>
        /// <param name="useLargeStore">是否使用大纹理存储</param>
        /// <returns>纹理对象</returns>
        public Texture? Get(string path, bool useLargeStore = false)
        {
            return useLargeStore ? largeTextureStore.Get(path) : textureStore.Get(path);
        }

        /// <summary>
        /// 获取纹理（基础方法，从当前 note set 加载）
        /// </summary>
        /// <param name="component">组件名称（如 "whitenote"）</param>
        /// <returns>纹理对象</returns>
        public Texture? GetNote(string component)
        {
            string path = $"note/{noteSetName.Value}/{component}";
            return textureStore.Get(path);
        }

        /// <summary>
        /// 获取 Stage 纹理
        /// </summary>
        /// <param name="component">组件名称</param>
        /// <returns>纹理对象</returns>
        public Texture? GetStage(string component)
        {
            string path = $"Stage/{stageName.Value}/Stage/{component}";
            return largeTextureStore.Get(path);
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

                // 尝试加载第一帧
                Texture texture = textureStore.Get($"{basePath}/000") ??
                                  textureStore.Get($"{basePath}/001");

                if (texture != null)
                {
                    float calculatedRatio = texture.Height / (float)texture.Width;
                    return calculatedRatio >= square_ratio_threshold ? 1.0f : calculatedRatio;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzResourceProvider] Error calculating ratio: {ex.Message}",
                    level: LogLevel.Debug);
            }

            return 1.0f;
        }

        #endregion

        #region 动画加载 API

        /// <summary>
        /// 获取纹理或动画纹理（默认按 "-" 再 "_" 作为动画分隔符探测）。
        /// </summary>
        /// <param name="componentName">组件基础路径（例如 "Column/ColumnLight"）</param>
        /// <param name="animatable">是否允许按帧动画探测</param>
        /// <param name="looping">当存在多帧时是否循环</param>
        /// <param name="startAtCurrentTime">是否从当前时间开始播放</param>
        /// <param name="frameLength">动画帧时长（毫秒），未指定时使用默认 60FPS</param>
        /// <param name="startFrameIndex">起始帧索引（默认 0）</param>
        /// <param name="useLargeStore">是否使用大纹理存储</param>
        public Drawable? GetAnimation(
            string componentName,
            bool animatable = true,
            bool looping = true,
            bool startAtCurrentTime = true,
            double? frameLength = null,
            int startFrameIndex = 0,
            bool useLargeStore = false)
        {
            Texture[] textures = GetTextures(componentName, animatable, new[] { "-", "_" }, startFrameIndex, useLargeStore);
            return createAnimationDrawable(textures, looping, startAtCurrentTime, frameLength);
        }

        /// <summary>
        /// 获取纹理或动画纹理（使用指定动画分隔符探测，例如 "-" 或 "_"）。
        /// </summary>
        /// <param name="componentName">组件基础路径（例如 "Column/ColumnLight"）</param>
        /// <param name="animationSeparator">动画分隔符（例如 "-" 或 "_"）</param>
        /// <param name="animatable">是否允许按帧动画探测</param>
        /// <param name="looping">当存在多帧时是否循环</param>
        /// <param name="startAtCurrentTime">是否从当前时间开始播放</param>
        /// <param name="frameLength">动画帧时长（毫秒），未指定时使用默认 60FPS</param>
        /// <param name="startFrameIndex">起始帧索引（默认 0）</param>
        /// <param name="useLargeStore">是否使用大纹理存储</param>
        public Drawable? GetAnimation(
            string componentName,
            string animationSeparator,
            bool animatable = true,
            bool looping = true,
            bool startAtCurrentTime = true,
            double? frameLength = null,
            int startFrameIndex = 0,
            bool useLargeStore = false)
        {
            Texture[] textures = GetTextures(componentName, animatable, animationSeparator, startFrameIndex, useLargeStore);
            return createAnimationDrawable(textures, looping, startAtCurrentTime, frameLength);
        }

        /// <summary>
        /// 获取纹理序列；当 animatable 为 true 时优先探测动画帧，否则仅取静态纹理。
        /// </summary>
        /// <param name="componentName">组件基础路径（例如 "Column/ColumnLight"）</param>
        /// <param name="animatable">是否允许按帧动画探测</param>
        /// <param name="animationSeparators">动画分隔符列表，按顺序探测</param>
        /// <param name="startFrameIndex">起始帧索引（默认 0）</param>
        /// <param name="useLargeStore">是否使用大纹理存储</param>
        /// <returns>纹理数组（0 张表示未找到）</returns>
        public Texture[] GetTextures(
            string componentName,
            bool animatable,
            IEnumerable<string> animationSeparators,
            int startFrameIndex = 0,
            bool useLargeStore = false)
        {
            if (animatable)
            {
                foreach (string separator in animationSeparators)
                {
                    var textures = getAnimatedTextures(componentName, separator, startFrameIndex, useLargeStore).ToArray();
                    if (textures.Length > 0)
                        return textures;
                }
            }

            Texture? singleTexture = Get(componentName, useLargeStore);
            return singleTexture != null ? new[] { singleTexture } : Array.Empty<Texture>();
        }

        /// <summary>
        /// 获取纹理序列；当 animatable 为 true 时优先探测动画帧，否则仅取静态纹理。
        /// </summary>
        /// <param name="componentName">组件基础路径（例如 "Column/ColumnLight"）</param>
        /// <param name="animatable">是否允许按帧动画探测</param>
        /// <param name="animationSeparator">动画分隔符（例如 "-" 或 "_"）</param>
        /// <param name="startFrameIndex">起始帧索引，默认 0</param>
        /// <param name="useLargeStore">是否使用大纹理存储</param>
        /// <returns>纹理数组（0 张表示未找到）</returns>
        public Texture[] GetTextures(
            string componentName,
            bool animatable,
            string animationSeparator,
            int startFrameIndex = 0,
            bool useLargeStore = false)
            => GetTextures(componentName, animatable, new[] { animationSeparator }, startFrameIndex, useLargeStore);

        private static Drawable? createAnimationDrawable(Texture[] textures, bool looping, bool startAtCurrentTime, double? frameLength)
        {
            switch (textures.Length)
            {
                case 0:
                    return null;

                case 1:
                    return new Sprite { Texture = textures[0] };

                default:
                    var animation = new TextureAnimation(startAtCurrentTime)
                    {
                        DefaultFrameLength = frameLength ?? 1000d / 60d,
                        Loop = looping,
                    };

                    foreach (Texture texture in textures)
                        animation.AddFrame(texture);

                    return animation;
            }
        }

        private IEnumerable<Texture> getAnimatedTextures(string componentName, string animationSeparator, int startFrameIndex, bool useLargeStore)
        {
            for (int i = 0;; i++)
            {
                int frameIndex = startFrameIndex + i;
                string framePath = buildIndexedFramePath(componentName, animationSeparator, frameIndex);
                Texture? texture = Get(framePath, useLargeStore);

                if (texture == null)
                    break;

                yield return texture;
            }
        }

        private static string buildIndexedFramePath(string componentName, string animationSeparator, int frameIndex)
            => $"{componentName}{animationSeparator}{frameIndex.ToString(CultureInfo.InvariantCulture)}";

        /// <summary>
        /// 加载 Stage 组件帧（支持多帧或单帧）
        /// </summary>
        /// <param name="basePath">基础路径</param>
        /// <returns>纹理列表</returns>
        public List<Texture> LoadStageFrames(string basePath)
        {
            var frames = new List<Texture>();

            // 尝试加载帧动画（xxx_0.png, xxx_1.png, ...）
            for (int i = 0;; i++)
            {
                Texture texture = textureStore.Get($"{basePath}_{i}.png");
                if (texture == null)
                    break;

                frames.Add(texture);
            }

            // 如果没有帧，加载单张纹理
            if (frames.Count == 0)
            {
                Texture texture = largeTextureStore.Get($"{basePath}.png");
                if (texture != null)
                    frames.Add(texture);
            }

            return frames;
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
                largeTextureStore.Dispose();
                sampleStore.Dispose();

                if (Files is IDisposable filesDisposable)
                    filesDisposable.Dispose();
            }

            base.Dispose(isDisposing);
        }

        #endregion
    }
}
