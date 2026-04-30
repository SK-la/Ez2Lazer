// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        /// <summary>
        /// 获取纹理（自定义路径）
        /// </summary>
        /// <param name="path">完整路径</param>
        /// <param name="useLargeStore">是否使用大纹理存储</param>
        /// <returns>纹理对象</returns>
        public Texture Get(string path, bool useLargeStore = false)
        {
            return useLargeStore ? largeTextureStore.Get(path) : textureStore.Get(path);
        }

        /// <summary>
        /// 获取纹理（基础方法，从当前 note set 加载）
        /// </summary>
        /// <param name="component">组件名称（如 "whitenote"）</param>
        /// <returns>纹理对象</returns>
        public Texture GetNote(string component)
        {
            string path = $"note/{noteSetName.Value}/{component}";
            return textureStore.Get(path);
        }

        /// <summary>
        /// 获取 Stage 纹理
        /// </summary>
        /// <param name="component">组件名称</param>
        /// <returns>纹理对象</returns>
        public Texture GetStage(string component)
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
        /// 加载帧动画纹理列表
        /// </summary>
        /// <param name="basePath">基础路径（如 "note/xxx/component"）</param>
        /// <param name="maxFrames">最大帧数</param>
        /// <param name="scaleAdjust">缩放调整（默认 2.0）</param>
        /// <returns>纹理列表</returns>
        public List<Texture> LoadAnimationFrames(string basePath, int maxFrames = 240, float scaleAdjust = 2.0f)
        {
            var frames = new List<Texture>();

            for (int i = 0; i < maxFrames; i++)
            {
                string framePath = $"{basePath}/{i:D3}";
                Texture texture = textureStore.Get(framePath);

                if (texture == null)
                    break;

                texture.ScaleAdjust = scaleAdjust;
                frames.Add(texture);
            }

            return frames;
        }

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
