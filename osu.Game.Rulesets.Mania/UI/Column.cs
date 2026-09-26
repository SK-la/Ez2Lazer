// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using osu.Game.EzOsuGame.Diagnostics;
using osu.Game.Rulesets.Mania.EzMania.Audio;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Input;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.UI.Components;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
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
        private OrderedHitPolicyHelper hitPolicyHelper = null!;
        private DrawableHitObject? columnRoutedPressTarget;
        private EzEnumJudgePrecedence judgePrecedence;
        private EzEnumHitMode? configuredMissCollectionHitMode;

        /// <summary>命中时「提前判定为 miss」的条目缓冲，避免每次命中分配一个迭代器。</summary>
        private readonly List<ManiaLaneEntry> forceMissScratch = new List<ManiaLaneEntry>();

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

        [Resolved(canBeNull: true)]
        private DrawableManiaRuleset? drawableRuleset { get; set; }

        internal ManiaLaneController LaneController { get; private set; } = null!;

        private double pressHistoryRetentionMs = 120_000;

        // 环形缓冲，避免在高 KPS 下占用过多内存。4096 个按键记录在 120s 内的 KPS 为 34.1，足够覆盖绝大多数情况。
        private readonly RingBufferPressTimes pressTimes = new RingBufferPressTimes(4096);

        internal IReadOnlyList<double> PressTimes => pressTimes;

        internal bool TryGetBmsRoute(DrawableNote note, out BmsHitModeJudgement.BmsRouteState route)
        {
            if (LaneController.TryGetEntry(note, out var entry))
            {
                route = entry.BmsRoute;
                return true;
            }

            route = null!;
            return false;
        }

        public Bindable<string> NoteSetNameBindable = null!;
        public Bindable<bool> ColorSettingsEnabledBindable = null!;
        public Bindable<Colour4> EzNoteColourBindable = null!;
        public Bindable<Vector2> EzNoteSizeBindable = null!;
        public Bindable<EzColumnType> EzNoteTypeBindable = null!;

        private KeySoundPreviewMode keySoundPreviewMode;

        private Action<int, int, EzColumnType>? onColumnTypeChangedHandler;
        private Action? onNoteDrawableChangedHandler;
        private Action? onNoteSizeChangedHandler;
        private Action? onNoteColourChangedHandler;

        // 缓存计算参数，避免闭包捕获
        public int KeyMode;
        public bool ConfigTimingBasedNoteColouring;

        protected override ScrollingHitObjectContainer CreateScrollingHitObjectContainer()
            => new LaneTrackingScrollingHitObjectContainer(this);

        internal void RegisterLaneDrawable(DrawableHitObject drawable)
            => LaneController.Register(drawable, drawableRuleset?.ColumnRoutesInput == true);

        internal void UnregisterLaneDrawable(DrawableHitObject drawable) => LaneController.Unregister(drawable);

        [BackgroundDependencyLoader]
        private void load(GameHost host, ManiaRulesetConfigManager? rulesetConfig, StageDefinition stageDefinition)
        {
            KeyMode = stageDefinition.Columns;

            // JudgePrecedence 来自全局配置（与 ManiaJudgementRound.Create 一致）；勿 [Resolved] DrawableManiaRuleset——
            // 皮肤预览等场景会单独构造 Column，且 JudgementRound 在 ruleset LoadComplete 才冻结。
            judgePrecedence = ezConfig.Get<EzEnumJudgePrecedence>(Ez2Setting.JudgePrecedence);

            LaneController = new ManiaLaneController();
            hitPolicyHelper = new OrderedHitPolicyHelper(HitObjectContainer, LaneController);

            EzNoteTypeBindable = ezConfig.GetColumnTypeBindable(KeyMode, Index);
            EzNoteSizeBindable = ezFactory.GetNoteSizeBindable(KeyMode, Index);
            EzNoteColourBindable = ezConfig.GetColumnColorBindable(KeyMode, Index);
            NoteSetNameBindable = ezConfig.GetBindable<string>(Ez2Setting.NoteSetName);
            ColorSettingsEnabledBindable = ezConfig.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);

            if (rulesetConfig != null) ConfigTimingBasedNoteColouring = rulesetConfig.Get<bool>(ManiaRulesetSetting.TimingBasedNoteColouring);

            SkinnableDrawable keyArea;

            skin.SourceChanged += onSourceChanged;
            onSourceChanged();

            InternalChildren = new Drawable[]
            {
                hitExplosionPool = new DrawablePool<PoolableHitExplosion>(5),
                sampleTriggerSource = new EzGameplaySampleTriggerSource(HitObjectContainer),
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

            RegisterPool<Note, DrawableNote>(10, 50);
            RegisterPool<HoldNote, DrawableHoldNote>(10, 50);
            RegisterPool<HeadNote, DrawableHoldNoteHead>(10, 50);
            RegisterPool<TailNote, DrawableHoldNoteTail>(10, 50);
            RegisterPool<HoldNoteBody, DrawableHoldNoteBody>(10, 50);
            RegisterPool<HoldNoteTick, DrawableHoldNoteTick>(50, 200);

            if (rulesetConfig != null)
                touchOverlay = rulesetConfig.GetBindable<bool>(ManiaRulesetSetting.TouchOverlay);

            keySoundPreviewMode = ezConfig.Get<KeySoundPreviewMode>(Ez2Setting.KeySoundPreviewMode);
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

            drawableRuleset ??= this.FindClosestParent<DrawableManiaRuleset>();
            NewResult += OnNewResult;

            onNoteDrawableChangedHandler = () => NoteSetChanged?.Invoke();
            ezFactory.OnNoteDrawableChanged += onNoteDrawableChangedHandler;

            onColumnTypeChangedHandler = (keyMode, columnIndex, _) =>
            {
                if (keyMode == KeyMode && columnIndex == Index)
                    NoteSetChanged?.Invoke();
            };
            ezConfig.ColumnTypeChanged += onColumnTypeChangedHandler;

            onNoteSizeChangedHandler = () => NoteSizeChanged?.Invoke();
            ezFactory.OnNoteSizeChanged += onNoteSizeChangedHandler;

            onNoteColourChangedHandler = () => NoteColourChanged?.Invoke();
            ezFactory.OnNoteColourChanged += onNoteColourChangedHandler;
        }

        protected override void Update()
        {
            if (drawableRuleset?.ColumnRoutesInput == true)
                LaneController.EnableAutoMissScheduling();

            base.Update();
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (drawableRuleset?.ColumnRoutesInput == true)
                LaneController.ProcessAutoMiss(Time.Current);
        }

        protected override void Dispose(bool isDisposing)
        {
            // must happen before children are disposed in base call to prevent illegal accesses to the hit explosion pool.
            NewResult -= OnNewResult;

            if (onNoteDrawableChangedHandler != null)
                ezFactory.OnNoteDrawableChanged -= onNoteDrawableChangedHandler;

            if (onColumnTypeChangedHandler != null)
                ezConfig.ColumnTypeChanged -= onColumnTypeChangedHandler;

            if (onNoteSizeChangedHandler != null)
                ezFactory.OnNoteSizeChanged -= onNoteSizeChangedHandler;

            if (onNoteColourChangedHandler != null)
                ezFactory.OnNoteColourChanged -= onNoteColourChangedHandler;

            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSourceChanged;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IBindable<ManiaAction>>(Action);
            return dependencies;
        }

        protected override void OnHitObjectAdded(HitObject hitObject)
        {
            // 不调用 base：base 只做 Playfield.preloadSamples，即按音频名为每个 sample 预建一个
            // DrawablePool<PoolableSkinnableSample>。note 音已全部由 ManiaPlayfield.SampleChannels 按音频名播放，
            // 这批池没有人取用，且每个池会在全局统计里留下一条永不释放的 PoolableSkinnableSample`N 条目，
            // 于是每开一局编号就往上累加一段。
            //
            // 唯一的例外是转换谱 LN 按住期间的滑动音：它仍走 note 自带的 SkinnableSound（名字由 Samples 派生、
            // 不在谱面 Samples 列表里），需要在这里预热，否则会在这条 LN 首次出现时才建池。
            if (hitObject is HoldNote holdNote && holdNote.PlaySlidingSamples)
            {
                foreach (var sample in holdNote.CreateSlidingSamples())
                    GetPooledSample(sample);
            }
        }

        protected override void OnNewDrawableHitObject(DrawableHitObject drawableHitObject)
        {
            base.OnNewDrawableHitObject(drawableHitObject);

            DrawableManiaHitObject maniaObject = (DrawableManiaHitObject)drawableHitObject;

            maniaObject.AccentColour.BindTo(AccentColour);
            maniaObject.CheckHittable = (d, time) =>
            {
                LaneController.RegisterIfNeeded(d, drawableRuleset?.ColumnRoutesInput == true);
                resolvePressRouting(out var precedence);
                return isHittable(d, time, precedence);
            };
            maniaObject.ShouldSkipColumnRoutedPress = _ => columnRoutedPressTarget != null;
        }

        private sealed partial class LaneTrackingScrollingHitObjectContainer : ScrollingHitObjectContainer
        {
            private readonly Column column;

            public LaneTrackingScrollingHitObjectContainer(Column column)
            {
                this.column = column;
            }

            protected override void AddDrawable(HitObjectLifetimeEntry entry, DrawableHitObject drawable)
            {
                base.AddDrawable(entry, drawable);
                column.RegisterLaneDrawable(drawable);
            }

            protected override void RemoveDrawable(HitObjectLifetimeEntry entry, DrawableHitObject drawable)
            {
                column.UnregisterLaneDrawable(drawable);
                base.RemoveDrawable(entry, drawable);
            }
        }

        internal void OnNewResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            if (result.IsHit)
                handleHit(judgedObject);
            else
                LaneController.NotifyJudged(judgedObject);

            if (!result.IsHit || !judgedObject.DisplayResult || !DisplayJudgements.Value)
                return;

            // 不用 setup 委托：`Apply` 只是记录 Result，而 `PrepareForUse`（真正读它的地方）在下一帧 Update 才跑，
            // 所以拿到实例后再赋值等价，且省掉每次判定一个捕获 result 的闭包。
            var explosion = hitExplosionPool.Get();
            explosion.Apply(result);
            HitObjectArea.Explosions.Add(explosion);
        }

        private bool isHittable(DrawableHitObject drawable, double time, EzEnumJudgePrecedence precedence)
        {
            ManiaJudgeHotPathTrace.RecordIsHittable();

            if (drawable is DrawableHoldNoteTail)
                return hitPolicyHelper.IsHittableWithPrecedence(drawable, time, precedence);

            if (LaneController.IsHittable(drawable, time, precedence))
                return true;

            // HoldNote fallback: when the Head has been auto-missed, TryCreateEntry rejects the HoldNote
            // from LaneController entries. Without an entry, LaneController.IsHittable returns false.
            // Fall through to hitPolicyHelper which checks hit windows via the HitObjectContainer directly.
            if (drawable is DrawableHoldNote)
                return hitPolicyHelper.IsHittableWithPrecedence(drawable, time, precedence);

            return false;
        }

        private bool applyRoutedPress(DrawableHitObject target, double time, KeyBindingPressEvent<ManiaAction> e)
        {
            switch (target)
            {
                case DrawableNote note when ManiaEzDrawableJudgement.TryBmsOnPressed(note, e):
                    columnRoutedPressTarget = target;
                    return true;

                case DrawableNote note:
                    if (!note.ApplyColumnRoutedPress())
                        return false;

                    columnRoutedPressTarget = target;
                    return true;

                case DrawableHoldNote hold:
                    if (!hold.TryBeginHoldPressFromColumn(time))
                        return false;

                    LaneController.SetActiveHold(hold);
                    columnRoutedPressTarget = target;
                    return true;

                default:
                    return false;
            }
        }

        private void handleHit(DrawableHitObject hitObject)
        {
            double judgementTime = hitObject.Result.TimeAbsolute;

            forceMissScratch.Clear();

            // [Ez] 探针消融开关：跳过「提前判 miss」的收集与判定，用于确认它是否属于延迟尾部。
            if (!EzPressLatencyDiagnostics.SkipForceMiss)
                LaneController.CollectForceMissBefore(hitObject.HitObject.StartTime, forceMissScratch);

            if (EzPressLatencyDiagnostics.Enabled)
                pressForceMissScan = forceMissScratch.Count;

            for (int i = 0; i < forceMissScratch.Count; i++)
            {
                var entry = forceMissScratch[i];

                // 走快照而不是实时枚举：其间 MissForcefully 会判掉条目（含其它条目），
                // 这里按实时状态再确认一次，语义与原来的惰性枚举一致。
                if (entry.IsPressJudged)
                    continue;

                if (OrderedHitPolicyHelper.IsUserTriggerJudgeableNow(entry.RoutedObject, judgementTime))
                    continue;

                ((DrawableManiaHitObject)entry.RoutedObject).MissForcefully();
            }

            LaneController.NotifyJudged(hitObject);
        }

        /// <summary>本次按键在 <see cref="handleHit"/> 中扫描的强制 miss 候选数（探针，跨按键在按键入口清零）。</summary>
        private int pressForceMissScan;

        /// <summary>探针只在本列首次按键时读一次非位置输入队列长度：读它会触发一次全树重建，不能每次按键都做。</summary>
        private bool inputQueueCountReported;

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action != Action.Value)
                return false;

            bool probe = EzPressLatencyDiagnostics.Enabled;
            long pressEnterTs = probe ? Stopwatch.GetTimestamp() : 0;

            // 首次命中的一次性成本（JIT / 惰性初始化 / 键音通道建立 / 池首次取用）只发生在本局前几次按键上，
            // 所以只在这几次按键上多取几个时间戳，之后不再进入该分支。
            bool breakdown = probe && EzPressLatencyDiagnostics.BreakdownActive;
            // 只在采集时才读本列的路由开关：关闭探针时这一行短路，生产路径仍按原样在下面自行判断。
            bool routesInput = breakdown && drawableRuleset?.ColumnRoutesInput == true;
            int entriesBefore = breakdown ? LaneController.Entries.Count : 0;
            long bookkeepingTs = pressEnterTs;
            long routeTs = pressEnterTs;
            long selectTs = pressEnterTs;
            long applyTs = pressEnterTs;

            pressForceMissScan = 0;

            double time = Time.Current;

            InputAudioLatencyTracker.Instance?.RecordColumnPress(Index);

            // 按键历史是 Ez 被动 miss stored TimeOffset 的输入（ManiaDrawableMissTiming → Session parity），
            // 保留时长也由判定窗口算出，属生产数据；只有计数埋点受 ManiaJudgeHotPathTrace.Enabled 控制。
            pressTimes.Add(time);
            pressTimes.Trim(time - pressHistoryRetentionMs);

            if (ManiaJudgeHotPathTrace.Enabled)
            {
                ManiaJudgeHotPathTrace.RecordPressTimesCount(pressTimes.Count);
                ManiaJudgeHotPathTrace.RecordColumnOnPressed();
            }

            // 取样必须先于路由判定：判定一落地，本次 note 即为「已判定」，触发源会改取「下一颗」或回退到
            // 「上一个已判定对象」，后者会把上一颗的键音再触发一次（同名键音被发声池打断后从头重播）。
            if (keySoundPreviewMode != KeySoundPreviewMode.AutoPlayPlus)
                sampleTriggerSource.Play();

            if (breakdown)
            {
                bookkeepingTs = Stopwatch.GetTimestamp();
                routeTs = selectTs = applyTs = bookkeepingTs;
            }

            bool routed = false;
            bool judged = false;

            if (drawableRuleset?.ColumnRoutesInput == true)
            {
                columnRoutedPressTarget = null;

                if (drawableRuleset.JudgementRound is { IsO2Jam: true } round)
                    round.NotifyO2InputAt(time);

                resolvePressRouting(out var precedence);

                if (breakdown)
                    routeTs = Stopwatch.GetTimestamp();

                var entry = LaneController.SelectPressEntry(time, precedence);

                if (breakdown)
                    selectTs = Stopwatch.GetTimestamp();

                if (entry != null)
                {
                    routed = applyRoutedPress(entry.RoutedObject, time, e);
                    judged = entry.IsPressJudged;
                }

                if (breakdown)
                    applyTs = Stopwatch.GetTimestamp();
            }

            if (probe)
            {
                ManiaPressProbe.Capture(this, pressEnterTs, time, routed, judged, pressForceMissScan);

                if (breakdown)
                    ManiaPressProbe.CaptureBreakdown(this, pressEnterTs, bookkeepingTs, routeTs, selectTs, applyTs, routesInput, entriesBefore, routed, judged);

                reportInputQueueCountOnce();
            }

            return false;
        }

        /// <summary>
        /// 探针只在本列首次按键时读一次非位置输入队列长度，交给帧级 stall 探针做基线；
        /// 读它会触发一次全树重建，所以不能每次按键都做。取不到输入管理器时记 -1。
        /// </summary>
        private void reportInputQueueCountOnce()
        {
            if (inputQueueCountReported)
                return;

            inputQueueCountReported = true;
            var containingInputManager = GetContainingInputManager();
            EzFrameStallDiagnostics.ReportInputQueueCount(containingInputManager == null ? -1 : containingInputManager.NonPositionalInputQueue.Count);
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action != Action.Value)
                return;

            if (drawableRuleset?.ColumnRoutesInput != true)
                return;

            var activeHold = LaneController.ActiveHold;

            if (activeHold == null || !activeHold.IsHolding.Value)
                return;

            var round = drawableRuleset.JudgementRound;

            if (round != null)
                ManiaEzDrawableJudgement.TryColumnHoldTailRelease(activeHold, Time.Current, round);

            LaneController.SetActiveHold(null);
        }

        private void resolvePressRouting(out EzEnumJudgePrecedence precedence)
        {
            var round = drawableRuleset?.JudgementRound;

            if (round != null)
            {
                precedence = round.JudgePrecedence;
                LaneController.SetPoorEnabled(round.PoorEnabled);
                ensureLaneConfigured(round);
                return;
            }

            precedence = judgePrecedence;
        }

        private void ensureLaneConfigured(ManiaJudgementRound round)
        {
            var hitMode = round.Environment.ManiaHitMode;

            if (configuredMissCollectionHitMode == hitMode)
                return;

            configuredMissCollectionHitMode = hitMode;
            double overallDifficulty = drawableRuleset?.Beatmap.Difficulty.OverallDifficulty ?? 5;
            LaneController.ConfigureMissCollection(hitMode, overallDifficulty);
            pressHistoryRetentionMs = computePressHistoryRetentionMs(hitMode, overallDifficulty);
        }

        private static double computePressHistoryRetentionMs(EzEnumHitMode hitMode, double overallDifficulty)
        {
            var helper = new HitModeHelper(hitMode) { OverallDifficulty = overallDifficulty };
            double early = helper.WindowFor(HitResult.Miss, true);
            double late = helper.WindowFor(HitResult.Miss, false);

            if (HitModeHelper.IsBMSHitMode(hitMode))
                BmsHitModeJudgement.ExpandMissCollectionWindows(helper, 1, ref early, ref late);

            // Keep enough history for nearest-press miss offset (several miss windows + margin).
            // Avoid the old 120s floor which retained ~2 minutes of presses per column at high KPS.
            return Math.Max(15_000, (early + late) * 8 + 5_000);
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            // Extend input coverage to the gaps close to this column.
            var spacingInflation = new MarginPadding { Left = leftColumnSpacing, Right = rightColumnSpacing };
            return DrawRectangle.Inflate(spacingInflation).Contains(ToLocalSpace(screenSpacePos));
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
