using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Screens;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.LAsEzExtensions
{
    [Cached]
    public partial class EzLocalTextureFactory : CompositeDrawable
    {
        // private const int max_stage_frames = 120;
        private const int max_frames_to_load = 240;
        private const double default_frame_length = 1000.0 / 60.0 * 4;
        private const float default_stage_body_height = 247f;
        private const float square_ratio_threshold = 0.75f;

        private static readonly object cleanup_lock = new object();
        private static readonly ConcurrentDictionary<string, CacheEntry> global_cache = new ConcurrentDictionary<string, CacheEntry>();
        private static readonly ConcurrentDictionary<string, float> note_ratio_cache = new ConcurrentDictionary<string, float>();

        private readonly Dictionary<string, TextureLoaderStore> loaderStoreCache = new Dictionary<string, TextureLoaderStore>();
        private readonly Ez2ConfigManager ezSkinConfig;

        private readonly LargeTextureStore stageTextureStore;
        private readonly TextureStore textureStore;

        private readonly Bindable<string> noteSetName;
        private readonly Bindable<string> stageName;
        private readonly Bindable<double> columnWidth;
        private readonly Bindable<double> specialFactor;
        private readonly Bindable<double> hitPositonBindable;
        private readonly Bindable<double> noteHeightScaleToWidth;

        private Vector2 tempNoteSize;

        private readonly struct CacheEntry : IEquatable<CacheEntry>
        {
            public readonly List<Texture>? Textures;
            public readonly bool HasComponent;
            public readonly DateTime LastAccess;

            public CacheEntry(List<Texture>? textures, bool hasComponent)
            {
                Textures = textures;
                HasComponent = hasComponent;
                LastAccess = DateTime.UtcNow;
            }

            public CacheEntry UpdateAccess() => new CacheEntry(Textures, HasComponent);

            public bool Equals(CacheEntry other) =>
                ReferenceEquals(Textures, other.Textures) &&
                HasComponent == other.HasComponent &&
                LastAccess.Equals(other.LastAccess);

            public override bool Equals(object? obj) => obj is CacheEntry other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Textures, HasComponent, LastAccess);
        }

        public EzLocalTextureFactory(Ez2ConfigManager ezSkinConfig,
                                     IRenderer renderer,
                                     Storage hostStorage)
        {
            this.ezSkinConfig = ezSkinConfig;

            const string base_path = "EzResources/";

            if (!loaderStoreCache.TryGetValue(base_path, out var baseTextureLoaderStore))
            {
                var baseStorage = hostStorage.GetStorageForDirectory(base_path);
                var baseFileStore = new StorageBackedResourceStore(baseStorage);
                baseTextureLoaderStore = new TextureLoaderStore(baseFileStore);
                loaderStoreCache[base_path] = baseTextureLoaderStore;
            }

            // 创建尺寸限制的纹理加载器（使用官方的 MaxDimensionLimitedTextureLoaderStore）
            var limitedLoader = new MaxDimensionLimitedTextureLoaderStore(baseTextureLoaderStore);
            textureStore = new TextureStore(renderer, limitedLoader);
            textureStore.AddTextureSource(baseTextureLoaderStore);

            stageTextureStore = new LargeTextureStore(renderer, limitedLoader);
            stageTextureStore.AddTextureSource(baseTextureLoaderStore);

            noteSetName = ezSkinConfig.GetBindable<string>(Ez2Setting.NoteSetName);
            stageName = ezSkinConfig.GetBindable<string>(Ez2Setting.StageName);
            columnWidth = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(Ez2Setting.SpecialFactor);
            hitPositonBindable = ezSkinConfig.GetBindable<double>(Ez2Setting.HitPosition);
            noteHeightScaleToWidth = ezSkinConfig.GetBindable<double>(Ez2Setting.NoteHeightScaleToWidth);

            // noteSetName.BindValueChanged(e =>
            // {
            //     clearRelatedCache(e.OldValue);
            // });

            // stageName.BindValueChanged(e =>
            // {
            // });
        }

        #region 工具方法

        // public bool IsSquareNote(string component) => GetRatio(component) >= square_ratio_threshold;

        public float GetRatio()
        {
            string noteSet = noteSetName.Value;

            float ratio = note_ratio_cache.GetOrAdd(noteSet, ns =>
            {
                string path = getComponentPath(ns, "whitenote"); // Use a representative component
                float calculatedRatio = calculateRatio(path);
                return calculatedRatio >= square_ratio_threshold ? 1.0f : calculatedRatio;
            });

            return ratio;
        }

        public Bindable<Vector2> GetNoteSize(int keyMode, int columnIndex, bool? noSpecial = null)
        {
            var result = new Bindable<Vector2>();
            float ratio = GetRatio();
            bool isSpecialColumn = noSpecial != true && ezSkinConfig.IsSpecialColumn(keyMode, columnIndex);

            void updateNoteSize()
            {
                float x = (float)(columnWidth.Value * (isSpecialColumn ? specialFactor.Value : 1.0));
                float y = (float)noteHeightScaleToWidth.Value * ratio * x;
                tempNoteSize.X = x;
                tempNoteSize.Y = y;
                result.Value = tempNoteSize;
            }

            columnWidth.BindValueChanged(_ => updateNoteSize());
            specialFactor.BindValueChanged(_ => updateNoteSize());
            noteHeightScaleToWidth.BindValueChanged(_ => updateNoteSize());

            updateNoteSize();
            return result;
        }

        private string getComponentPath(string noteName, string component)
        {
            return $"note/{noteName}/{component}";
        }

        private float calculateRatio(string path)
        {
            try
            {
                if (global_cache.TryGetValue(path, out var cacheEntry) &&
                    cacheEntry.Textures is not null && cacheEntry.Textures.Count > 0)
                {
                    var firstFrame = cacheEntry.Textures[0];
                    return firstFrame.Height / (float)firstFrame.Width;
                }

                var sb = new StringBuilder(path.Length + 8);
                Texture? texture = textureStore.Get(sb.Append(path).Append("/000.png").ToString()) ??
                                   textureStore.Get(sb.Clear().Append(path).Append("/001.png").ToString());

                return texture?.Height / (texture?.Width ?? 1f) ?? 1.0f;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error calculating ratio for {path}: {ex.Message}",
                    LoggingTarget.Runtime, LogLevel.Error);
                return 1.0f;
            }
        }

        private static bool isStageTexturePath(string texturePath)
        {
            return texturePath.Contains("Stage/") ||
                   texturePath.Contains("/Body") ||
                   texturePath.Contains("/GrooveLight") ||
                   texturePath.Contains("keybase") ||
                   texturePath.Contains("keypress") ||
                   texturePath.Contains("_OverObject");
        }

        #endregion

        #region 组件构造

        /// <summary>
        /// 构造Note、光效等动画组件。
        /// </summary>
        /// <param name="component"></param>
        /// <param name="isFlare">是否为光效</param>
        /// <returns></returns>
        public virtual TextureAnimation CreateAnimation(string component, bool? isFlare = null)
        {
            bool isHit = isFlare is true;

            var animation = new TextureAnimation
            {
                Anchor = isHit ? Anchor.BottomCentre : Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = isHit ? Axes.None : Axes.Both,
                FillMode = isHit ? FillMode.Fit : FillMode.Stretch,
                Loop = !isHit
            };

            if (!isHit)
            {
                animation.DefaultFrameLength = default_frame_length;
                // animation.Blending = BlendingParameters.Inherit;
            }

            if (component == "JudgementLine")
                FillMode = FillMode.Fill;

            var frames = getCachedTextureFrames(component);
            animation.AddFrames(frames);

            return animation;
        }

        private List<Texture> getCachedTextureFrames(string component)
        {
            string currentNoteSetName = noteSetName.Value;
            string cacheKey = $"{currentNoteSetName}_{component}";
            return getOrAddCachedFrames(cacheKey, () => loadNotesFrames(component, currentNoteSetName));
        }

        private List<Texture> loadNotesFrames(string component, string noteSetName)
        {
            var frames = new List<Texture>();
            string basePath = $"note/{noteSetName}/{component}";

            if (component != "JudgementLine")
            {
                for (int i = 0; i < max_frames_to_load; i++)
                {
                    string frameFile = $"{basePath}/{i:D3}.png";
                    var texture = textureStore.Get(frameFile);

                    if (texture == null) break;

                    if (texture.Width < 500) texture.ScaleAdjust = 0.5f; // 大纹理缩小加载，防止内存暴涨

                    frames.Add(texture);
                }
            }
            else
            {
                string frameFile = $"{basePath}.png";
                Logger.Log($"[EzLocalTextureFactory] Loading JudgementLine Frame: {frameFile}",
                    LoggingTarget.Runtime, LogLevel.Debug);
                var texture = textureStore.Get(frameFile);

                frames.Add(texture);
            }

            return new List<Texture>(frames);
        }

        #endregion

        #region Stage Creation

        public virtual Container CreateStage(string component)
        {
            var container = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,

                // AutoSizeAxes = Axes.X,
                // Height = default_stage_body_height,
                // Masking = true,
            };
            // hitPositonBindable.BindValueChanged(_ =>
            // {
            //     container.Y = ezSkinConfig.DefaultHitPosition - (float)hitPositonBindable.Value;
            // }, true);

            string basePath = $"Stage/{stageName.Value}/Stage";

            addStageComponent(container, $"{basePath}/eightkey/{component}");
            addStageComponent(container, $"{basePath}/GrooveLight"); //此纹理需要修改正片叠底
            addStageComponent(container, $"{basePath}/{stageName.Value}_OverObject/{stageName.Value}_OverObject");

            return container;
        }

        private void addStageComponent(Container container, string basePath)
        {
            var frames = loadStageComponentFrames(basePath);
            var animation = new TextureAnimation
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = 384f + 247f,
                // RelativeSizeAxes = Axes.None,
                // FillMode = FillMode.Fill,
            };
            if (basePath.Contains("GrooveLight"))
                animation.Blending = BlendingParameters.Additive;

            animation.Loop = frames.Count > 1;
            animation.Scale = frames.Count > 1
                ? new Vector2(2f)
                : Vector2.One;

            animation.AddFrames(frames);
            container.Add(animation);
        }

        private List<Texture> loadStageComponentFrames(string basePath)
        {
            var frames = new List<Texture>();

            for (int i = 0;; i++)
            {
                Texture? texture = textureStore.Get($"{basePath}_{i}.png");

                if (texture == null) break;

                Logger.Log($"[EzLocalTextureFactory] Added Stage Frames: {basePath}_{i}.png",
                    LoggingTarget.Runtime, LogLevel.Debug);

                frames.Add(texture);
            }

            if (frames.Count == 0)
            {
                Texture? texture = stageTextureStore.Get($"{basePath}.png");

                if (texture != null)
                {
                    Logger.Log($"[EzLocalTextureFactory] Added Stage Frame: {basePath}",
                        LoggingTarget.Runtime, LogLevel.Debug);
                    frames.Add(texture);
                }
            }

            return frames;
        }

        public virtual TextureAnimation CreateStageKeys(string component, string? keySuffix = null)
        {
            var frames = getCachedStageKeysFrames(component, keySuffix);
            var animation = new TextureAnimation
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                RelativeSizeAxes = Axes.None,
                FillMode = FillMode.Stretch,
                DefaultFrameLength = default_frame_length * 4,
            };
            animation.AddFrames(frames);

            return animation;
        }

        private List<Texture> getCachedStageKeysFrames(string component, string? keySuffix = null)
        {
            string currentStageName = stageName.Value;
            string cacheKey = $"{currentStageName}_{component}_{keySuffix}";
            return getOrAddCachedFrames(cacheKey, () => loadStageKeysFrames(component, keySuffix));
        }

        private List<Texture> loadStageKeysFrames(string component, string? keySuffix = null)
        {
            var frames = new List<Texture>();
            string currentStageName = stageName.Value;

            string[] pathsToTry =
            {
                $"Stage/{currentStageName}/Stage/eightkey/keybase/{component}",
                $"Stage/{currentStageName}/Stage/eightkey/keypress/{component}",
                $"Stage/{currentStageName}/Stage/eightkey/keybase/{component}_{keySuffix}",
                $"Stage/{currentStageName}/Stage/eightkey/keypress/{component}_{keySuffix}",
            };

            foreach (string basePath in pathsToTry)
            {
                for (int i = 0;; i++)
                {
                    Texture? texture = textureStore.Get($"{basePath}_frame{i}.png");

                    if (texture == null) break;

                    Logger.Log($"[EzLocalTextureFactory] Added Keys Frames: {basePath}_{i}",
                        LoggingTarget.Runtime, LogLevel.Debug);

                    frames.Add(texture);
                }

                // 如果没有帧，加载单个纹理作为单帧
                if (frames.Count == 0)
                {
                    Texture? texture = textureStore.Get($"{basePath}.png");

                    if (texture != null)
                    {
                        Logger.Log($"[EzLocalTextureFactory] Added Keys Frame: {basePath}",
                            LoggingTarget.Runtime, LogLevel.Debug);
                        frames.Add(texture);
                    }
                }
            }

            return frames;
        }

        #endregion

        #region 缓存管理

        private List<Texture> getOrAddCachedFrames(string cacheKey, Func<List<Texture>> factory)
        {
            // 双重检查锁定以确保线程安全，获取或添加缓存纹理
            lock (cleanup_lock)
            {
                if (global_cache.TryGetValue(cacheKey, out var cachedEntry))
                {
                    if (cachedEntry.Textures != null && cachedEntry.Textures.Count > 0)
                    {
                        global_cache.TryUpdate(cacheKey, cachedEntry.UpdateAccess(), cachedEntry);
                        return cachedEntry.Textures;
                    }

                    global_cache.TryRemove(cacheKey, out _);
                }

                var frames = factory();

                if (frames.Count > 0)
                {
                    Logger.Log($"[EzLocalTextureFactory] global_cache Caching {frames.Count} frames for {cacheKey}",
                        LoggingTarget.Runtime, LogLevel.Debug);

                    var newEntry = new CacheEntry(frames, true);
                    global_cache.TryAdd(cacheKey, newEntry);
                }

                return frames;
            }
        }

        public static void ClearGlobalCache()
        {
            int count1 = note_ratio_cache.Count;

            if (count1 > 0)
            {
                Logger.Log($"[EzLocalTextureFactory] Clearing note_ratio_cache ({count1})",
                    LoggingTarget.Runtime, LogLevel.Debug);

                note_ratio_cache.Clear();
            }

            int count2 = global_cache.Count;

            if (count2 > 0)
            {
                Logger.Log($"[EzLocalTextureFactory] Clearing global_cache ({count2})",
                    LoggingTarget.Runtime, LogLevel.Debug);
                global_cache.Clear();
            }
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // 只清理实例级别的缓存，全局缓存留给 ClearGlobalCache 处理
                foreach (var loader in loaderStoreCache.Values)
                    loader.Dispose();

                loaderStoreCache.Clear();
                note_ratio_cache.Clear();
            }

            base.Dispose(isDisposing);
        }
    }

    public enum EzAnimationType
    {
        Note,
        Hit,
        Stage,
        Key,
        Health,
    }
}
