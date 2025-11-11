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
using osu.Framework.Graphics.Sprites;
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
        private static EzLocalTextureFactory? instance;
        private static readonly object instance_lock = new object();

        public static EzLocalTextureFactory? GetInstance() => instance;

        private const int max_cache_size = 100;
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

        private Bindable<string> noteSetName = null!;
        private Bindable<string> stageName = null!;
        private Bindable<double> columnWidth = null!;
        private Bindable<double> specialFactor = null!;
        private Bindable<double> noteHeightScaleToWidth = null!;

        private bool initialized;

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
            lock (instance_lock)
            {
                if (instance != null)
                {
                    Logger.Log("[EzLocalTextureFactory] WARNING: Multiple instances detected!",
                        LoggingTarget.Runtime, LogLevel.Important);
                }

                instance = this;
            }

            this.ezSkinConfig = ezSkinConfig;
            textureStore = new TextureStore(renderer);
            largeTextureStore = new LargeTextureStore(renderer);

            const string path = "EzResources/";

            if (!loaderStoreCache.TryGetValue(path, out var textureLoaderStore))
            {
                var storage = hostStorage.GetStorageForDirectory(path);
                var fileStore = new StorageBackedResourceStore(storage);
                textureLoaderStore = new TextureLoaderStore(fileStore);
                loaderStoreCache[path] = textureLoaderStore;
                textureStore.AddTextureSource(textureLoaderStore);
                largeTextureStore.AddTextureSource(textureLoaderStore);
            }

            initialize();
        }

        private void initialize()
        {
            if (initialized)
                return;

            initialized = true;

            noteSetName = ezSkinConfig.GetBindable<string>(EzSkinSetting.NoteSetName);
            stageName = ezSkinConfig.GetBindable<string>(EzSkinSetting.StageName);
            columnWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);
            noteHeightScaleToWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.NoteHeightScaleToWidth);

            noteSetName.BindValueChanged(e =>
            {
                ForceRefreshCache();
                clearRelatedCache(e.OldValue, e.NewValue);
            });

            stageName.BindValueChanged(e =>
            {
                clearStageCache(e.OldValue, e.NewValue);
            });
        }

        private void clearRelatedCache(string? oldNoteSet, string newNoteSet)
        {
            if (string.IsNullOrEmpty(oldNoteSet)) return;

            var keysToRemove = new List<string>();

            foreach (string key in global_cache.Keys)
            {
                if (key.StartsWith($"{oldNoteSet}_", StringComparison.Ordinal))
                    keysToRemove.Add(key);
            }

            foreach (string key in keysToRemove)
            {
                global_cache.TryRemove(key, out _);
            }

            var pathKeysToRemove = pathCache.Keys.Where(k => k.StartsWith($"{oldNoteSet}_", StringComparison.Ordinal)).ToList();

            foreach (string key in pathKeysToRemove)
            {
                pathCache.TryRemove(key, out _);
            }

            var ratioKeysToRemove = noteRatioCache.Keys.Where(k =>
                k.Contains($"note/{oldNoteSet}/", StringComparison.Ordinal) ||
                k.StartsWith($"{oldNoteSet}_", StringComparison.Ordinal)).ToList();

            foreach (string key in ratioKeysToRemove)
            {
                noteRatioCache.TryRemove(key, out _);
            }

            var singleTextureKeysToRemove = singleTextureCache.Keys.Where(k =>
                k.Contains($"note/{oldNoteSet}/", StringComparison.Ordinal) ||
                k.Contains($"/{oldNoteSet}/", StringComparison.Ordinal)).ToList();

            foreach (string key in singleTextureKeysToRemove)
            {
                singleTextureCache.TryRemove(key, out _);
            }

            resetPreloadState();

            var newNoteKeysToRemove = new List<string>();

            foreach (string key in global_cache.Keys)
            {
                if (key.StartsWith($"{newNoteSet}_", StringComparison.Ordinal))
                    newNoteKeysToRemove.Add(key);
            }

            foreach (string key in newNoteKeysToRemove)
            {
                global_cache.TryRemove(key, out _);
            }

            var newPathKeysToRemove = pathCache.Keys.Where(k => k.StartsWith($"{newNoteSet}_", StringComparison.Ordinal)).ToList();

            foreach (string key in newPathKeysToRemove)
            {
                pathCache.TryRemove(key, out _);
            }

            var newRatioKeysToRemove = noteRatioCache.Keys.Where(k =>
                k.Contains($"note/{newNoteSet}/", StringComparison.Ordinal) ||
                k.StartsWith($"{newNoteSet}_", StringComparison.Ordinal)).ToList();

            foreach (string key in newRatioKeysToRemove)
            {
                noteRatioCache.TryRemove(key, out _);
            }

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

            // Logger.Log($"[EzLocalTextureFactory] Cleared cache for note set change: {oldNoteSet} -> {newNoteSet} " +
            //            $"(Removed {keysToRemove.Count + newNoteKeysToRemove.Count} texture cache, {pathKeysToRemove.Count + newPathKeysToRemove.Count} path cache, {ratioKeysToRemove.Count + newRatioKeysToRemove.Count} ratio cache)",
            //     LoggingTarget.Runtime, LogLevel.Debug);
        }

        private void clearStageCache(string? oldStage, string newStage)
        {
            if (string.IsNullOrEmpty(oldStage)) return;

            var stageTextureKeysToRemove = singleTextureCache.Keys.Where(k =>
                k.Contains($"/{oldStage}/", StringComparison.Ordinal) ||
                k.Contains($"{oldStage}_", StringComparison.Ordinal)).ToList();

            foreach (string key in stageTextureKeysToRemove)
            {
                singleTextureCache.TryRemove(key, out _);
            }

            Logger.Log($"[EzLocalTextureFactory] Cleared stage cache for change: {oldStage} -> {newStage}",
                LoggingTarget.Runtime, LogLevel.Debug);
        }

        public void ForceRefreshCache()
        {
            int singleCacheCount = singleTextureCache.Count;
            int globalCacheCount = global_cache.Count;

            Logger.Log($"[EzLocalTextureFactory] Clearing caches: {singleCacheCount} single textures, {globalCacheCount} frame sets",
                LoggingTarget.Runtime, LogLevel.Debug);

            noteRatioCache.Clear();
            pathCache.Clear();
            clearSingleTextureCache();
            ClearGlobalCache();
        }

        #region 工具方法

        private string getComponentPath(string noteName, string component)
        {
            string key = $"{noteName}_{component}";
            return pathCache.GetOrAdd(key, _ => $"note/{noteName}/{component}");
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

            return noteRatioCache.GetOrAdd(path, p =>
            {
                float ratio = calculateRatio(p);
                return ratio >= square_ratio_threshold ? 1.0f : ratio;
            });
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

        private List<Texture> getCachedTextureFrames(string component, string currentNoteSetName)
        {
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
                var frames = loadTextureFrames(component, currentNoteSetName);

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

        private void clearSingleTextureCache()
        {
            int count = singleTextureCache.Count;

            if (count > 0)
            {
                Logger.Log($"[EzLocalTextureFactory] Clearing single texture cache ({count})",
                    LoggingTarget.Runtime, LogLevel.Debug);
                singleTextureCache.Clear();
            }
        }

        private List<Texture> loadTextureFrames(string component, string noteSetName)
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
            string currentNoteSetName = noteSetName.Value;

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

            var frames = getCachedTextureFrames(component, currentNoteSetName);

            foreach (var texture in frames)
            {
                animation.AddFrame(texture);
            }

            return animation;
        }

        #endregion

        #region Stage Creation

        // private bool isStageBody(string componentName)
        // {
        //     return componentName.Contains("Body", StringComparison.InvariantCultureIgnoreCase);
        // }

        public virtual Drawable CreateStage(string component)
        {
            // if (!isStageBody(component))
            //     throw new ArgumentException("CreateStage only handles Body components. Use CreateStageKeys for key components.", nameof(component));

            string basePath = $"Stage/{stageName.Value}/Stage";

            var container = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                FillMode = FillMode.Fill,
                Y = 0,
                AutoSizeAxes = Axes.X,
                Height = default_stage_body_height,
                Masking = true,
            };

            addStageComponent(container, $"{basePath}/fivekey/{component}");
            addStageComponent(container, $"{basePath}/GrooveLight");
            createStageAnimation(container, basePath);

            return container;
        }

        public virtual Container CreateStageKeys(string component)
        {
            string basePath = $"Stage/{stageName.Value}/Stage";

            var container = new Container
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                AutoSizeAxes = Axes.Both,
                FillMode = FillMode.Fill,
            };

            string baseKeyPath = $"{basePath}/eightkey/{component}";
            string[] pathsToTry =
            {
                $"{baseKeyPath}/KeyBase",
                $"{baseKeyPath}/KeyPress",
                $"{baseKeyPath}/KeyBase_0",
                $"{baseKeyPath}/KeyPress_0",
                $"{baseKeyPath}/2KeyBase_0",
                $"{baseKeyPath}/2KeyPress_0",
            };

            foreach (string path in pathsToTry)
            {
                addStageComponent(container, path);
            }

            return container;
        }

        private void addStageComponent(Container container, string basePath, int frameIndex = -1)
        {
            Texture? texture = loadStageTexture(basePath, frameIndex);
            if (texture == null) return;

            const int medium_texture_threshold = 384;

            bool isLargeTexture = Math.Max(texture.Width, texture.Height) > medium_texture_threshold;

            if (isLargeTexture)
            {
                Logger.Log($"[EzLocalTextureFactory] Large texture detected: {basePath} ({texture.Width}x{texture.Height}) - bypassing atlas",
                    LoggingTarget.Runtime, LogLevel.Debug);
                // For large textures, try to load from large texture store to bypass atlas
                string texturePath = frameIndex < 0
                    ? $"{basePath}.png"
                    : (frameIndex == 0 ? $"{basePath}.png" : $"{basePath}_{frameIndex}.png");

                var largeTexture = largeTextureStore.Get(texturePath);
                if (largeTexture != null)
                {
                    texture = largeTexture;
                }
            }

            var sprite = new Sprite
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.None,
                FillMode = FillMode.Fill,
                Width = texture.Width,
                Height = texture.Height,
                Texture = texture,
            };

            container.Add(sprite);
        }

        private void createStageAnimation(Container container, string basePath)
        {
            string overObjectPath = $"{basePath}/{stageName.Value}_OverObject/{stageName.Value}_OverObject";

            var animation = new TextureAnimation
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.None,
                FillMode = FillMode.Fill,
                Loop = true,
                DefaultFrameLength = default_frame_length
            };

            try
            {
                for (int i = 0; i < max_stage_frames; i++)
                {
                    var texture = getCachedTexture($"{overObjectPath}_{i}.png");
                    if (texture == null)
                        break;

                    animation.AddFrame(texture);
                }

                if (animation.FrameCount > 0)
                    container.Add(animation);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error loading stage animation: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
            }
        }

        private Texture? loadStageTexture(string basePath, int i = -1)
        {
            string texturePath = i < 0
                ? $"{basePath}.png"
                : (i == 0 ? $"{basePath}.png" : $"{basePath}_{i}.png");

            return getCachedTexture(texturePath);
        }

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
            }
            else
            {
#if DEBUG
                Logger.Log($"[EzLocalTextureFactory] Failed to load texture: {texturePath}",
                    LoggingTarget.Runtime, LogLevel.Debug);
#endif
            }

            return texture;
        }

        #endregion

        #region 预加载系统

        private static readonly string[] preload_components =
        {
            "whitenote", "bluenote", "greennote",
            "noteflare", "noteflaregood", "longnoteflare",
        };

        private static readonly object preload_lock = new object();
        private static bool isPreloading;
        private static bool preloadCompleted;

        public async Task PreloadGameTextures()
        {
            if (preloadCompleted || isPreloading) return;

            lock (preload_lock)
            {
                if (preloadCompleted || isPreloading) return;

                isPreloading = true;
            }

            try
            {
                string currentNoteSetName = noteSetName.Value;
                Logger.Log($"[EzLocalTextureFactory] Starting preload for note set: {currentNoteSetName}",
                    LoggingTarget.Runtime, LogLevel.Debug);

                var preloadTasks = new List<Task>();

                foreach (string component in preload_components)
                {
                    preloadTasks.Add(Task.Run(() => preloadComponent(component, currentNoteSetName)));
                }

                preloadTasks.Add(Task.Run(preloadStageTextures));

                await Task.WhenAll(preloadTasks).ConfigureAwait(false);

                lock (preload_lock)
                {
                    preloadCompleted = true;
                    isPreloading = false;
                }

                Logger.Log($"[EzLocalTextureFactory] Preload completed for {preload_components.Length} components",
                    LoggingTarget.Runtime, LogLevel.Debug);

                Logger.Log($"[EzLocalTextureFactory] Cache stats after preload: {singleTextureCache.Count} single textures, {global_cache.Count} frame sets",
                    LoggingTarget.Runtime, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzLocalTextureFactory] Preload failed: {ex.Message}",
                    LoggingTarget.Runtime, LogLevel.Error);

                lock (preload_lock)
                {
                    isPreloading = false;
                }
            }
        }

        private void preloadComponent(string component, string noteSetName)
        {
            try
            {
                string cacheKey = $"{noteSetName}_{component}";

                if (global_cache.ContainsKey(cacheKey)) return;

                var frames = loadTextureFrames(component, noteSetName);

                if (frames.Count > 0)
                {
                    var newEntry = new CacheEntry(frames, true);
                    global_cache.TryAdd(cacheKey, newEntry);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzLocalTextureFactory] Failed to preload {component}: {ex.Message}",
                    LoggingTarget.Runtime, LogLevel.Error);
            }
        }

        private async Task preloadStageTextures()
        {
            try
            {
                string currentStageName = stageName.Value;
                Logger.Log($"[EzLocalTextureFactory] Preloading stage textures for: {currentStageName}",
                    LoggingTarget.Runtime, LogLevel.Debug);

                var stagePaths = new List<string>
                {
                    $"Stage/{currentStageName}/Stage/fivekey/Body",
                    $"Stage/{currentStageName}/Stage/GrooveLight",
                    $"Stage/{currentStageName}/Stage/eightkey/keybase/KeyBase",
                    $"Stage/{currentStageName}/Stage/eightkey/keypress/KeyBase",
                    $"Stage/{currentStageName}/Stage/eightkey/keypress/KeyPress",
                };

                int loadedCount = 0;

                foreach (string path in stagePaths)
                {
                    var texture = getCachedTexture($"{path}.png");
                    if (texture != null)
                        loadedCount++;

                    Logger.Log($"[EzLocalTextureFactory] preload stage texture {path}",
                        LoggingTarget.Runtime, LogLevel.Debug);

                    if (loadedCount % 2 == 0)
                    {
                        await Task.Delay(10).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzLocalTextureFactory] Stage texture preload failed: {ex.Message}",
                    LoggingTarget.Runtime, LogLevel.Error);
            }
        }

        private void resetPreloadState()
        {
            lock (preload_lock)
            {
                preloadCompleted = false;
                isPreloading = false;
            }
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
