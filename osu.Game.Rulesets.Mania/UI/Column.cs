// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Game.Extensions;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Audio;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.UI.Components;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.UI
{
    [Cached]
    public partial class Column : ScrollingPlayfield, IKeyBindingHandler<ManiaAction>
    {
        public const float COLUMN_WIDTH = 80;
        public const float SPECIAL_COLUMN_WIDTH = 70;

        /// <summary>
        /// The index of this column as part of the whole playfield.
        /// </summary>
        public readonly int Index;

        public readonly Bindable<ManiaAction> Action = new Bindable<ManiaAction>();

        public readonly ColumnHitObjectArea HitObjectArea;

        internal readonly Container BackgroundContainer = new Container { RelativeSizeAxes = Axes.Both };

        internal readonly Container TopLevelContainer = new Container { RelativeSizeAxes = Axes.Both };

        private DrawablePool<PoolableHitExplosion> hitExplosionPool = null!;
        private readonly OrderedHitPolicy hitPolicy;
        public Container UnderlayElements => HitObjectArea.UnderlayElements;

        private GameplaySampleTriggerSource sampleTriggerSource = null!;

        /// <summary>
        /// Whether this is a special (ie. scratch) column.
        /// </summary>
        public readonly bool IsSpecial;

        public readonly Bindable<Color4> AccentColour = new Bindable<Color4>(Color4.Black);

        private IBindable<bool> touchOverlay = null!;

        private float leftColumnSpacing;
        private float rightColumnSpacing;

        public Column(int index, bool isSpecial)
        {
            Index = index;
            IsSpecial = isSpecial;

            RelativeSizeAxes = Axes.Y;
            Width = COLUMN_WIDTH;

            hitPolicy = new OrderedHitPolicy(HitObjectContainer);
            HitObjectArea = new ColumnHitObjectArea
            {
                RelativeSizeAxes = Axes.Both,
                Child = HitObjectContainer,
            };
        }

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory ezFactory { get; set; } = null!;

        // 保留备用，以后有精力对比一下全局注入和列级缓存的差异
        // [Cached(Type = typeof(IEzSkinInfo))]
        // private readonly EzSkinInfo ezSkinInfo = new EzSkinInfo();
        //
        // public IEzSkinInfo EzSkinInfo => ezSkinInfo;

        public Bindable<Colour4> EzNoteColourBindable = new Bindable<Colour4>(Color4.White);
        public Bindable<Vector2> EzNoteSizeBindable = null!;
        public Bindable<string> EzNoteSetNameBindable = null!;
        private Bindable<EzEnumHitMode> hitModeBindable = null!;

        private KeySoundPreviewMode keySoundPreviewMode;

        // 存储对本地事件的引用，以便在 Dispose 中正确取消订阅
        private Action onNoteSizeChangedHandler = null!;
        private Action onNoteColourChangedHandler = null!;

        // 缓存计算参数，避免闭包捕获
        private int cachedKeyMode;
        private bool? cachedNoSpecial;

        [BackgroundDependencyLoader]
        private void load(GameHost host, ManiaRulesetConfigManager? rulesetConfig, StageDefinition stageDefinition)
        {
            keySoundPreviewMode = ezConfig.Get<KeySoundPreviewMode>(Ez2Setting.KeySoundPreviewMode);

            // 缓存计算参数，避免每次创建闭包
            cachedKeyMode = stageDefinition.Columns;
            cachedNoSpecial = null;

            EzNoteSizeBindable = ezFactory.GetNoteSize(cachedKeyMode, Index, cachedNoSpecial);
            EzNoteColourBindable = ezConfig.GetColumnColorBindable(stageDefinition.Columns, Index);
            EzNoteSetNameBindable = ezConfig.GetBindable<string>(Ez2Setting.NoteSetName);

            SkinnableDrawable keyArea;

            skin.SourceChanged += onSourceChanged;
            onSourceChanged();

            InternalChildren = new Drawable[]
            {
                hitExplosionPool = new DrawablePool<PoolableHitExplosion>(5),
                sampleTriggerSource = new GameplaySampleTriggerSource(HitObjectContainer),
                HitObjectArea,
                keyArea = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.KeyArea), _ => new DefaultKeyArea())
                {
                    RelativeSizeAxes = Axes.Both,
                },
                // For input purposes, the background is added at the highest depth, but is then proxied back below all other elements externally
                // (see `Stage.columnBackgrounds`).
                BackgroundContainer,
                TopLevelContainer
            };

            var background = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.ColumnBackground), _ => new DefaultColumnBackground())
            {
                RelativeSizeAxes = Axes.Both,
            };

            background.ApplyGameWideClock(host);
            keyArea.ApplyGameWideClock(host);

            BackgroundContainer.Add(background);
            TopLevelContainer.Add(HitObjectArea.Explosions.CreateProxy());

            hitModeBindable = ezConfig.GetBindable<EzEnumHitMode>(Ez2Setting.HitMode);
            hitModeBindable.BindValueChanged(mode => configurePools(mode.NewValue), true);

            if (rulesetConfig != null)
                touchOverlay = rulesetConfig.GetBindable<bool>(ManiaRulesetSetting.TouchOverlay);
        }

        private void onSourceChanged()
        {
            AccentColour.Value = skin.GetManiaSkinConfig<Color4>(LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour, Index)?.Value ?? Color4.Black;

            leftColumnSpacing = skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                                        new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.LeftColumnSpacing, Index))
                                    ?.Value ?? Stage.COLUMN_SPACING;

            rightColumnSpacing = skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                                         new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.RightColumnSpacing, Index))
                                     ?.Value ?? Stage.COLUMN_SPACING;
        }

        public event Action? NoteSetChanged;
        public event Action? NoteSizeChanged;
        public event Action? NoteColourChanged;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            NewResult += OnNewResult;

            // 使用缓存的参数创建 Action，避免闭包捕获和重复计算
            onNoteSizeChangedHandler = () =>
            {
                EzNoteSizeBindable.Value = ezFactory.GetNoteSize(cachedKeyMode, Index, cachedNoSpecial).Value;
            };

            onNoteColourChangedHandler = () => NoteColourChanged?.Invoke();

            ezFactory.OnNoteSizeChanged += onNoteSizeChangedHandler;
            ezFactory.OnNoteColourChanged += onNoteColourChangedHandler;

            EzNoteSetNameBindable.BindValueChanged(_ => NoteSetChanged?.Invoke());
            EzNoteSizeBindable.BindValueChanged(_ => NoteSizeChanged?.Invoke());
            EzNoteColourBindable.BindValueChanged(_ => NoteColourChanged?.Invoke());
        }

        protected override void Dispose(bool isDisposing)
        {
            // must happen before children are disposed in base call to prevent illegal accesses to the hit explosion pool.
            NewResult -= OnNewResult;

            // 解除对 EzLocalTextureFactory 事件的订阅
            ezFactory.OnNoteColourChanged -= onNoteColourChangedHandler;
            ezFactory.OnNoteSizeChanged -= onNoteSizeChangedHandler;

            NoteSetChanged = null;
            NoteSizeChanged = null;
            NoteColourChanged = null;

            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSourceChanged;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IBindable<ManiaAction>>(Action);
            // dependencies.CacheAs(EzSkinInfo);

            return dependencies;
        }

        protected override void OnNewDrawableHitObject(DrawableHitObject drawableHitObject)
        {
            base.OnNewDrawableHitObject(drawableHitObject);

            DrawableManiaHitObject maniaObject = (DrawableManiaHitObject)drawableHitObject;

            maniaObject.AccentColour.BindTo(AccentColour);
            maniaObject.CheckHittable = hitPolicy.IsHittable;
        }

        internal void OnNewResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            if (result.IsHit)
                hitPolicy.HandleHit(judgedObject);

            if (!result.IsHit || !judgedObject.DisplayResult || !DisplayJudgements.Value)
                return;

            HitObjectArea.Explosions.Add(hitExplosionPool.Get(e => e.Apply(result)));
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action != Action.Value)
                return false;

            // 记录延迟追踪按键输入
            InputAudioLatencyTracker.Instance?.RecordColumnPress(Index);

            if (keySoundPreviewMode != KeySoundPreviewMode.AutoPlayPlus)
                sampleTriggerSource.Play();
            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            // Extend input coverage to the gaps close to this column.
            var spacingInflation = new MarginPadding { Left = leftColumnSpacing, Right = rightColumnSpacing };
            return DrawRectangle.Inflate(spacingInflation).Contains(ToLocalSpace(screenSpacePos));
        }

        private void configurePools(EzEnumHitMode hitMode)
        {
            switch (hitMode)
            {
                case EzEnumHitMode.EZ2AC:
                    RegisterPool<Note, Ez2AcDrawableNote>(10, 50);
                    RegisterPool<HoldNote, DrawableHoldNote>(10, 50);
                    RegisterPool<HeadNote, DrawableHoldNoteHead>(10, 50);
                    RegisterPool<TailNote, Ez2AcDrawableLNTail>(10, 50);
                    RegisterPool<HoldNoteBody, DrawableHoldNoteBody>(10, 50);
                    break;

                case EzEnumHitMode.Malody:
                    RegisterPool<Note, DrawableNote>(10, 50);
                    RegisterPool<HoldNote, DrawableHoldNote>(10, 50);
                    RegisterPool<HeadNote, DrawableHoldNoteHead>(10, 50);
                    RegisterPool<TailNote, MalodyDrawableLNTail>(10, 50);
                    RegisterPool<HoldNoteBody, MalodyDrawableLNBody>(10, 50);
                    break;

                case EzEnumHitMode.IIDX_HD:
                case EzEnumHitMode.LR2_HD:
                case EzEnumHitMode.Raja_NM:
                    RegisterPool<Note, BMSDrawableNote>(10, 50);
                    RegisterPool<HoldNote, DrawableHoldNote>(10, 50);
                    RegisterPool<HeadNote, BMSDrawableHoldNoteHead>(10, 50);
                    RegisterPool<TailNote, BMSDrawableHoldNoteTail>(10, 50);
                    RegisterPool<HoldNoteBody, DrawableHoldNoteBody>(10, 50);
                    break;

                case EzEnumHitMode.O2Jam:
                    RegisterPool<Note, O2DrawableNote>(10, 50);
                    RegisterPool<HoldNote, O2DrawableHoldNote>(10, 50);
                    RegisterPool<HeadNote, O2DrawableHoldNoteHead>(10, 50);
                    RegisterPool<TailNote, O2DrawableHoldNoteTail>(10, 50);
                    RegisterPool<HoldNoteBody, DrawableHoldNoteBody>(10, 50);
                    break;

                default:
                    RegisterPool<Note, DrawableNote>(10, 50);
                    RegisterPool<HoldNote, DrawableHoldNote>(10, 50);
                    RegisterPool<HeadNote, DrawableHoldNoteHead>(10, 50);
                    RegisterPool<TailNote, DrawableHoldNoteTail>(10, 50);
                    RegisterPool<HoldNoteBody, DrawableHoldNoteBody>(10, 50);
                    break;
            }
        }

        #region Touch Input

        [Resolved]
        private ManiaInputManager? maniaInputManager { get; set; }

        private int touchActivationCount;

        protected override bool OnTouchDown(TouchDownEvent e)
        {
            // if touch overlay is visible, disallow columns from handling touch directly.
            if (touchOverlay.Value)
                return false;

            maniaInputManager?.KeyBindingContainer.TriggerPressed(Action.Value);
            touchActivationCount++;
            return true;
        }

        protected override void OnTouchUp(TouchUpEvent e)
        {
            touchActivationCount--;

            if (touchActivationCount == 0)
                maniaInputManager?.KeyBindingContainer.TriggerReleased(Action.Value);
        }

        #endregion
    }
}
