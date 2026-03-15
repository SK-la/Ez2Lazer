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
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.LAsEzExtensions
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

        private readonly Bindable<string> noteSetName;
        private readonly Bindable<string> stageName;
        private readonly Bindable<double> columnWidth;
        private readonly Bindable<double> specialFactor;
        private readonly Bindable<double> noteHeightScaleToWidth;
        private readonly IBindable<bool> colorSettingsEnabled;
        private readonly Bindable<Colour4> columnTypeA;
        private readonly Bindable<Colour4> columnTypeB;
        private readonly Bindable<Colour4> columnTypeS;
        private readonly Bindable<Colour4> columnTypeE;
        private readonly Bindable<Colour4> columnTypeP;

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

            columnWidth = ezConfig.GetBindable<double>(Ez2Setting.ColumnWidth);
            specialFactor = ezConfig.GetBindable<double>(Ez2Setting.SpecialFactor);
            noteHeightScaleToWidth = ezConfig.GetBindable<double>(Ez2Setting.NoteHeightScaleToWidth);

            noteSetName = ezConfig.GetBindable<string>(Ez2Setting.NoteSetName);
            stageName = ezConfig.GetBindable<string>(Ez2Setting.StageName);

            // 绑定颜色设置，用于通知颜色变化
            colorSettingsEnabled = ezConfig.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);
            columnTypeA = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeA);
            columnTypeB = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeB);
            columnTypeS = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeS);
            columnTypeE = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeE);
            columnTypeP = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeP);

            initializeDrawableEvents();
            initializeSizeEvents();
            initializeColourEvents();
        }

        #region 事件发布

        private void scheduleTextureRefresh()
        {
            // 使用 Scheduler.AddOnce 而不是 Schedule，确保即使时钟暂停也能执行
            Scheduler.AddOnce(() =>
            {
                // 强制重新计算比例并刷新尺寸
                GetRatio(forceRecalculate: true);
                OnNoteSizeChanged?.Invoke();
            });
        }

        public event Action? OnNoteSizeChanged;
        public event Action? OnNoteColourChanged;

        private void initializeDrawableEvents()
        {
            noteSetName.BindValueChanged(_ => scheduleTextureRefresh(), true);
            stageName.BindValueChanged(_ => scheduleTextureRefresh(), true);
        }

        private void initializeSizeEvents()
        {
            columnWidth.BindValueChanged(_ => scheduleTextureRefresh(), true);
            specialFactor.BindValueChanged(_ => scheduleTextureRefresh(), true);
            noteHeightScaleToWidth.BindValueChanged(_ => scheduleTextureRefresh(), true);

            columnWidth.BindValueChanged(_ => OnNoteSizeChanged?.Invoke());
            specialFactor.BindValueChanged(_ => OnNoteSizeChanged?.Invoke());
            noteHeightScaleToWidth.BindValueChanged(_ => OnNoteSizeChanged?.Invoke());

            var columnWidthStyleBindable = ezConfig.GetBindable<ColumnWidthStyle>(Ez2Setting.ColumnWidthStyle);
            var holdTailMaskHeightBindable = ezConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailMaskGradientHeight);

            columnWidthStyleBindable.BindValueChanged(_ => OnNoteSizeChanged?.Invoke());
            holdTailMaskHeightBindable.BindValueChanged(_ => OnNoteSizeChanged?.Invoke());
        }

        private void initializeColourEvents()
        {
            var holdTailAlphaBindable = ezConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailAlpha);
            var customColour = ezConfig.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);
            var colorABindable = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeA);
            var colorBBindable = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeB);
            var colorSBindable = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeS);
            var colorEBindable = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeE);
            var colorPBindable = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeP);

            holdTailAlphaBindable.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            customColour.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            colorABindable.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            colorBBindable.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            colorSBindable.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            colorEBindable.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            colorPBindable.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
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

        public Bindable<Vector2> GetNoteSize(int keyMode, int columnIndex, bool? noSpecial = null)
        {
            // 当 Note/Column销毁时，Bindable会被 GC自动回收，避免内存泄漏
            var cacheKey = new NoteSizeCacheKey(keyMode, columnIndex, noSpecial == true);
            return new Bindable<Vector2>(calculateNoteSize(cacheKey));
        }

        private Vector2 calculateNoteSize(NoteSizeCacheKey cacheKey)
        {
            bool isSpecialColumn = !cacheKey.NoSpecial && ezConfig.IsSpecialColumnFast(cacheKey.KeyMode, cacheKey.ColumnIndex);
            float ratio = GetRatio();
            float x = (float)(columnWidth.Value * (isSpecialColumn ? specialFactor.Value : 1.0));
            float y = (float)noteHeightScaleToWidth.Value * ratio * x;
            return new Vector2(x, y);
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
