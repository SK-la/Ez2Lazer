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
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.LAsEzExtensions
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
        private readonly EzSkinSettingsManager ezSkinConfig;

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

        public EzLocalTextureFactory(EzSkinSettingsManager ezSkinConfig,
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

            noteSetName = ezSkinConfig.GetBindable<string>(EzSkinSetting.NoteSetName);
            stageName = ezSkinConfig.GetBindable<string>(EzSkinSetting.StageName);
            columnWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);
            hitPositonBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            noteHeightScaleToWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.NoteHeightScaleToWidth);

            noteSetName.BindValueChanged(e =>
            {
                clearRelatedCache(e.OldValue);
            });

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

        public Bindable<Vector2> GetNoteSize(int keyMode, int columnIndex)
        {
            var result = new Bindable<Vector2>();
            float ratio = GetRatio();
            bool isSpecialColumn = ezSkinConfig.IsSpecialColumn(keyMode, columnIndex);

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

            if (!isHit) animation.DefaultFrameLength = default_frame_length;

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

        private List<Texture> getOrAddCachedFrames(string cacheKey, Func<List<Texture>> factory)
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

            lock (cleanup_lock)
            {
                if (global_cache.TryGetValue(cacheKey, out cachedEntry))
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
                    var newEntry = new CacheEntry(frames, true);
                    global_cache.TryAdd(cacheKey, newEntry);
                }

                return frames;
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
                        var texture = textureStore.Get(frameFile);

                        if (texture == null) break;

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
            var frames = loadStageComponentFrames(basePath);
            var animation = new TextureAnimation
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = 384f + 247f,
                // RelativeSizeAxes = Axes.None,
                // FillMode = FillMode.Fill,
            };

            animation.Loop = frames.Count > 1;
            animation.Scale = frames.Count > 1
                ? new Vector2(2f)
                : Vector2.One;
            animation.AddFrames(frames);

            container.Add(animation);
            Logger.Log($"[EzLocalTextureFactory] Added stage component with {animation.FrameCount} frames ({(animation.Loop ? "animated" : "static")})",
                LoggingTarget.Runtime, LogLevel.Debug);
        }

        private List<Texture> loadStageComponentFrames(string basePath)
        {
            var frames = new List<Texture>();

            for (int i = 0;; i++)
            {
                Texture? texture = textureStore.Get($"{basePath}_{i}.png");

                if (texture == null) break;

                frames.Add(texture);
            }

            if (frames.Count == 0)
            {
                Texture? singleTexture = stageTextureStore.Get($"{basePath}.png");

                if (singleTexture != null)
                    frames.Add(singleTexture);
            }

            return frames;
        }

        public virtual TextureAnimation CreateStageKeys(string component)
        {
            var frames = getCachedStageKeysFrames(component);
            var animation = new TextureAnimation
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                RelativeSizeAxes = Axes.None,
                FillMode = FillMode.Fill,
                Loop = frames.Count > 1
            };
            animation.AddFrames(frames);
            return animation;
        }

        private List<Texture> getCachedStageKeysFrames(string component)
        {
            string currentStageName = stageName.Value;
            string cacheKey = $"stagekeys_{currentStageName}_{component}";
            return getOrAddCachedFrames(cacheKey, () => loadStageKeysFrames(component, currentStageName));
        }

        private List<Texture> loadStageKeysFrames(string component, string stageName)
        {
            var frames = new List<Texture>();
            string baseKeyPath = $"Stage/{stageName}/Stage/eightkey/{component}";
            string[] pathsToTry =
            {
                $"{baseKeyPath}/KeyBase",
                $"{baseKeyPath}/KeyPress",
                $"{baseKeyPath}/2KeyBase",
                $"{baseKeyPath}/2KeyPress",
            };

            foreach (string path in pathsToTry)
            {
                frames.Clear();

                // 加载帧序列
                for (int i = 0;; i++)
                {
                    Texture? texture = textureStore.Get($"{path}_{i}.png");

                    if (texture == null) break;

                    frames.Add(texture);
                }

                // 如果没有帧，加载单个纹理作为单帧
                if (frames.Count == 0)
                {
                    Texture? singleTexture = textureStore.Get($"{path}.png");

                    if (singleTexture == null) continue;

                    frames.Add(singleTexture);
                }

                // 设置循环模式：多帧循环，单帧不循环
                bool isLoop = frames.Count > 1;

                Logger.Log($"[EzLocalTextureFactory] Loaded stage key frames with {frames.Count} frames for {path} (Loop: {isLoop})",
                    LoggingTarget.Runtime, LogLevel.Debug);

                // 如果找到了帧，返回
                if (frames.Count > 0) return frames;
            }

            return frames; // 返回空列表如果没有找到
        }

        #endregion

        #region 缓存管理

        private void clearRelatedCache(string? oldNoteSet)
        {
            if (string.IsNullOrEmpty(oldNoteSet)) return;

            clearCache(global_cache, k => k.StartsWith($"{oldNoteSet}_", StringComparison.Ordinal), nameof(global_cache));
            note_ratio_cache.TryRemove(oldNoteSet, out _);

            resetPreloadState();
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
                foreach (var loader in loaderStoreCache.Values)
                    loader.Dispose();
                loaderStoreCache.Clear();

                // Clear instance caches
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
