// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;
using osuTK;

namespace osu.Game.EzOsuGame
{
    // DI注入的全局纹理工厂，主要为EzPro皮肤提供配套纹理资源。
    public partial class EzLocalTextureFactory : CompositeDrawable
    {
        // private const int max_stage_frames = 120;
        /// <summary>note 帧上限，与旧实现的 <c>Parallel.For(0, 60)</c> 一致。</summary>
        private const int max_note_frames = 60;

        private const double default_frame_length = 1000.0 / 60.0 * 4;
        private const float square_ratio_threshold = 0.75f;

        private static readonly ConcurrentDictionary<string, float> note_ratio_cache = new ConcurrentDictionary<string, float>();

        private readonly Ez2ConfigManager ezConfig;
        private readonly EzResourceStore resource;

        private readonly BindableDouble columnWidth = new BindableDouble();
        private readonly BindableDouble specialFactor = new BindableDouble();
        private readonly BindableDouble noteHeightScaleToWidth = new BindableDouble();

        private readonly Bindable<string> stageName = new Bindable<string>();
        private readonly Bindable<string> noteSetName = new Bindable<string>();
        private readonly Bindable<EzEnumGameThemeName> gameThemeName = new Bindable<EzEnumGameThemeName>();

        private readonly BindableBool colorSettingsEnabled = new BindableBool(true);
        private readonly Bindable<Colour4> columnTypeA = new Bindable<Colour4>();
        private readonly Bindable<Colour4> columnTypeB = new Bindable<Colour4>();
        private readonly Bindable<Colour4> columnTypeS = new Bindable<Colour4>();
        private readonly Bindable<Colour4> columnTypeE = new Bindable<Colour4>();
        private readonly Bindable<Colour4> columnTypeP = new Bindable<Colour4>();

        private readonly IBindable<string>[] columnTypeLists;
        private readonly Dictionary<NoteSizeCacheKey, Bindable<Vector2>> noteSizeBindables = new Dictionary<NoteSizeCacheKey, Bindable<Vector2>>();

        private readonly Action<int, int, EzColumnType>? onColumnTypeChangedHandler;

        private string? plateLayoutStage;
        private EzStagePlateLayout? plateLayout;

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

        public EzLocalTextureFactory(Ez2ConfigManager ezConfig, EzResourceStore resource)
        {
            this.ezConfig = ezConfig;
            this.resource = resource;

            ezConfig.BindWith(Ez2Setting.NoteSetName, noteSetName);
            ezConfig.BindWith(Ez2Setting.StageName, stageName);
            gameThemeName.BindTo(ezConfig.GetBindable<EzEnumGameThemeName>(Ez2Setting.GameThemeName));

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
            Texture? texture = resource.Get($@"{path}/000", EzTextureUsage.AnimationSafe)
                               ?? resource.Get($@"{path}/0", EzTextureUsage.AnimationSafe);

            return texture;
        }

        public float GetRatio(bool forceRecalculate = false)
        {
            string noteSet = noteSetName.Value;

            // 如果强制重新计算或缓存中没有，则重新计算
            if (forceRecalculate || !note_ratio_cache.TryGetValue(noteSet, out float ratio))
            {
                string notePath = GetNotePath("whitenote");
                Texture? note = GetNoteTexture(notePath);
                float calculatedRatio = 1;

                if (note != null)
                {
                    float noteHeight = note.Height;
                    float noteWidth = note.Width;
                    calculatedRatio = noteHeight / noteWidth;

                    // Logger.Log($"{noteSet} ratio: {calculatedRatio}");
                }

                ratio = calculatedRatio;

                // if (noteHeightScaleToWidth.IsDefault)
                //     ratio = calculatedRatio;
                // else
                //     ratio = calculatedRatio >= square_ratio_threshold ? 1.0f : calculatedRatio;

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
        /// 构造Note、光效等动画组件。遵循 ISkin 的 <c>GetAnimation</c> 约定：0 帧返回 <c>null</c>，1 帧返回 <see cref="Sprite"/>，多帧才返回动画。
        /// </summary>
        /// <param name="component"></param>
        /// <param name="isFlare">是否为光效</param>
        /// <returns>纹理 drawable；无资源时为 <c>null</c>。</returns>
        public Drawable? CreateAnimation(string component, bool? isFlare = null)
        {
            bool isHit = isFlare is true;

            if (component == "JudgementLine")
                FillMode = FillMode.Fill;

            // 直接加载纹理帧，不缓存
            var frames = loadNotesFrames(component);

            if (frames.Count == 0)
                return null;

            Anchor anchor = isHit ? Anchor.BottomCentre : Anchor.Centre;
            Axes relativeSizeAxes = isHit ? Axes.None : Axes.Both;
            FillMode fillMode = isHit ? FillMode.Fit : FillMode.Stretch;

            // 1 帧即普通纹理，不按动画加载。
            if (frames.Count == 1)
            {
                return new Sprite
                {
                    Anchor = anchor,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = relativeSizeAxes,
                    FillMode = fillMode,
                    Texture = frames[0],
                };
            }

            var animation = new TextureAnimation
            {
                Anchor = anchor,
                Origin = Anchor.Centre,
                RelativeSizeAxes = relativeSizeAxes,
                FillMode = fillMode,
                Loop = !isHit
            };

            if (!isHit)
            {
                animation.DefaultFrameLength = default_frame_length;
                // animation.Blending = BlendingParameters.Inherit;
            }

            animation.AddFrames(frames);

            return animation;
        }

        // note 帧的**命名**交由层2 三模板统一解析（子目录 {i:D3}、同层 {name}-0、{name}/frame_0 都在覆盖范围内），
        // JudgementLine 这类单图由单图回退自然覆盖。比例由 store 构造期的 scaleAdjust 决定，故这里不再改写共享纹理。
        // 帧的**并行预解码**保留在本工厂：这是既有并已验证的做法，且并行只作用于这一处。
        private List<Texture> loadNotesFrames(string component)
        {
            var request = new EzAnimationRequest
            {
                Path = GetNotePath(component),
                MaxFrames = max_note_frames,
                Usage = EzTextureUsage.AnimationSafe,
            };

            IReadOnlyList<string> keys = resource.ResolveFrameKeys(request);

            if (keys.Count > 1)
            {
                // 帧互相独立、TextureStore 自身带锁，批量帧并行解码，避免逐帧串行拖慢进图。
                Parallel.For(0, keys.Count, i => resource.Get(keys[i], EzTextureUsage.AnimationSafe));
            }

            return new List<Texture>(resource.GetTextureFrames(request));
        }

        #endregion

        #region Stage Creation

        public Container CreateStage(int columnCount)
        {
            var container = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };

            EzStagePlateLayout? layout = getPlateLayout();

            if (layout == null)
                return container;

            string basePath = $"Stage/{stageName.Value}/Stage";
            string? keyFolder = ResolveStageKeyFolder(resource.ListSubdirectories(basePath), columnCount);

            addPlateSprite(container, layout.Back, $"{basePath}/BlackPanel");

            if (keyFolder != null)
                addPlateSprite(container, layout.Body, $"{basePath}/{keyFolder}/Body");

            addPlateSprite(container, layout.GrooveLight, $"{basePath}/GrooveLight");

            if (layout.Meter != null)
            {
                Drawable? gauge = createPlateDrawable($"{basePath}/GrooveGauge", layout.Meter.Gauge);
                Drawable? bright = createPlateDrawable($"{basePath}/GrooveGaugeLight", layout.Meter.Bright);

                if (gauge != null || bright != null)
                    container.Add(new EzStageGrooveMeter(layout.Meter, gauge, bright));
            }

            addPlateSprite(container, layout.OverObject, $"{basePath}/{stageName.Value}_OverObject/{stageName.Value}_OverObject");

            if (layout.Character != null)
            {
                bool placed = keyFolder != null && addPlateSprite(container, layout.Character, $"{basePath}/{keyFolder}/Character");

                if (!placed)
                    addPlateSprite(container, layout.Character, $"{basePath}/Character_overlayer");
            }

            return container;
        }

        private EzStagePlateLayout? getPlateLayout()
        {
            if (plateLayoutStage == stageName.Value)
                return plateLayout;

            plateLayoutStage = stageName.Value;
            plateLayout = EzStagePlateLayout.TryLoad(resource, stageName.Value);
            return plateLayout;
        }

        internal const int MAX_STAGE_KEYS = 18;

        private const int stage_key_downward_floor = 4;

        internal static string? ResolveStageKeyFolder(IEnumerable<string> folderNames, int columnCount)
        {
            if (columnCount <= 0)
                return null;

            string?[] best = new string?[MAX_STAGE_KEYS + 1];

            foreach (string name in folderNames)
            {
                if (!tryParseKeyFolder(name, out int keys))
                    continue;

                string? current = best[keys];

                if (current == null || keyFolderPreference(name, keys) > keyFolderPreference(current, keys))
                    best[keys] = name;
            }

            if (columnCount > MAX_STAGE_KEYS)
                columnCount = MAX_STAGE_KEYS;

            if (columnCount >= stage_key_downward_floor)
            {
                for (int keys = columnCount; keys >= stage_key_downward_floor; keys--)
                {
                    if (best[keys] != null)
                        return best[keys];
                }
            }
            else if (best[columnCount] != null)
                return best[columnCount];

            for (int keys = columnCount + 1; keys <= MAX_STAGE_KEYS; keys++)
            {
                if (best[keys] != null)
                    return best[keys];
            }

            return null;
        }

        private static int keyFolderPreference(string name, int keys)
        {
            if (name.Equals($"_{keys}key", StringComparison.OrdinalIgnoreCase))
                return 2;

            if (name.Equals($"{keys}key", StringComparison.OrdinalIgnoreCase))
                return 1;

            return 0;
        }

        private static bool tryParseKeyFolder(string folder, out int keys)
        {
            keys = 0;

            if (folder.Length <= 3 || !folder.EndsWith("key", StringComparison.OrdinalIgnoreCase))
                return false;

            string head = folder[..^3].TrimStart('_');

            if (head.Length == 0)
                return false;

            if (int.TryParse(head, out keys))
                return keys is >= 1 and <= MAX_STAGE_KEYS;

            return false;
        }

        private bool addPlateSprite(Container parent, EzStagePlateSprite? piece, string texturePath)
        {
            if (piece == null)
                return false;

            Drawable? drawable = createPlateDrawable(texturePath, piece);

            if (drawable == null)
                return false;

            parent.Add(drawable);
            return true;
        }

        private Drawable? createPlateDrawable(string texturePath, EzStagePlateSprite? piece)
        {
            if (piece == null)
                return null;

            var frames = loadStageComponentFrames(texturePath);

            if (frames.Count == 0)
                return null;

            BlendingParameters blending = piece.IsAdditive ? BlendingParameters.Additive : BlendingParameters.Inherit;

            if (frames.Count == 1)
            {
                var sprite = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Position = piece.ToOsuPosition(),
                    Texture = frames[0],
                    Blending = blending,
                };

                if (isSingleFrameGlow(texturePath))
                    return new EzStageSingleLightBreath(sprite);

                return sprite;
            }

            var animation = new TextureAnimation
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Position = piece.ToOsuPosition(),
                Loop = piece.Loop,
                DefaultFrameLength = piece.FrameLength,
                Blending = blending,
            };

            animation.AddFrames(frames);
            return animation;
        }

        private static bool isSingleFrameGlow(string texturePath)
            => texturePath.EndsWith("/GrooveLight", StringComparison.OrdinalIgnoreCase)
               || texturePath.EndsWith("/GrooveGaugeLight", StringComparison.OrdinalIgnoreCase);

        // Stage 组件帧：交给层2/层3 同一规则（多帧 AnimationSafe，空则回退 Large 单图）。
        private List<Texture> loadStageComponentFrames(string basePath) => resource.LoadStageFrames(basePath);

        /// <summary>
        /// 构造 Key 底/压图。同样遵循 0 帧 <c>null</c>、1 帧 <see cref="Sprite"/>、多帧动画。
        /// </summary>
        public Drawable? CreateStageKeys(string component, string? keySuffix = null)
        {
            // 直接加载纹理帧，不缓存
            var frames = loadStageKeysFrames(component, keySuffix);

            if (frames.Count == 0)
                return null;

            // 1 帧即普通纹理，不按动画加载。
            if (frames.Count == 1)
            {
                return new Sprite
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.None,
                    FillMode = FillMode.Stretch,
                    Texture = frames[0],
                };
            }

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

        // keybase/keypress 四条路径的尝试与「累计为空才吃单图」属皮肤编排，留在工厂；单条路径的帧解析交给层2。
        private List<Texture> loadStageKeysFrames(string component, string? keySuffix = null)
        {
            var frames = new List<Texture>();
            string currentStageName = stageName.Value;

            string[] pathsToTry =
            {
                $"Stage/{currentStageName}/Stage/_8key/keybase/{component}",
                $"Stage/{currentStageName}/Stage/_8key/keypress/{component}",
                $"Stage/{currentStageName}/Stage/_8key/keybase/{component}_{keySuffix}",
                $"Stage/{currentStageName}/Stage/_8key/keypress/{component}_{keySuffix}",
            };

            foreach (string basePath in pathsToTry)
            {
                // 这里刻意不让层2 回退单图：单图只在「累计仍为空」时补一张，与原行为一致。
                frames.AddRange(resource.GetTextureFrames(new EzAnimationRequest
                {
                    Path = basePath,
                    Usage = EzTextureUsage.AnimationSafe,
                    AllowSingleFallback = false,
                }));

                if (frames.Count == 0)
                {
                    Texture? texture = resource.Get(basePath, EzTextureUsage.AnimationSafe);

                    if (texture != null)
                        frames.Add(texture);
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
