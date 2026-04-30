// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
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
    // DI注入的全局纹理工厂，主要为EzPro皮肤提供配套纹理资源。
    public partial class EzLocalTextureFactory : CompositeDrawable
    {
        // private const int max_stage_frames = 120;
        private const int max_frames_to_load = 240;
        private const double default_frame_length = 1000.0 / 60.0 * 4;
        private const float square_ratio_threshold = 0.75f;

        private static readonly ConcurrentDictionary<string, float> note_ratio_cache = new ConcurrentDictionary<string, float>();

        private readonly Ez2ConfigManager ezConfig;
        private readonly LargeTextureStore largeTextureStore;
        private readonly TextureStore textureStore;

        private readonly BindableDouble columnWidth = new BindableDouble();
        private readonly BindableDouble specialFactor = new BindableDouble();
        private readonly BindableDouble noteHeightScaleToWidth = new BindableDouble();

        private readonly Bindable<string> stageName = new Bindable<string>();
        private readonly Bindable<string> noteSetName = new Bindable<string>();

        private readonly BindableBool colorSettingsEnabled = new BindableBool(true);
        private readonly Bindable<Colour4> columnTypeA = new Bindable<Colour4>();
        private readonly Bindable<Colour4> columnTypeB = new Bindable<Colour4>();
        private readonly Bindable<Colour4> columnTypeS = new Bindable<Colour4>();
        private readonly Bindable<Colour4> columnTypeE = new Bindable<Colour4>();
        private readonly Bindable<Colour4> columnTypeP = new Bindable<Colour4>();

        private readonly IBindable<string>[] columnTypeLists;
        private readonly Dictionary<NoteSizeCacheKey, Bindable<Vector2>> noteSizeBindables = new Dictionary<NoteSizeCacheKey, Bindable<Vector2>>();

        private readonly Action<int, int, EzColumnType>? onColumnTypeChangedHandler;

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

        public EzLocalTextureFactory(Ez2ConfigManager ezConfig, IRenderer renderer, Storage storage)
        {
            this.ezConfig = ezConfig;

            IResourceStore<byte[]> userFiles = new StorageBackedResourceStore(storage.GetStorageForDirectory(EzModifyPath.RESOURCES_PATH));
            var baseTextureLoaderStore = new TextureLoaderStore(userFiles);

            // 创建尺寸限制的纹理加载器（使用官方的 MaxDimensionLimitedTextureLoaderStore）
            var limitedLoader = new MaxDimensionLimitedTextureLoaderStore(baseTextureLoaderStore);
            textureStore = new TextureStore(renderer, limitedLoader);
            textureStore.AddTextureSource(baseTextureLoaderStore);

            largeTextureStore = new LargeTextureStore(renderer, limitedLoader);
            largeTextureStore.AddTextureSource(baseTextureLoaderStore);

            ezConfig.BindWith(Ez2Setting.NoteSetName, noteSetName);
            ezConfig.BindWith(Ez2Setting.StageName, stageName);

            ezConfig.BindWith(Ez2Setting.ColumnWidth, columnWidth);
            ezConfig.BindWith(Ez2Setting.SpecialFactor, specialFactor);
            ezConfig.BindWith(Ez2Setting.NoteHeightScaleToWidth, noteHeightScaleToWidth);

            ezConfig.BindWith(Ez2Setting.ColorSettingsEnabled, colorSettingsEnabled);
            ezConfig.BindWith(Ez2Setting.ColumnTypeA, columnTypeA);
            ezConfig.BindWith(Ez2Setting.ColumnTypeB, columnTypeB);
            ezConfig.BindWith(Ez2Setting.ColumnTypeS, columnTypeS);
            ezConfig.BindWith(Ez2Setting.ColumnTypeE, columnTypeE);
            ezConfig.BindWith(Ez2Setting.ColumnTypeP, columnTypeP);

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

        public event Action? OnNoteDrawableChanged;
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
            noteSetName.BindValueChanged(_ =>
            {
                scheduleTextureRefresh();
                OnNoteDrawableChanged?.Invoke();
            });
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

        public string GetNotePath(string name)
        {
            return $"note/{noteSetName.Value}/{name}";
        }

        /// <summary>
        /// 获取单个纹理。
        /// </summary>
        public Texture? GetNoteTexture(string path)
        {
            // 直接加载纹理计算比例，不使用缓存
            var sb = new StringBuilder(path.Length + 8);
            Texture? texture = textureStore.Get(sb.Append(path).Append("/000.png").ToString()) ??
                               textureStore.Get(sb.Clear().Append(path).Append("/001.png").ToString());
            return texture;
        }

        private float getTextureRatio(Texture? texture)
        {
            return texture?.Height / (texture?.Width ?? 1f) ?? 1.0f;
        }

        public float GetRatio(bool forceRecalculate = false)
        {
            string noteSet = noteSetName.Value;

            // 如果强制重新计算或缓存中没有，则重新计算
            if (forceRecalculate || !note_ratio_cache.TryGetValue(noteSet, out float ratio))
            {
                string notePath = GetNotePath("whitenote");
                Texture? note = GetNoteTexture(notePath);
                float calculatedRatio = getTextureRatio(note);
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
            var frames = loadNotesFrames(component);
            animation.AddFrames(frames);

            return animation;
        }

        private List<Texture> loadNotesFrames(string component)
        {
            string notePath = GetNotePath(component);
            var frames = new List<Texture>();

            if (component != "JudgementLine")
            {
                var textures = new Texture[60];

                Parallel.For(0, 60, i =>
                {
                    string frameFile = $"{notePath}/{i:D3}";
                    textures[i] = textureStore.Get(frameFile);
                });

                // 按顺序收集非空纹理
                foreach (var texture in textures)
                {
                    if (texture == null) break;

                    texture.ScaleAdjust = 2f;
                    frames.Add(texture);
                }
            }
            else
            {
                string frameFile = notePath;
                var texture = textureStore.Get(frameFile);

                if (texture != null)
                {
                    Logger.Log($"[EzLocalTextureFactory] Loading JudgementLine Frame: {frameFile}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

                    frames.Add(texture);
                }
            }

            return frames;
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
                Texture? texture = textureStore.Get($"{basePath}_{i}");

                if (texture == null) break;

                Logger.Log($"[EzLocalTextureFactory] Added Stage Frames: {basePath}_{i}.png", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

                frames.Add(texture);
            }

            if (frames.Count == 0)
            {
                Texture? texture = largeTextureStore.Get($"{basePath}");

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
                    Texture? texture = textureStore.Get($"{basePath}_frame{i}");

                    if (texture == null) break;

                    Logger.Log($"[EzLocalTextureFactory] Added Keys Frames: {basePath}_{i}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

                    frames.Add(texture);
                }

                // 如果没有帧，加载单个纹理作为单帧
                if (frames.Count == 0)
                {
                    Texture? texture = textureStore.Get($"{basePath}");

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
