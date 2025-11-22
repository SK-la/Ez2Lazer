using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
using osuTK;

namespace osu.Game.Screens.LAsEzExtensions
{
    [Cached]
    public partial class EzLocalTextureFactory : CompositeDrawable
    {
        private const int max_cache_size = 100;
        private const int max_single_texture_cache_size = 200;
        private const int max_note_ratio_cache_size = 50;
        private const int max_path_cache_size = 50;
        private const int max_frames_to_load = 240;
        private const int max_stage_frames = 120;
        private const double default_frame_length = 1000.0 / 60.0 * 4;
        private const float default_stage_body_height = 247f;
        private const float square_ratio_threshold = 0.75f;

        private static readonly ConcurrentDictionary<string, CacheEntry> global_cache = new ConcurrentDictionary<string, CacheEntry>();
        private static readonly object cleanup_lock = new object();

        private readonly ConcurrentDictionary<string, float> noteRatioCache = new ConcurrentDictionary<string, float>();
        private readonly ConcurrentDictionary<string, string> pathCache = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, Texture> singleTextureCache = new ConcurrentDictionary<string, Texture>();

        private readonly TextureStore textureStore;
        private readonly LargeTextureStore largeTextureStore;
        private readonly EzSkinSettingsManager ezSkinConfig;
        private readonly Dictionary<string, TextureLoaderStore> loaderStoreCache = new Dictionary<string, TextureLoaderStore>();

        private readonly Bindable<string> noteSetName;
        private readonly Bindable<string> stageName;
        private readonly Bindable<double> columnWidth;
        private readonly Bindable<double> specialFactor;
        private readonly Bindable<double> hitPositonBindable;
        private readonly Bindable<double> noteHeightScaleToWidth;

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

        public EzLocalTextureFactory(EzSkinSettingsManager ezSkinConfig,
            IRenderer renderer,
            Storage hostStorage)
        {
            this.ezSkinConfig = ezSkinConfig;
            textureStore = new TextureStore(renderer);
            largeTextureStore = new LargeTextureStore(renderer); //stage相关纹理专用

            const string base_path = "EzResources/";
            const string stage_path = "EzResources/";

            if (!loaderStoreCache.TryGetValue(base_path, out var baseTextureLoaderStore))
            {
                var baseStorage = hostStorage.GetStorageForDirectory(base_path);
                var baseFileStore = new StorageBackedResourceStore(baseStorage);
                baseTextureLoaderStore = new TextureLoaderStore(baseFileStore);
                loaderStoreCache[base_path] = baseTextureLoaderStore;
                textureStore.AddTextureSource(baseTextureLoaderStore);

                var stageStorage = hostStorage.GetStorageForDirectory(stage_path);
                var stageFileStore = new StorageBackedResourceStore(stageStorage);
                var stageTextureLoaderStore = new TextureLoaderStore(stageFileStore);
                largeTextureStore.AddTextureSource(stageTextureLoaderStore);
            }

            noteSetName = ezSkinConfig.GetBindable<string>(EzSkinSetting.NoteSetName);
            stageName = ezSkinConfig.GetBindable<string>(EzSkinSetting.StageName);
            columnWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);
            hitPositonBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            noteHeightScaleToWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.NoteHeightScaleToWidth);

            noteSetName.BindValueChanged(e =>
            {
                ForceRefreshCache();
                clearRelatedCache(e.OldValue, e.NewValue);
            });

            stageName.BindValueChanged(e =>
            {
            });
        }

        private void clearCache<T>(ConcurrentDictionary<string, T> cache, Func<string, bool> predicate, string cacheName)
        {
            var keysToRemove = cache.Keys.Where(predicate).ToList();
            int removedCount = 0;

            foreach (string key in keysToRemove)
            {
                if (cache.TryRemove(key, out _))
                    removedCount++;
            }

            if (removedCount > 0)
            {
                Logger.Log($"[EzLocalTextureFactory] Cleared {removedCount} entries from {cacheName}",
                    LoggingTarget.Runtime, LogLevel.Debug);
            }
        }

        private void clearRelatedCache(string? oldNoteSet, string newNoteSet)
        {
            if (string.IsNullOrEmpty(oldNoteSet)) return;

            clearCache(global_cache, k => k.StartsWith($"{oldNoteSet}_", StringComparison.Ordinal), "global_cache");
            clearCache(pathCache, k => k.StartsWith($"{oldNoteSet}_", StringComparison.Ordinal), "pathCache");
            clearCache(noteRatioCache, k => k.Contains($"note/{oldNoteSet}/", StringComparison.Ordinal) || k.StartsWith($"{oldNoteSet}_", StringComparison.Ordinal), "noteRatioCache");
            clearCache(singleTextureCache, k => k.Contains($"note/{oldNoteSet}/", StringComparison.Ordinal) || k.Contains($"/{oldNoteSet}/", StringComparison.Ordinal), "singleTextureCache");

            resetPreloadState();

            clearCache(global_cache, k => k.StartsWith($"{newNoteSet}_", StringComparison.Ordinal), "global_cache");
            clearCache(pathCache, k => k.StartsWith($"{newNoteSet}_", StringComparison.Ordinal), "pathCache");
            clearCache(noteRatioCache, k => k.Contains($"note/{newNoteSet}/", StringComparison.Ordinal) || k.StartsWith($"{newNoteSet}_", StringComparison.Ordinal), "noteRatioCache");

            Schedule(() =>
            {
                foreach (string component in preload_components)
                {
                    string path = getComponentPath(newNoteSet, component);
                    noteRatioCache.TryRemove(path, out _);
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await PreloadGameTextures().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[EzLocalTextureFactory] Background preload failed: {ex.Message}",
                            LoggingTarget.Runtime, LogLevel.Verbose);
                    }
                });
            });
        }

        public void ForceRefreshCache()
        {
            int singleCacheCount = singleTextureCache.Count;
            int globalCacheCount = global_cache.Count;

            Logger.Log($"[EzLocalTextureFactory] Clearing caches: {singleCacheCount} single textures, {globalCacheCount} frame sets",
                LoggingTarget.Runtime, LogLevel.Debug);

            noteRatioCache.Clear();
            pathCache.Clear();
            singleTextureCache.Clear();
            ClearGlobalCache();
        }

        #region 工具方法

        private string getComponentPath(string noteName, string component)
        {
            string key = $"{noteName}_{component}";
            string path = pathCache.GetOrAdd(key, _ => $"note/{noteName}/{component}");

            if (pathCache.Count > max_path_cache_size)
            {
                Task.Run(cleanPathCache);
            }

            return path;
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
                Texture? texture = getCachedTexture(sb.Append(path).Append("/000.png").ToString()) ??
                                   getCachedTexture(sb.Clear().Append(path).Append("/001.png").ToString());

                return texture?.Height / (texture?.Width ?? 1f) ?? 1.0f;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error calculating ratio for {path}: {ex.Message}",
                    LoggingTarget.Runtime, LogLevel.Error);
                return 1.0f;
            }
        }

        public float GetRatio(string component)
        {
            string path = getComponentPath(noteSetName.Value, component);

            float ratio = noteRatioCache.GetOrAdd(path, p =>
            {
                float calculatedRatio = calculateRatio(p);
                return calculatedRatio >= square_ratio_threshold ? 1.0f : calculatedRatio;
            });

            if (noteRatioCache.Count > max_note_ratio_cache_size)
            {
                Task.Run(cleanNoteRatioCache);
            }

            return ratio;
        }

        public bool IsSquareNote(string component) => GetRatio(component) >= square_ratio_threshold;

        public Bindable<Vector2> GetNoteSize(int keyMode, int columnIndex)
        {
            var result = new Bindable<Vector2>();
            float ratio = GetRatio("whitenote");
            bool isSpecialColumn = ezSkinConfig.IsSpecialColumn(keyMode, columnIndex);

            void updateNoteSize()
            {
                float x = (float)(columnWidth.Value * (isSpecialColumn ? specialFactor.Value : 1.0));
                float y = (float)noteHeightScaleToWidth.Value * ratio * x;
                result.Value = new Vector2(x, y);
            }

            columnWidth.BindValueChanged(_ => updateNoteSize());
            specialFactor.BindValueChanged(_ => updateNoteSize());
            noteHeightScaleToWidth.BindValueChanged(_ => updateNoteSize());

            updateNoteSize();
            return result;
        }

        #endregion

        #region 优化的缓存处理

        private static void cleanOldCacheEntries()
        {
            if (global_cache.Count <= max_cache_size) return;

            lock (cleanup_lock)
            {
                if (global_cache.Count <= max_cache_size) return;

                string[] entriesToRemove = global_cache
                                           .OrderBy(kvp => kvp.Value.LastAccess)
                                           .Take(global_cache.Count - max_cache_size + 10)
                                           .Select(kvp => kvp.Key)
                                           .ToArray();

                int removedCount = 0;

                foreach (string key in entriesToRemove)
                {
                    if (global_cache.TryRemove(key, out _))
                        removedCount++;
                }

                if (removedCount > 0)
                {
                    Logger.Log($"[EzLocalTextureFactory] Cleaned {removedCount} old cache entries",
                        LoggingTarget.Performance, LogLevel.Debug);
                }
            }
        }

        private List<Texture> getCachedTextureFrames(string component)
        {
            string currentNoteSetName = noteSetName.Value;
            string cacheKey = $"{currentNoteSetName}_{component}";

            // 双重检查锁定模式，避免并发时重复创建
            if (global_cache.TryGetValue(cacheKey, out var cachedEntry))
            {
                if (cachedEntry.Textures != null && cachedEntry.Textures.Count > 0)
                {
                    global_cache.TryUpdate(cacheKey, cachedEntry.UpdateAccess(), cachedEntry);
                    return cachedEntry.Textures;
                }

                global_cache.TryRemove(cacheKey, out _);
            }

            // 使用锁确保同一组件只加载一次
            lock (cleanup_lock)
            {
                // 再次检查，防止在等待锁的过程中其他线程已经加载了
                if (global_cache.TryGetValue(cacheKey, out cachedEntry))
                {
                    if (cachedEntry.Textures != null && cachedEntry.Textures.Count > 0)
                    {
                        global_cache.TryUpdate(cacheKey, cachedEntry.UpdateAccess(), cachedEntry);
                        return cachedEntry.Textures;
                    }

                    global_cache.TryRemove(cacheKey, out _);
                }

                // 加载纹理帧
                var frames = loadNotesFrames(component, currentNoteSetName);

                // 只缓存有效的帧数据，不缓存空结果
                if (frames.Count > 0)
                {
                    var newEntry = new CacheEntry(frames, true);
                    global_cache.TryAdd(cacheKey, newEntry);

                    if (global_cache.Count > max_cache_size)
                    {
                        Task.Run(cleanOldCacheEntries);
                    }
                }

                return frames;
            }
        }

        public static void ClearGlobalCache()
        {
            int count = global_cache.Count;

            if (count > 0)
            {
                Logger.Log($"[EzLocalTextureFactory] Clearing global cache ({count})",
                    LoggingTarget.Runtime, LogLevel.Debug);
                global_cache.Clear();
            }
        }

        private void cleanSingleTextureCache()
        {
            if (singleTextureCache.Count <= max_single_texture_cache_size) return;

            lock (cleanup_lock)
            {
                if (singleTextureCache.Count <= max_single_texture_cache_size) return;

                // Simple cleanup: remove oldest entries (assuming insertion order, but ConcurrentDictionary doesn't guarantee order)
                // For simplicity, remove a fixed number
                var keysToRemove = singleTextureCache.Keys.Take(singleTextureCache.Count - max_single_texture_cache_size + 10).ToList();

                int removedCount = 0;

                foreach (string key in keysToRemove)
                {
                    if (singleTextureCache.TryRemove(key, out _))
                        removedCount++;
                }

                if (removedCount > 0)
                {
                    Logger.Log($"[EzLocalTextureFactory] Cleaned {removedCount} old single texture cache entries",
                        LoggingTarget.Performance, LogLevel.Debug);
                }
            }
        }

        private void cleanNoteRatioCache()
        {
            if (noteRatioCache.Count <= max_note_ratio_cache_size) return;

            lock (cleanup_lock)
            {
                if (noteRatioCache.Count <= max_note_ratio_cache_size) return;

                var keysToRemove = noteRatioCache.Keys.Take(noteRatioCache.Count - max_note_ratio_cache_size + 5).ToList();

                int removedCount = 0;

                foreach (string key in keysToRemove)
                {
                    if (noteRatioCache.TryRemove(key, out _))
                        removedCount++;
                }

                if (removedCount > 0)
                {
                    Logger.Log($"[EzLocalTextureFactory] Cleaned {removedCount} old note ratio cache entries",
                        LoggingTarget.Performance, LogLevel.Debug);
                }
            }
        }

        private void cleanPathCache()
        {
            if (pathCache.Count <= max_path_cache_size) return;

            lock (cleanup_lock)
            {
                if (pathCache.Count <= max_path_cache_size) return;

                var keysToRemove = pathCache.Keys.Take(pathCache.Count - max_path_cache_size + 5).ToList();

                int removedCount = 0;

                foreach (string key in keysToRemove)
                {
                    if (pathCache.TryRemove(key, out _))
                        removedCount++;
                }

                if (removedCount > 0)
                {
                    Logger.Log($"[EzLocalTextureFactory] Cleaned {removedCount} old path cache entries",
                        LoggingTarget.Performance, LogLevel.Debug);
                }
            }
        }

        private List<Texture> loadNotesFrames(string component, string noteSetName)
        {
            var frames = new List<Texture>();
            string[] pathsToTry =
            {
                $"note/{noteSetName}/{component}",
                $"note/circle/{component}"
            };

            foreach (string basePath in pathsToTry)
            {
                frames.Clear();

                for (int i = 0; i < max_frames_to_load; i++)
                {
                    try
                    {
                        string frameFile = $"{basePath}/{i:D3}.png";
                        var texture = getCachedTexture(frameFile);

                        if (texture == null)
                            break;

                        frames.Add(texture);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Error loading frame {i} for {basePath}: {ex.Message}",
                            LoggingTarget.Runtime, LogLevel.Error);
                        break;
                    }
                }

                if (frames.Count > 0) break;
            }

            return new List<Texture>(frames);
        }

        #endregion

        #region Note Animation Creation

        private bool isHitCom(string componentName)
        {
            return componentName.Contains("flare", StringComparison.InvariantCultureIgnoreCase);
        }

        public virtual TextureAnimation CreateAnimation(string component)
        {
            bool isHit = isHitCom(component);
            var animation = new TextureAnimation
            {
                Anchor = isHit ? Anchor.BottomCentre : Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = isHit ? Axes.None : Axes.Both,
                FillMode = isHit ? FillMode.Fit : FillMode.Stretch,
                Loop = !isHit
            };

            if (!isHit) animation.DefaultFrameLength = default_frame_length;

            var frames = getCachedTextureFrames(component);

            foreach (var texture in frames)
            {
                animation.AddFrame(texture);
            }

            return animation;
        }

        #endregion

        #region Stage Creation

        public virtual Drawable CreateStage(string component)
        {
            string basePath = $"Stage/{stageName.Value}/Stage";

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

            addStageComponent(container, $"{basePath}/fivekey/{component}");
            // addStageComponent(container, $"{basePath}/GrooveLight"); //此纹理需要修改正片叠底
            addStageComponent(container, $"{basePath}/{stageName.Value}_OverObject/{stageName.Value}_OverObject");

            return container;
        }

        private void addStageComponent(Container container, string basePath)
        {
            var animation = new TextureAnimation
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = 384f + 247f,
                RelativeSizeAxes = Axes.None,
                FillMode = FillMode.Fill,
                DefaultFrameLength = default_frame_length
            };

            for (int i = 0; i < max_stage_frames; i++)
            {
                string framePath = $"{basePath}_{i}.png";
                var texture = textureStore.Get(framePath);

                if (texture == null) break;

                animation.AddFrame(texture);
            }

            if (animation.FrameCount == 0)
            {
                Texture? singleTexture = largeTextureStore.Get($"{basePath}.png");

                animation.AddFrame(singleTexture);
            }

            // 如果只有1帧，设置为非循环（静态）；否则循环
            animation.Loop = animation.FrameCount > 1;

            container.Add(animation);
            Logger.Log($"[EzLocalTextureFactory] Added stage component with {animation.FrameCount} frames ({(animation.Loop ? "animated" : "static")})",
                LoggingTarget.Runtime, LogLevel.Debug);
        }

        public virtual Container CreateStageKeys(string component)
        {
            var container = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                AutoSizeAxes = Axes.Both,
                FillMode = FillMode.Fill,
            };

            string baseKeyPath = $"Stage/{stageName.Value}/Stage/eightkey/{component}";
            string[] pathsToTry =
            {
                $"{baseKeyPath}/KeyBase",
                $"{baseKeyPath}/KeyPress",
                $"{baseKeyPath}/2KeyBase",
                $"{baseKeyPath}/2KeyPress",
            };

            foreach (string path in pathsToTry)
            {
                var animation = new TextureAnimation
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.None,
                    FillMode = FillMode.Fill,
                    DefaultFrameLength = default_frame_length / 4,
                };

                // 加载帧序列
                for (int i = 0; i < max_stage_frames; i++)
                {
                    string framePath = $"{path}_{i}.png";
                    Texture? texture = textureStore.Get(framePath);

                    if (texture == null) break;

                    animation.AddFrame(texture);
                }

                // 如果没有帧，加载单个纹理作为单帧
                if (animation.FrameCount == 0)
                {
                    Texture? singleTexture = textureStore.Get($"{path}.png");

                    if (singleTexture == null) break;

                    animation.AddFrame(singleTexture);
                }

                // 设置循环模式：多帧循环，单帧不循环
                animation.Loop = animation.FrameCount > 1;

                container.Add(animation);

                Logger.Log($"[EzLocalTextureFactory] Added stage key animation with {animation.FrameCount} frames for {path} (Loop: {animation.Loop})",
                    LoggingTarget.Runtime, LogLevel.Debug);
            }

            return container;
        }

        /// <summary>
        /// 小纹理使用，不建议用于stage相关纹理。
        /// </summary>
        /// <param name="texturePath"></param>
        /// <returns></returns>
        private Texture? getCachedTexture(string texturePath)
        {
            if (singleTextureCache.TryGetValue(texturePath, out var cachedTexture))
            {
#if DEBUG
                Logger.Log($"[EzLocalTextureFactory] Cache hit for: {texturePath}",
                    LoggingTarget.Runtime, LogLevel.Debug);
#endif
                return cachedTexture;
            }

            var texture = textureStore.Get(texturePath);

            if (texture != null)
            {
                bool added = singleTextureCache.TryAdd(texturePath, texture);
#if DEBUG
                Logger.Log($"[EzLocalTextureFactory] Loaded texture from disk: {texturePath} ({texture.Width}x{texture.Height}) - Cache add: {added}",
                    LoggingTarget.Runtime, LogLevel.Debug);
#endif
                if (added && singleTextureCache.Count > max_single_texture_cache_size)
                {
                    Task.Run(cleanSingleTextureCache);
                }
            }

            return texture;
        }

        #endregion

        private bool isStageTexturePath(string texturePath)
        {
            return texturePath.Contains("Stage/") ||
                texturePath.Contains("/Body") ||
                texturePath.Contains("/GrooveLight") ||
                texturePath.Contains("keybase") ||
                texturePath.Contains("keypress") ||
                texturePath.Contains("_OverObject");
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (var loader in loaderStoreCache.Values)
                    loader.Dispose();
                loaderStoreCache.Clear();

                // Clear instance caches
                noteRatioCache.Clear();
                pathCache.Clear();
                singleTextureCache.Clear();
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
