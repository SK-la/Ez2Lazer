// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame
{
    public partial class EzLocalTextureFactory : CompositeDrawable
    {
        // private const int max_stage_frames = 120;
        private const int max_frames_to_load = 240;
        private const double default_frame_length = 1000.0 / 60.0 * 4;
        private const float default_stage_body_height = 247f; // 不要删，Ez-Stage 的默认判定线高度
        private const float square_ratio_threshold = 0.75f;

        private readonly Dictionary<string, TextureLoaderStore> loaderStoreCache = new Dictionary<string, TextureLoaderStore>();
        private static readonly ConcurrentDictionary<string, float> note_ratio_cache = new ConcurrentDictionary<string, float>();

        private readonly Ez2ConfigManager ezConfig;
        private readonly LargeTextureStore stageTextureStore;
        private readonly TextureStore textureStore;

        private readonly IBindable<string> noteSetName;
        private readonly IBindable<string> stageName;

        private readonly BindableDouble columnWidth = new BindableDouble();
        private readonly BindableDouble specialFactor = new BindableDouble();
        private readonly BindableDouble noteHeightScaleToWidth = new BindableDouble();

        private readonly IBindable<bool> colorSettingsEnabled;
        private readonly IBindable<Colour4> columnTypeA;
        private readonly IBindable<Colour4> columnTypeB;
        private readonly IBindable<Colour4> columnTypeS;
        private readonly IBindable<Colour4> columnTypeE;
        private readonly IBindable<Colour4> columnTypeP;
        private readonly IBindable<string>[] columnTypeLists;
        private readonly Dictionary<NoteSizeCacheKey, Bindable<Vector2>> noteSizeBindables = new Dictionary<NoteSizeCacheKey, Bindable<Vector2>>();

        private readonly Action<int, int, EzColumnType>? onColumnTypeChangedHandler;

        public IBindable<string> NoteSetNameBindable { get; }
        public IBindable<bool> ColorSettingsEnabledBindable { get; }

        private readonly struct NoteSizeCacheKey : IEquatable<NoteSizeCacheKey>
        {
            public readonly int KeyMode;
            public readonly int ColumnIndex;
            public readonly bool NoSpecial;

            public NoteSizeCacheKey(int keyMode, int columnIndex, bool noSpecial)
            {
                KeyMode = keyMode;
                ColumnIndex = columnIndex;
                NoSpecial = noSpecial;
            }

            public bool Equals(NoteSizeCacheKey other) => KeyMode == other.KeyMode && ColumnIndex == other.ColumnIndex && NoSpecial == other.NoSpecial;

            public override bool Equals(object? obj) => obj is NoteSizeCacheKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(KeyMode, ColumnIndex, NoSpecial);
        }

        public EzLocalTextureFactory(Ez2ConfigManager ezConfig,
                                     IRenderer renderer,
                                     Storage hostStorage)
        {
            this.ezConfig = ezConfig;

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

            noteSetName = ezConfig.GetBindable<string>(Ez2Setting.NoteSetName);
            stageName = ezConfig.GetBindable<string>(Ez2Setting.StageName);
            NoteSetNameBindable = noteSetName;

            ezConfig.BindWith(Ez2Setting.ColumnWidth, columnWidth);
            ezConfig.BindWith(Ez2Setting.SpecialFactor, specialFactor);
            ezConfig.BindWith(Ez2Setting.NoteHeightScaleToWidth, noteHeightScaleToWidth);
            // columnWidth = ezConfig.GetBindable<double>(Ez2Setting.ColumnWidth);
            // specialFactor = ezConfig.GetBindable<double>(Ez2Setting.SpecialFactor);
            // noteHeightScaleToWidth = ezConfig.GetBindable<double>(Ez2Setting.NoteHeightScaleToWidth);

            // 绑定颜色设置，用于通知颜色变化
            colorSettingsEnabled = ezConfig.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);
            ColorSettingsEnabledBindable = colorSettingsEnabled;
            columnTypeA = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeA);
            columnTypeB = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeB);
            columnTypeS = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeS);
            columnTypeE = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeE);
            columnTypeP = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeP);
            columnTypeLists = new IBindable<string>[]
            {
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf4K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf5K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf6K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf7K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf8K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf9K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf10K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf12K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf14K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf16K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf18K),
            };

            initializeDrawableEvents();
            initializeSizeEvents();
            initializeColourEvents();

            onColumnTypeChangedHandler = onColumnTypeChanged;
            ezConfig.ColumnTypeChanged += onColumnTypeChangedHandler;
        }

        #region 事件发布

        public event Action? OnNoteSizeChanged;
        public event Action? OnNoteColourChanged;

        private void scheduleTextureRefresh()
        {
            // 纹理名或轨道尺寸相关设置变化时，立即重算尺寸，避免 note 先用旧尺寸渲染一帧。
            GetRatio(forceRecalculate: true);
            refreshNoteSizeBindables();
            OnNoteSizeChanged?.Invoke();
        }

        private void onColumnTypeChanged(int keyMode, int columnIndex, EzColumnType type)
        {
            foreach (var pair in noteSizeBindables)
            {
                if (pair.Key.KeyMode == keyMode && pair.Key.ColumnIndex == columnIndex)
                    updateNoteSizeBindable(pair.Key, pair.Value);
            }
        }

        private void initializeDrawableEvents()
        {
            noteSetName.BindValueChanged(_ => scheduleTextureRefresh());
            stageName.BindValueChanged(_ => scheduleTextureRefresh());
        }

        private void initializeSizeEvents()
        {
            columnWidth.BindValueChanged(_ => scheduleTextureRefresh());
            specialFactor.BindValueChanged(_ => scheduleTextureRefresh());
            noteHeightScaleToWidth.BindValueChanged(_ => scheduleTextureRefresh(), true);
        }

        private void initializeColourEvents()
        {
            colorSettingsEnabled.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            columnTypeA.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            columnTypeB.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            columnTypeS.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            columnTypeE.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            columnTypeP.BindValueChanged(_ => OnNoteColourChanged?.Invoke());

            foreach (var columnTypeList in columnTypeLists)
                columnTypeList.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 获取单个纹理。
        /// </summary>
        public Texture? GetTexture(string component)
        {
            string currentNoteSetName = noteSetName.Value;
            string path = getComponentPath(currentNoteSetName, component);

            // 直接从 TextureStore 获取，不缓存
            return textureStore.Get(path);
        }

        public float GetRatio(bool forceRecalculate = false)
        {
            string noteSet = noteSetName.Value;

            // 如果强制重新计算或缓存中没有，则重新计算
            if (forceRecalculate || !note_ratio_cache.TryGetValue(noteSet, out float ratio))
            {
                string path = getComponentPath(noteSet, "whitenote");
                float calculatedRatio = calculateRatio(path);
                ratio = calculatedRatio >= square_ratio_threshold ? 1.0f : calculatedRatio;

                // 更新缓存
                note_ratio_cache.AddOrUpdate(noteSet, ratio, (_, _) => ratio);
            }

            return ratio;
        }

        public Bindable<Vector2> GetNoteSizeBindable(int keyMode, int columnIndex, bool noSpecial = false)
        {
            var cacheKey = new NoteSizeCacheKey(keyMode, columnIndex, noSpecial);

            if (!noteSizeBindables.TryGetValue(cacheKey, out var bindable))
            {
                bindable = new Bindable<Vector2>(calculateNoteSize(cacheKey));
                noteSizeBindables[cacheKey] = bindable;
            }

            return bindable;
        }

        private Vector2 calculateNoteSize(NoteSizeCacheKey cacheKey)
        {
            bool isSpecialColumn = !cacheKey.NoSpecial && ezConfig.IsSpecialColumnFast(cacheKey.KeyMode, cacheKey.ColumnIndex);
            float ratio = GetRatio();
            float x = (float)(columnWidth.Value * (isSpecialColumn ? specialFactor.Value : 1.0));
            float y = (float)noteHeightScaleToWidth.Value * ratio * x;
            return new Vector2(x, y);
        }

        private void refreshNoteSizeBindables()
        {
            foreach (var pair in noteSizeBindables)
                updateNoteSizeBindable(pair.Key, pair.Value);
        }

        private void updateNoteSizeBindable(NoteSizeCacheKey cacheKey)
        {
            if (noteSizeBindables.TryGetValue(cacheKey, out var bindable))
                updateNoteSizeBindable(cacheKey, bindable);
        }

        private void updateNoteSizeBindable(NoteSizeCacheKey cacheKey, Bindable<Vector2> bindable)
        {
            Vector2 newSize = calculateNoteSize(cacheKey);

            if (bindable.Value != newSize)
                bindable.Value = newSize;
        }

        private string getComponentPath(string noteName, string component)
        {
            return $"note/{noteName}/{component}";
        }

        private float calculateRatio(string path)
        {
            try
            {
                // 直接加载纹理计算比例，不使用缓存
                var sb = new StringBuilder(path.Length + 8);
                Texture? texture = textureStore.Get(sb.Append(path).Append("/000.png").ToString()) ??
                                   textureStore.Get(sb.Clear().Append(path).Append("/001.png").ToString());

                return texture?.Height / (texture?.Width ?? 1f) ?? 1.0f;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error calculating ratio for {path}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
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
        public TextureAnimation CreateAnimation(string component, bool? isFlare = null)
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

            // 直接加载纹理帧，不缓存
            var frames = loadNotesFrames(component, noteSetName.Value);
            animation.AddFrames(frames);

            return animation;
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

                    texture.ScaleAdjust = 2f;

                    frames.Add(texture);
                }
            }
            else
            {
                string frameFile = $"{basePath}.png";
                Logger.Log($"[EzLocalTextureFactory] Loading JudgementLine Frame: {frameFile}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                var texture = textureStore.Get(frameFile);

                frames.Add(texture);
            }

            return new List<Texture>(frames);
        }

        #endregion

        #region Stage Creation

        public Container CreateStage(string component)
        {
            var container = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,

                // AutoSizeAxes = Axes.X,
                // Height = default_stage_body_height,
                // Masking = true,
            };

            string basePath = $"Stage/{stageName.Value}/Stage";

            container.Add(getStageTextureAnimation($"{basePath}/eightkey/{component}"));
            container.Add(getStageTextureAnimation($"{basePath}/GrooveLight")); //此纹理需要修改正片叠底
            container.Add(getStageTextureAnimation($"{basePath}/{stageName.Value}_OverObject/{stageName.Value}_OverObject"));

            return container;
        }

        private TextureAnimation getStageTextureAnimation(string basePath)
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

            return animation;
        }

        private List<Texture> loadStageComponentFrames(string basePath)
        {
            var frames = new List<Texture>();

            for (int i = 0;; i++)
            {
                Texture? texture = textureStore.Get($"{basePath}_{i}.png");

                if (texture == null) break;

                Logger.Log($"[EzLocalTextureFactory] Added Stage Frames: {basePath}_{i}.png", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

                frames.Add(texture);
            }

            if (frames.Count == 0)
            {
                Texture? texture = stageTextureStore.Get($"{basePath}.png");

                if (texture != null)
                {
                    Logger.Log($"[EzLocalTextureFactory] Added Stage Frame: {basePath}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                    frames.Add(texture);
                }
            }

            return frames;
        }

        public TextureAnimation CreateStageKeys(string component, string? keySuffix = null)
        {
            // 直接加载纹理帧，不缓存
            var frames = loadStageKeysFrames(component, keySuffix);
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

                    Logger.Log($"[EzLocalTextureFactory] Added Keys Frames: {basePath}_{i}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

                    frames.Add(texture);
                }

                // 如果没有帧，加载单个纹理作为单帧
                if (frames.Count == 0)
                {
                    Texture? texture = textureStore.Get($"{basePath}.png");

                    if (texture != null)
                    {
                        Logger.Log($"[EzLocalTextureFactory] Added Keys Frame: {basePath}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                        frames.Add(texture);
                    }
                }
            }

            return frames;
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (onColumnTypeChangedHandler != null)
                    ezConfig.ColumnTypeChanged -= onColumnTypeChangedHandler;

                noteSetName.UnbindAll();
                stageName.UnbindAll();
                columnWidth.UnbindAll();
                specialFactor.UnbindAll();
                noteHeightScaleToWidth.UnbindAll();
                colorSettingsEnabled.UnbindAll();
                columnTypeA.UnbindAll();
                columnTypeB.UnbindAll();
                columnTypeS.UnbindAll();
                columnTypeE.UnbindAll();
                columnTypeP.UnbindAll();

                // 清理纹理加载器
                lock (loaderStoreCache)
                {
                    foreach (var loader in loaderStoreCache.Values)
                        loader.Dispose();
                }

                lock (loaderStoreCache)
                {
                    loaderStoreCache.Clear();
                }
            }

            base.Dispose(isDisposing);
        }
    }
}
// public enum EzAnimationType
// {
//     Note,
//     Hit,
//     Stage,
//     Key,
//     Health,
// }
