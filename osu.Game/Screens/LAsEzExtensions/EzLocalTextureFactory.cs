using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
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
        // 常量定义
        private const int max_cache_size = 100;
        private const int max_frames_to_load = 240;
        private const int max_stage_frames = 120;
        private const double default_frame_length = 1000.0 / 60.0 * 4;
        private const float default_stage_body_height = 247f;

        // 全局缓存，带LRU机制
        private static readonly Dictionary<string, List<Texture>> global_texture_cache = new Dictionary<string, List<Texture>>();
        private static readonly Dictionary<string, bool> global_component_cache = new Dictionary<string, bool>();
        private static readonly Dictionary<string, DateTime> cache_access_times = new Dictionary<string, DateTime>();
        private static readonly object global_cache_lock = new object();

        // 实例级缓存，统一使用全局锁
        private readonly Dictionary<string, float> noteRatioCache = new Dictionary<string, float>();

        private readonly TextureStore textureStore;
        private readonly EzSkinSettingsManager ezSkinConfig;
        private readonly Dictionary<string, TextureLoaderStore> loaderStoreCache = new Dictionary<string, TextureLoaderStore>();
        private bool initialized;

        public Bindable<string> NoteSetName { get; set; } = new Bindable<string>();
        public Bindable<string> StageName { get; set; } = new Bindable<string>();

        public Bindable<double> ColumnWidth = new Bindable<double>();
        public Bindable<double> SpecialFactor = new Bindable<double>();
        public Bindable<double> NoteHeightScaleToWidth = new Bindable<double>();

        public event Action? OnNoteChanged;
        public event Action? OnNoteSizeChanged;
        public event Action? OnStageChanged;

        public EzLocalTextureFactory(
            EzSkinSettingsManager ezSkinConfig,
            TextureStore textureStore,
            Storage hostStorage)
        {
            this.ezSkinConfig = ezSkinConfig;
            this.textureStore = textureStore;
            // this.hostStorage = hostStorage;

            const string path = "EzResources/";

            if (!loaderStoreCache.TryGetValue(path, out var textureLoaderStore))
            {
                var storage = hostStorage.GetStorageForDirectory(path);
                var fileStore = new StorageBackedResourceStore(storage);
                textureLoaderStore = new TextureLoaderStore(fileStore);
                loaderStoreCache[path] = textureLoaderStore;
                textureStore.AddTextureSource(textureLoaderStore);
            }

            initialize();
        }

        private void initialize()
        {
            if (initialized) return;

            initialized = true;

            NoteSetName = ezSkinConfig.GetBindable<string>(EzSkinSetting.NoteSetName);
            NoteSetName.BindValueChanged(e =>
            {
                OnNoteChanged?.Invoke();
            }, true);

            StageName = ezSkinConfig.GetBindable<string>(EzSkinSetting.StageName);
            StageName.BindValueChanged(e =>
            {
                OnStageChanged?.Invoke();
            }, true);

            ColumnWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            SpecialFactor = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);
            NoteHeightScaleToWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.NoteHeightScaleToWidth);
            // ColumnWidth.BindValueChanged(e =>
            // {
            //     OnNoteSizeChanged?.Invoke();
            // }, true);
            // SpecialFactor.BindValueChanged(e =>
            // {
            //     OnNoteSizeChanged?.Invoke();
            // }, true);
            // NoteHeightScaleToWidth.BindValueChanged(e =>
            // {
            //     OnNoteSizeChanged?.Invoke();
            // }, true);
        }

        #region 工具方法

        private float calculateRatio(string path)
        {
            try
            {
                string noteSetName = NoteSetName.Value;
                string cacheKey = $"{noteSetName}_{System.IO.Path.GetFileName(path)}";

                lock (global_cache_lock)
                {
                    if (global_texture_cache.TryGetValue(cacheKey, out var cachedFrames) && cachedFrames.Count > 0)
                    {
                        var firstFrame = cachedFrames[0];
                        return (firstFrame.Height / (float)firstFrame.Width);
                    }
                }

                Texture texture = textureStore.Get($"{path}/000.png") ??
                                  textureStore.Get($"{path}/001.png");

                return (texture?.Height / (float)(texture?.Width!)) ?? 1.0f;
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
            string? noteName = NoteSetName.Value;
            string path = $"note/{noteName}/{component}";

            lock (global_cache_lock)
            {
                if (noteRatioCache.TryGetValue(path, out float cachedRatio))
                    return cachedRatio;
            }

            float ratio = calculateRatio(path);
            if (ratio >= 0.75f) ratio = 1.0f;

            lock (global_cache_lock)
            {
                noteRatioCache[path] = ratio;
            }

            return ratio;
        }

        public bool IsSquareNote(string component)
        {
            float ratio = GetRatio(component);
            return ratio >= 0.75f;
        }

        public Bindable<Vector2> GetNoteSize(int keyMode, int columnIndex)
        {
            var result = new Bindable<Vector2>();

            float ratio = GetRatio("whitenote");

            void updateNoteSize()
            {
                bool isSpecialColumn = ezSkinConfig.GetColumnType(keyMode, columnIndex) == "S";
                float x = (float)(ColumnWidth.Value * (isSpecialColumn ? SpecialFactor.Value : 1.0));
                float y = (float)(NoteHeightScaleToWidth.Value) * ratio * x;
                result.Value = new Vector2(x, y);
            }

            ColumnWidth.BindValueChanged(_ => updateNoteSize());
            SpecialFactor.BindValueChanged(_ => updateNoteSize());
            NoteHeightScaleToWidth.BindValueChanged(_ => updateNoteSize());

            updateNoteSize();

            return result;
        }

        #endregion

        #region 缓存处理

        private static void cleanOldCacheEntries()
        {
            if (global_texture_cache.Count <= max_cache_size) return;

            var oldestEntries = cache_access_times
                                .OrderBy(kvp => kvp.Value)
                                .Take(global_texture_cache.Count - max_cache_size + 10)
                                .Select(kvp => kvp.Key)
                                .ToList();

            foreach (string key in oldestEntries)
            {
                global_texture_cache.Remove(key);
                global_component_cache.Remove(key);
                cache_access_times.Remove(key);
            }

            Logger.Log($"[EzLocalTextureFactory] Cleaned {oldestEntries.Count} old cache entries",
                LoggingTarget.Performance, LogLevel.Debug);
        }

        private List<Texture> getCachedTextureFrames(string component, string currentNoteSetName)
        {
            string cacheKey = $"{currentNoteSetName}_{component}";

            lock (global_cache_lock)
            {
                if (global_texture_cache.TryGetValue(cacheKey, out var cachedFrames))
                {
                    cache_access_times[cacheKey] = DateTime.Now;
                    return cachedFrames;
                }
            }

            var frames = loadTextureFrames(component, currentNoteSetName);

            lock (global_cache_lock)
            {
                cleanOldCacheEntries();

                if (frames.Count > 0)
                {
                    global_texture_cache[cacheKey] = frames;
                    global_component_cache[cacheKey] = true;
                    cache_access_times[cacheKey] = DateTime.Now;
                }
                else
                {
                    global_component_cache[cacheKey] = false;
                }
            }

            return frames;
        }

        public static void ClearGlobalCache()
        {
            lock (global_cache_lock)
            {
                Logger.Log($"[EzLocalTextureFactory] Clearing global cache ({global_texture_cache.Count} entries)",
                    LoggingTarget.Performance, LogLevel.Important);

                global_texture_cache.Clear();
                global_component_cache.Clear();
                cache_access_times.Clear();
            }
        }

        private List<Texture> loadTextureFrames(string component, string noteSetName)
        {
            var frames = new List<Texture>();

            string[] pathsToTry = new[]
            {
                $"note/{noteSetName}/{component}",
                $"note/circle/{component}"
            };

            foreach (string basePath in pathsToTry)
            {
                try
                {
                    for (int i = 0; i < max_frames_to_load; i++)
                    {
                        string frameFile = $"{basePath}/{i:D3}.png";
                        var texture = textureStore.Get(frameFile);

                        if (texture == null) break;

                        frames.Add(texture);
                    }

                    if (frames.Count > 0)
                        break;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error loading texture frames for {basePath}: {ex.Message}",
                        LoggingTarget.Runtime, LogLevel.Error);
                }
            }

            return frames;
        }

        #endregion

        #region Note Animation Creation

        private bool isHitCom(string componentName)
        {
            return componentName.Contains("flare", StringComparison.InvariantCultureIgnoreCase);
        }

        public virtual TextureAnimation CreateAnimation(string component, int? columnIndex = null)
        {
            string currentNoteSetName = NoteSetName.Value;
            // string cacheKey = $"{currentNoteSetName}_{component}";
            // bool fromCache;
            //
            // lock (global_cache_lock)
            // {
            //     fromCache = global_texture_cache.ContainsKey(cacheKey);
            // }

// #if DEBUG
//             string callerType = new System.Diagnostics.StackTrace().GetFrame(1)?.GetMethod()?.DeclaringType?.Name ?? "Unknown";
//             Logger.Log($"[EzLocalTextureFactory] Creating texture {currentNoteSetName}/{component} from {callerType} (cached: {fromCache})",
//                 LoggingTarget.Runtime, LogLevel.Debug);
// #endif

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

// #if DEBUG
//             if (frames.Count > 0)
//             {
//                 Logger.Log($"[EzLocalTextureFactory] Created animation for {component} with {frames.Count} frames",
//                     LoggingTarget.Runtime, LogLevel.Debug);
//             }
// #endif

            foreach (var texture in frames)
            {
                animation.AddFrame(texture);
            }

            return animation;
        }

        #endregion

        #region Stage Creation

        private bool isStageBody(string componentName)
        {
            return componentName.Contains("Body", StringComparison.InvariantCultureIgnoreCase);
        }

        public virtual Drawable CreateStage(string component)
        {
            bool isBody = isStageBody(component);
            string basePath = $"Stage/{StageName.Value}/Stage";

            var container = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                AutoSizeAxes = Axes.Both,
                FillMode = FillMode.Fill,
            };

            if (isBody)
            {
                container = new Container
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
            }
            else
            {
                createStageKeys(container, basePath, component);
            }

            return container;
        }

        private void addStageComponent(Container container, string basePath, int frameIndex = -1)
        {
            Texture? texture = loadStageTexture(basePath, frameIndex);
            if (texture == null) return;

            container.Add(new Sprite
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.None,
                FillMode = FillMode.Fill,
                Width = texture.Width,
                Height = texture.Height,
                Texture = texture,
            });

#if DEBUG
            Logger.Log($"[EzLocalTextureFactory] Loaded stage component: {basePath}",
                LoggingTarget.Runtime, LogLevel.Debug);
#endif
        }

        private void createStageAnimation(Container container, string basePath)
        {
            string overObjectPath = $"{basePath}/Stage/{StageName.Value}_OverObject/{StageName.Value}_OverObject";

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
                    var texture = textureStore.Get($"{overObjectPath}_{i}.png");
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

        private void createStageKeys(Container container, string basePath, string component)
        {
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
        }

        private Texture? loadStageTexture(string basePath, int i = -1)
        {
            string texturePath = i < 0
                ? $"{basePath}.png"
                : (i == 0 ? $"{basePath}.png" : $"{basePath}_{i}.png");

            return textureStore.Get(texturePath);
        }

        #endregion

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
