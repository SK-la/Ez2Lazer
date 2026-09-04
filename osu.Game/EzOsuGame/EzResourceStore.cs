// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// 纹理三路径（经 <see cref="EzTextureUsage"/> 选择，勿直接持有底层 store）：
    /// 1. <see cref="EzTextureUsage.Atlas"/> — 小 UI，可进 1024 atlas
    /// 2. <see cref="EzTextureUsage.AnimationSafe"/> — 多帧/循环动画（非 atlas，Dispose 为空操作）
    /// 3. <see cref="EzTextureUsage.Large"/> — 单帧大图（refcount，禁止给 TextureAnimation）
    /// </summary>
    public partial class EzResourceStore : Component, IStorageResourceProvider
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

        // 纹理加载器链（三路径，见 EzTextureUsage）
        private readonly TextureStore textureStore;
        private readonly TextureStore animationSafeStore;
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

            // 创建组合资源存储：用户文件优先，DLL 回退
            var combinedStore = new ResourceStore<byte[]>();
            combinedStore.AddStore(Files);        // 首先查找用户文件
            combinedStore.AddStore(Resources);    // 找不到时回退到 DLL

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

        /// <summary>
        /// 获取纹理（基础方法，从当前 note set 加载）。Note 帧走动画安全路径。
        /// </summary>
        /// <param name="component">组件名称（如 "whitenote"）</param>
        public Texture? GetNote(string component)
        {
            string path = $"note/{noteSetName.Value}/{component}";
            return Get(path, EzTextureUsage.AnimationSafe);
        }

        /// <summary>
        /// 获取 Stage 静态单帧大图（Large）。多帧 Stage 请用 <see cref="LoadStageFrames"/> 或 AnimationSafe。
        /// </summary>
        /// <param name="component">组件名称</param>
        public Texture? GetStage(string component)
        {
            string path = $"Stage/{stageName.Value}/Stage/{component}";
            return Get(path, EzTextureUsage.Large);
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
                Logger.Log($"[EzTextureStore] Error calculating ratio: {ex.Message}",
                    level: LogLevel.Debug);
            }

            return 1.0f;
        }

        #endregion

        #region 动画加载 API

        /// <summary>
        /// 获取纹理或动画纹理（默认按 "-" 再 "_" 作为动画分隔符探测）。
        /// 帧一律走 <see cref="EzTextureUsage.AnimationSafe"/>。
        /// </summary>
        public Drawable? GetAnimation(
            string componentName,
            bool animatable = true,
            bool looping = true,
            bool startAtCurrentTime = true,
            double? frameLength = null,
            int startFrameIndex = 0)
        {
            Texture[] textures = GetTextures(componentName, animatable, new[] { "-", "_" }, startFrameIndex, EzTextureUsage.AnimationSafe);
            return createAnimationDrawable(textures, looping, startAtCurrentTime, frameLength);
        }

        /// <summary>
        /// 获取纹理或动画纹理（使用指定动画分隔符探测）。
        /// 帧一律走 <see cref="EzTextureUsage.AnimationSafe"/>。
        /// </summary>
        public Drawable? GetAnimation(
            string componentName,
            string animationSeparator,
            bool animatable = true,
            bool looping = true,
            bool startAtCurrentTime = true,
            double? frameLength = null,
            int startFrameIndex = 0)
        {
            Texture[] textures = GetTextures(componentName, animatable, animationSeparator, startFrameIndex, EzTextureUsage.AnimationSafe);
            return createAnimationDrawable(textures, looping, startAtCurrentTime, frameLength);
        }

        /// <summary>
        /// 兼容旧签名：忽略 <paramref name="useLargeStore"/>，动画帧固定 AnimationSafe。
        /// </summary>
        [Obsolete("GetAnimation 已固定使用 AnimationSafe，请去掉 useLargeStore 参数。")]
        public Drawable? GetAnimation(
            string componentName,
            bool animatable,
            bool looping,
            bool startAtCurrentTime,
            double? frameLength,
            int startFrameIndex,
            bool useLargeStore)
            => GetAnimation(componentName, animatable, looping, startAtCurrentTime, frameLength, startFrameIndex);

        /// <summary>
        /// 获取纹理序列；当 animatable 为 true 时优先探测动画帧，否则仅取静态纹理。
        /// </summary>
        public Texture[] GetTextures(
            string componentName,
            bool animatable,
            IEnumerable<string> animationSeparators,
            int startFrameIndex = 0,
            EzTextureUsage usage = EzTextureUsage.AnimationSafe)
        {
            if (animatable)
            {
                foreach (string separator in animationSeparators)
                {
                    var textures = getAnimatedTextures(componentName, separator, startFrameIndex, usage).ToArray();
                    if (textures.Length > 0)
                        return textures;
                }
            }

            Texture? singleTexture = Get(componentName, usage);
            return singleTexture != null ? new[] { singleTexture } : Array.Empty<Texture>();
        }

        /// <summary>
        /// 获取纹理序列（单一分隔符）。
        /// </summary>
        public Texture[] GetTextures(
            string componentName,
            bool animatable,
            string animationSeparator,
            int startFrameIndex = 0,
            EzTextureUsage usage = EzTextureUsage.AnimationSafe)
            => GetTextures(componentName, animatable, new[] { animationSeparator }, startFrameIndex, usage);

        /// <summary>
        /// 兼容旧签名：<c>useLargeStore</c> 仅影响「非动画」单帧回退；多帧探测始终 AnimationSafe。
        /// </summary>
        [Obsolete("请使用 GetTextures(..., EzTextureUsage)。")]
        public Texture[] GetTextures(
            string componentName,
            bool animatable,
            IEnumerable<string> animationSeparators,
            int startFrameIndex,
            bool useLargeStore)
            => GetTextures(componentName, animatable, animationSeparators, startFrameIndex,
                useLargeStore ? EzTextureUsage.Large : EzTextureUsage.AnimationSafe);

        /// <summary>
        /// 兼容旧签名。
        /// </summary>
        [Obsolete("请使用 GetTextures(..., EzTextureUsage)。")]
        public Texture[] GetTextures(
            string componentName,
            bool animatable,
            string animationSeparator,
            int startFrameIndex,
            bool useLargeStore)
            => GetTextures(componentName, animatable, animationSeparator, startFrameIndex,
                useLargeStore ? EzTextureUsage.Large : EzTextureUsage.AnimationSafe);

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

        private IEnumerable<Texture> getAnimatedTextures(string componentName, string animationSeparator, int startFrameIndex, EzTextureUsage usage)
        {
            for (int i = 0;; i++)
            {
                int frameIndex = startFrameIndex + i;
                string framePath = buildIndexedFramePath(componentName, animationSeparator, frameIndex);
                Texture? texture = Get(framePath, usage);

                if (texture == null)
                    break;

                yield return texture;
            }
        }

        private static string buildIndexedFramePath(string componentName, string animationSeparator, int frameIndex)
            => $"{componentName}{animationSeparator}{frameIndex.ToString(CultureInfo.InvariantCulture)}";

        /// <summary>
        /// 使用帧路径模板加载纹理序列（相对 <paramref name="baseDirectory"/> 的路径）。
        /// 占位符：<c>{result}</c> 为判定名；<c>{0}</c>、<c>{00}</c>、<c>{000}</c> 等为指定宽度的帧序号。
        /// 帧走 <see cref="EzTextureUsage.AnimationSafe"/>。
        /// </summary>
        public Drawable? GetAnimationFromTemplate(
            string baseDirectory,
            string resultName,
            string frameTemplate,
            bool looping = true,
            bool startAtCurrentTime = true,
            double? frameLength = null)
        {
            if (string.IsNullOrWhiteSpace(frameTemplate))
                return null;

            var textures = new List<Texture>();

            for (int i = 0;; i++)
            {
                string relativePath = formatJudgementFrameTemplate(frameTemplate, resultName, i);
                string fullPath = $"{baseDirectory}{relativePath}";
                Texture? texture = Get(fullPath, EzTextureUsage.AnimationSafe);

                if (texture == null)
                    break;

                textures.Add(texture);
            }

            return createAnimationDrawable(textures.ToArray(), looping, startAtCurrentTime, frameLength);
        }

        /// <summary>
        /// 兼容旧签名：忽略 <paramref name="useLargeStore"/>。
        /// </summary>
        [Obsolete("GetAnimationFromTemplate 已固定使用 AnimationSafe，请去掉 useLargeStore 参数。")]
        public Drawable? GetAnimationFromTemplate(
            string baseDirectory,
            string resultName,
            string frameTemplate,
            bool looping,
            bool startAtCurrentTime,
            double? frameLength,
            bool useLargeStore)
            => GetAnimationFromTemplate(baseDirectory, resultName, frameTemplate, looping, startAtCurrentTime, frameLength);

        private static string formatJudgementFrameTemplate(string template, string resultName, int frameIndex)
        {
            string formatted = template.Replace("{result}", resultName, StringComparison.Ordinal);

            return Regex.Replace(formatted, @"\{(0+)\}", m =>
            {
                int width = Math.Clamp(m.Groups[1].Value.Length, 1, 9);
                return frameIndex.ToString($"D{width}", CultureInfo.InvariantCulture);
            });
        }

        /// <summary>
        /// 加载 Stage 组件帧：多帧走 AnimationSafe；仅单帧时走 Large。
        /// </summary>
        /// <param name="basePath">基础路径（不含扩展名）</param>
        public List<Texture> LoadStageFrames(string basePath)
        {
            var frames = new List<Texture>();

            for (int i = 0;; i++)
            {
                // TextureStore 自行探测扩展名，勿带 .png 以免路径重复
                Texture? texture = Get($"{basePath}_{i}", EzTextureUsage.AnimationSafe);
                if (texture == null)
                    break;

                frames.Add(texture);
            }

            if (frames.Count == 0)
            {
                Texture? texture = Get(basePath, EzTextureUsage.Large);
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
                animationSafeStore.Dispose();
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
