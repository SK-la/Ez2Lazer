// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Overlays;
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
        private readonly Bindable<ColumnWidthStyle> columnWidthStyle = new Bindable<ColumnWidthStyle>();

        private readonly Bindable<string> stageName = new Bindable<string>();
        private readonly Bindable<string> noteSetName = new Bindable<string>();
        private readonly Bindable<string> gameThemeName = new Bindable<string>();
        private readonly Bindable<string> judgementLineName = new Bindable<string>();

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
            ezConfig.BindWith(Ez2Setting.JudgementLineName, judgementLineName);
            gameThemeName.BindTo(ezConfig.GetBindable<string>(Ez2Setting.GameThemeName));

            ezConfig.BindWith(Ez2Setting.ColumnWidth, columnWidth);
            ezConfig.BindWith(Ez2Setting.ColumnWidthStyle, columnWidthStyle);
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
            noteHeightScaleToWidth.BindValueChanged(_ => scheduleTextureRefresh());
            columnWidthStyle.BindValueChanged(_ => scheduleTextureRefresh());
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

            // 列宽风格可能改写实际列宽（全局总宽度按 10 / keyMode 摊到每列）。note 高度由宽度推导，
            // 不跟着换算就会在风格切换后压扁。
            x *= ezConfig.GetColumnWidthStyleScale(cacheKey.KeyMode);

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
            return CreateStage(columnCount, null);
        }

        /// <summary>
        /// 构造舞台板。<paramref name="keyState"/> 为每列的按下状态，用于驱动按键贴图。
        /// </summary>
        public Container CreateStage(int columnCount, EzStageKeyState? keyState)
            => CreateStage(columnCount, keyState, out _);

        /// <summary>
        /// 构造舞台板，并给出按键层供调用方按列宽调整尺寸（layout 声明了 <c>keysColumnWidth</c> 时）。
        /// </summary>
        public Container CreateStage(int columnCount, EzStageKeyState? keyState, out EzStageKeyLayer? keyLayer)
        {
            keyLayer = null;

            // 舞台板的 (0, 0) 是判定线：EzStageAnchorContainer 保持零尺寸，子项沿用 layout 的绝对坐标，
            // 外层把这个原点放到游戏判定线位置再缩放，因此列宽与画布缩放都不会移动判定线。
            var container = new EzStageAnchorContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };

            EzStagePlateLayout? layout = getPlateLayout();

            if (layout == null)
                return container;

            string basePath = $"Stage/{stageName.Value}/Stage";
            string? keyFolder = ResolveStageKeyFolder(resource.ListSubdirectories(basePath), columnCount);

            // 先收集再按 order 排序：Unity 的层级来自 m_SortingOrder，各舞台并不统一，
            // 固定绘制先后会让角色、光效之类的附加图盖住数字。
            var layers = new List<(int Order, Drawable Drawable)>();

            void place(Drawable? drawable, int order)
            {
                if (drawable != null)
                    layers.Add((order, drawable));
            }

            place(addPlateSprite(layout.Back, $"{basePath}/BlackPanel"), layout.Back?.Order ?? EzStagePlateLayout.ORDER_BACK);

            Vector2 bodySize = Vector2.Zero;
            int bodyOrder = layout.Body?.Order ?? EzStagePlateLayout.ORDER_BODY;

            if (keyFolder != null)
                place(addPlateSprite(layout.Body, $"{basePath}/{keyFolder}/Body", out bodySize), bodyOrder);

            // 按键与数字都要能在 Body 之后，序号来自 layout；这里先算出来供后面复用。
            int keyOrder = layout.GetKeys(columnCount)?.FirstOrDefault()?.Order ?? EzStagePlateLayout.ORDER_KEYS;

            place(addPlateSprite(layout.GrooveLight, $"{basePath}/GrooveLight"), layout.GrooveLight?.Order ?? EzStagePlateLayout.ORDER_GROOVE_LIGHT);

            if (layout.Meter != null)
            {
                Drawable? gauge = createPlateDrawable($"{basePath}/GrooveGauge", layout.Meter.Gauge);
                Drawable? bright = createPlateDrawable($"{basePath}/GrooveGaugeLight", layout.Meter.Bright);

                if (gauge != null || bright != null)
                {
                    place(applyRotation(new EzStageGrooveMeter(layout.Meter, gauge, bright), layout.Meter),
                        layout.Meter.Order ?? EzStagePlateLayout.ORDER_METER);
                }
            }

            place(addPlateSprite(layout.OverObject, plateTexturePath(basePath, layout.OverObject, $"{stageName.Value}_OverObject/{stageName.Value}_OverObject")),
                  layout.OverObject?.Order ?? EzStagePlateLayout.ORDER_OVER_OBJECT);

            if (layout.Parts != null)
            {
                foreach (EzStagePlateSprite part in layout.Parts)
                {
                    if (string.IsNullOrEmpty(part.Path))
                        continue;

                    place(addPlateSprite(part, $"{basePath}/{part.Path}"), part.Order ?? EzStagePlateLayout.ORDER_OVER_OBJECT);
                }
            }

            place(createPlateCounter(layout.Score, EzStagePlateCounter.Source.Score), layout.Score?.Order ?? EzStagePlateLayout.ORDER_DIGITS);
            place(createPlateCounter(layout.Combo, EzStagePlateCounter.Source.MaxCombo), layout.Combo?.Order ?? EzStagePlateLayout.ORDER_DIGITS);

            keyLayer = mountKeys(layout, keyFolder, basePath, columnCount, keyState);
            place(keyLayer, keyOrder);

            if (layout.Character != null)
            {
                Drawable? character = null;

                if (!string.IsNullOrEmpty(layout.Character.Path))
                    character = addPlateSprite(layout.Character, $"{basePath}/{layout.Character.Path}");

                if (character == null && keyFolder != null)
                    character = addPlateSprite(layout.Character, $"{basePath}/{keyFolder}/Character");

                character ??= addPlateSprite(layout.Character, $"{basePath}/Character_overlayer");

                place(character, layout.Character.Order ?? EzStagePlateLayout.ORDER_CHARACTER);
            }

            // 判定线的偏移决定整块舞台板的上移量，必须在加子项前算出来。
            Drawable? judgementLine = createJudgementLine(layout.JudgementLine, basePath, bodySize, out float judgementLineOffset);

            if (judgementLineOffset != 0)
                container.Y = -judgementLineOffset;

            place(judgementLine, layout.JudgementLine?.Order ?? EzStagePlateLayout.ORDER_JUDGEMENT_LINE);

            // OrderBy 是稳定排序：同层的部件保持上面写死的先后。
            container.AddRange(layers.OrderBy(l => l.Order).Select(l => l.Drawable));

            return container;
        }

        /// <summary>
        /// 把静态旋转套在部件外面：容器摆在支点，部件改写成相对支点的偏移。
        /// 旋转为 0 时不加任何层级，渲染路径与旧行为一致。
        /// </summary>
        private static Drawable applyRotation(Drawable drawable, EzStagePlateElement element)
        {
            if (!element.HasRotation)
                return drawable;

            Vector2 pivot = element.ToOsuPivot();
            drawable.Position = element.ToOsuPosition() - pivot;

            // 用零尺寸的锚点容器：它自身不能被裁剪判定丢掉，否则支点移出屏幕时子项会整块消失。
            return new EzStageAnchorContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Position = pivot,
                Rotation = element.Rotation,
                Child = drawable,
            };
        }

        /// <summary>
        /// 构造按键层。位置与文件名全部来自 stage layout；该键数没有声明时整层不画，
        /// 因此缺美术的回退组合不会画到错误的位置上。
        /// </summary>
        private EzStageKeyLayer? mountKeys(EzStagePlateLayout layout, string? keyFolder, string basePath, int columnCount, EzStageKeyState? keyState)
        {
            IReadOnlyList<EzStagePlateKey>? keys = layout.GetKeys(columnCount);

            if (keys == null || keyFolder == null)
                return null;

            var bases = new List<Drawable?>(keys.Count);
            var presses = new List<Drawable?>(keys.Count);
            var pressed = new List<IBindable<bool>?>(keys.Count);

            // 键图目录名与 Body 相同（_8key），文件直接放在 keybase / keypress 下。
            for (int i = 0; i < keys.Count; i++)
            {
                EzStagePlateKey key = keys[i];
                var position = key.ToOsuPosition();

                bases.Add(createKeyDrawable(basePath, keyFolder, "keybase", key.Path, position, key));
                presses.Add(createKeyDrawable(basePath, keyFolder, "keypress", key.PressPath, position, key));
                pressed.Add(keyState?.GetPressed(i));
            }

            return new EzStageKeyLayer(bases, presses, pressed, layout.UsesColumnWidth(columnCount));
        }

        // 文件名由 layout 给出，且只是前缀：帧序号不在数据里，按前缀把该列所有帧一起取出来。
        // 先查该键数自己的目录；派生排列（7/9/10 复用 5 张图）的时候源图在别的目录，再逐个回退。
        private Drawable? createKeyDrawable(string basePath, string? keyFolder, string folder, string? stem, Vector2 position, EzStagePlateKey key)
        {
            if (string.IsNullOrWhiteSpace(stem))
                return null;

            List<Texture> frames = loadKeyFrames(basePath, keyFolder, folder, keyPrefixOf(stem.Trim()));

            if (frames.Count == 0)
                return null;

            // 键图按源图像素绘制：纹理池按 @2x 处理（scaleAdjust=2），只对舞台板贴图合适；
            // 键图是 1x 资源，必须还原这一半，否则会变成半大小。旧 EzKeyArea 的 ×2 就是干这个的。
            Vector2 size = frames[0].DisplaySize * frames[0].ScaleAdjust;

            Drawable drawable;

            if (frames.Count == 1)
            {
                drawable = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Position = position,
                    Size = size,
                    Texture = frames[0],
                };
            }
            else
            {
                var animation = new TextureAnimation
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.None,
                    Position = position,
                    Size = size,
                    Loop = true,
                    DefaultFrameLength = keyFrameLength,
                };

                animation.AddFrames(frames);
                drawable = animation;
            }

            return applyRotation(drawable, key);
        }

        private double keyFrameLength => 1000.0 / StageKeyPressFps;

        // 去掉结尾的帧序号，得到这一列帧序列的公共前缀。layout 只存前缀，这里兼容早期带序号的数据。
        private static string keyPrefixOf(string stem)
        {
            int end = stem.Length;

            while (end > 0 && char.IsAsciiDigit(stem[end - 1]))
                end--;

            if (end == stem.Length || end == 0)
                return stem;

            char separator = stem[end - 1];

            return separator is '_' or '-' ? stem[..(end - 1)] : stem;
        }

        // 先查该键数的目录，再逐个看其它键数目录；帧序列按序号排序后交给 store 加载。
        private List<Texture> loadKeyFrames(string basePath, string? keyFolder, string folder, string prefix)
        {
            if (keyFolder != null)
            {
                List<Texture> preferred = resource.LoadStageFramesFromKeys(resolveKeyFrameKeys($"{basePath}/{keyFolder}/{folder}", prefix));

                if (preferred.Count > 0)
                    return preferred;
            }

            foreach (string candidate in resource.ListSubdirectories(basePath))
            {
                if (candidate == keyFolder || !candidate.EndsWith("key", StringComparison.OrdinalIgnoreCase))
                    continue;

                List<Texture> frames = resource.LoadStageFramesFromKeys(resolveKeyFrameKeys($"{basePath}/{candidate}/{folder}", prefix));

                if (frames.Count > 0)
                    return frames;
            }

            return new List<Texture>();
        }

        // 目录里的实际帧：前缀相同、后接 _ 或 - 与数字的文件，按数字升序。
        private IReadOnlyList<string> resolveKeyFrameKeys(string directory, string prefix)
        {
            var matches = new List<(int Index, string Key)>();

            foreach (string key in resource.ListImageKeys(directory))
            {
                string stem = EzResourcePath.Stem(EzResourcePath.FileName(key));

                if (!tryGetFrameIndex(stem, prefix, out int index))
                    continue;

                matches.Add((index, key));
            }

            if (matches.Count == 0)
                return Array.Empty<string>();

            matches.Sort((a, b) => a.Index.CompareTo(b.Index));

            string[] keys = new string[matches.Count];

            for (int i = 0; i < matches.Count; i++)
                keys[i] = matches[i].Key;

            return keys;
        }

        private static bool tryGetFrameIndex(string stem, string prefix, out int index)
        {
            index = 0;

            if (stem.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                return true;

            if (stem.Length <= prefix.Length + 1 || !stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            char separator = stem[prefix.Length];

            if (separator is not ('_' or '-'))
                return false;

            ReadOnlySpan<char> digits = stem.AsSpan(prefix.Length + 1);

            foreach (char c in digits)
            {
                if (!char.IsAsciiDigit(c))
                    return false;
            }

            return int.TryParse(digits, out index);
        }

        /// <summary>
        /// 构造判定线贴图。<paramref name="lineOffset"/> 是判定线相对 Body 中心的偏移，
        /// 舞台板要整体上移该值，使其 (0, 0) 正好落在判定线上。
        /// </summary>
        private Drawable? createJudgementLine(EzStagePlateJudgementLine? piece, string basePath, Vector2 bodySize, out float lineOffset)
        {
            // ratio 相对 Body 底边，用 Body 的实际绘制高度换算，因此不受 store 缩放与源图分辨率影响。
            // 舞台没有声明 judgementLine 时无从得知判定线位置，退化为以板中心为锚点。
            lineOffset = piece != null && bodySize.Y > 0
                ? EzStagePlacement.GetJudgementLineOffset(bodySize.Y, piece.Ratio)
                : 0;

            // 下拉选中的图优先；留空时才退回舞台 layout 声明的判定线；两者都没有就不画。
            List<Texture> frames = loadStageJudgementFrames(piece, basePath);

            if (frames.Count == 0)
                return null;

            var position = new Vector2(-(piece?.X ?? 0), lineOffset);

            // 贴图按自身显示尺寸绘制，随舞台板一起缩放；这与旧 EzJudgementLine 的视觉一致。
            Vector2 size = frames[0].DisplaySize;

            Drawable drawable;

            if (frames.Count == 1)
            {
                drawable = new Sprite
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Position = position,
                    Size = size,
                    Texture = frames[0],
                };
            }
            else
            {
                var animation = new TextureAnimation
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.None,
                    Position = position,
                    Size = size,
                    Loop = true,
                    DefaultFrameLength = piece?.FrameLength ?? 66,
                };

                animation.AddFrames(frames);
                drawable = animation;
            }

            return drawable;
        }

        /// <summary>
        /// 判定线帧：<c>EzResources/JudgementLine</c> 当前选中的那张优先，留空时才用舞台目录里声明的图。
        /// </summary>
        private List<Texture> loadStageJudgementFrames(EzStagePlateJudgementLine? piece, string basePath)
        {
            string name = judgementLineName.Value?.Trim() ?? string.Empty;

            if (name.Length > 0)
            {
                if (name.StartsWith(EzResourceDiscovery.judgement_line_prefix, StringComparison.OrdinalIgnoreCase))
                    name = name[EzResourceDiscovery.judgement_line_prefix.Length..];

                if (name.Length > 0)
                {
                    List<Texture> overrideFrames = loadStageComponentFrames($"JudgementLine/{EzResourceDiscovery.judgement_line_prefix}{name}");

                    // 不带前缀的 {name}.png 也接受，与资源选择列表的口径一致。
                    if (overrideFrames.Count == 0)
                        overrideFrames = loadStageComponentFrames($"JudgementLine/{name}");

                    if (overrideFrames.Count > 0)
                        return overrideFrames;
                }
            }

            string? stagePath = piece?.Path?.Trim().Trim('/');

            if (!string.IsNullOrEmpty(stagePath))
                stagePath = $"{basePath}/{stagePath}";
            else if (piece != null)
                stagePath = $"{basePath}/JudgementLine";

            return stagePath != null ? loadStageComponentFrames(stagePath) : new List<Texture>();
        }

        private EzStagePlateLayout? getPlateLayout()
        {
            if (plateLayoutStage == stageName.Value)
                return plateLayout;

            plateLayoutStage = stageName.Value;
            plateLayout = EzStagePlateLayout.TryLoad(resource, stageName.Value);
            return plateLayout;
        }

        /// <summary>
        /// 当前舞台的按键动画帧率（keybase 与 keypress 共用），由 stage layout 的 <c>keyPressFps</c> 声明；
        /// 未声明或非正数时为 <see cref="EzStagePlateLayout.DEFAULT_KEY_PRESS_FPS"/>（与旧固定帧长等价）。
        /// </summary>
        public float StageKeyPressFps
        {
            get
            {
                float fps = getPlateLayout()?.KeyPressFps ?? 0;
                return fps > 0 ? fps : EzStagePlateLayout.DEFAULT_KEY_PRESS_FPS;
            }
        }

        /// <summary>
        /// 当前舞台的设计宽度，由 stage layout 声明；未声明或非法时为 <see cref="EzStagePlacement.DEFAULT_DESIGN_WIDTH"/>。
        /// </summary>
        public float StageDesignWidth
        {
            get
            {
                float width = getPlateLayout()?.DesignWidth ?? EzStagePlacement.DEFAULT_DESIGN_WIDTH;
                return width > 0 ? width : EzStagePlacement.DEFAULT_DESIGN_WIDTH;
            }
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

            if (best[columnCount] != null)
                return best[columnCount];

            // 先耗尽同奇偶（先下后上，向下不低于 4），再按同样顺序耗尽异奇偶。
            int parity = columnCount & 1;

            return scanParity(parity) ?? scanParity(parity ^ 1);

            string? scanParity(int wantedParity)
            {
                if (columnCount >= stage_key_downward_floor)
                {
                    for (int keys = columnCount - 1; keys >= stage_key_downward_floor; keys--)
                    {
                        if ((keys & 1) != wantedParity)
                            continue;

                        if (best[keys] != null)
                            return best[keys];
                    }
                }

                for (int keys = columnCount + 1; keys <= MAX_STAGE_KEYS; keys++)
                {
                    if ((keys & 1) != wantedParity)
                        continue;

                    if (best[keys] != null)
                        return best[keys];
                }

                return null;
            }
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

        private static string plateTexturePath(string basePath, EzStagePlateSprite? piece, string fallback)
        {
            string relative = piece != null && !string.IsNullOrEmpty(piece.Path) ? piece.Path : fallback;
            return $"{basePath}/{relative}";
        }

        private static Drawable? createPlateCounter(EzStagePlateDigits? digits, EzStagePlateCounter.Source source)
        {
            if (digits == null || !digits.Enabled || digits.Digits <= 0)
                return null;

            return new EzStagePlateCounter(digits, source);
        }

        private Drawable? addPlateSprite(EzStagePlateSprite? piece, string texturePath)
            => addPlateSprite(piece, texturePath, out _);

        // 返回已套上旋转的 drawable（加入容器用）；contentSize 供 Body 之类需要度量尺寸的地方使用。
        private Drawable? addPlateSprite(EzStagePlateSprite? piece, string texturePath, out Vector2 contentSize)
        {
            contentSize = Vector2.Zero;

            if (piece == null)
                return null;

            Drawable? drawable = createPlateDrawable(texturePath, piece, out contentSize);

            return drawable == null ? null : applyRotation(drawable, piece);
        }

        private Drawable? createPlateDrawable(string texturePath, EzStagePlateSprite? piece)
            => createPlateDrawable(texturePath, piece, out _);

        private Drawable? createPlateDrawable(string texturePath, EzStagePlateSprite? piece, out Vector2 contentSize)
        {
            contentSize = Vector2.Zero;

            if (piece == null)
                return null;

            var frames = loadStageComponentFrames(texturePath);

            if (frames.Count == 0)
                return null;

            // 首帧的显示尺寸就是 drawable 铺开后的尺寸；动画在布局前 Height 还是 0，所以这里按帧取。
            contentSize = new Vector2(frames[0].DisplayWidth, frames[0].DisplayHeight);

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
                {
                    applySpin(sprite, piece);
                    return new EzStageSingleLightBreath(sprite);
                }

                applySpin(sprite, piece);
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
            applySpin(animation, piece);
            return animation;
        }

        private static void applySpin(Drawable drawable, EzStagePlateSprite piece)
        {
            EzStagePlateSpin? spin = piece.Spin;

            if (spin == null || spin.Seconds <= 0 || spin.Degrees == 0)
                return;

            double revolution = spin.Seconds * 360.0 / Math.Abs(spin.Degrees);
            RotationDirection direction = spin.Degrees > 0 ? RotationDirection.Counterclockwise : RotationDirection.Clockwise;
            bool loop = spin.Loop;
            float angle = direction == RotationDirection.Counterclockwise ? -Math.Abs(spin.Degrees) : Math.Abs(spin.Degrees);
            double seconds = spin.Seconds;

            // Loop 会读 Clock.Time。零件这时还没挂进画面，时钟为空。
            void start(Drawable target)
            {
                target.OnLoadComplete -= start;

                if (loop)
                    target.Spin(revolution, direction);
                else
                    target.RotateTo(angle, seconds, Easing.None);
            }

            if (drawable.IsLoaded)
                start(drawable);
            else
                drawable.OnLoadComplete += start;
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
                DefaultFrameLength = getStageKeyFrameLength(component),
            };
            animation.AddFrames(frames);

            return animation;
        }

        // keybase / keypress 的帧长由 stage layout 的 keyPressFps 决定，两者共用同一速率。
        private double getStageKeyFrameLength(string component)
        {
            if (component != "KeyPress" && component != "KeyBase")
                return default_frame_length * 4;

            return 1000.0 / StageKeyPressFps;
        }

        // keybase/keypress 四条路径的尝试与「累计为空才吃单图」属皮肤编排，留在工厂；单条路径的帧解析交给层2。
        private List<Texture> loadStageKeysFrames(string component, string? keySuffix = null)
        {
            var frames = new List<Texture>();
            string currentStageName = stageName.Value;

            string[] pathsToTry =
            {
                $"Stage/{currentStageName}/Stage/_5key/keybase/{component}",
                $"Stage/{currentStageName}/Stage/_5key/keypress/{component}",
                $"Stage/{currentStageName}/Stage/_5key/keybase/{component}_{keySuffix}",
                $"Stage/{currentStageName}/Stage/_5key/keypress/{component}_{keySuffix}",
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
