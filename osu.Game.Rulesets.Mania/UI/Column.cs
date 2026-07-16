// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
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

        private readonly List<double> pressTimes = new List<double>();

        private double pressHistoryRetentionMs = 120_000;

        internal IReadOnlyList<double> PressTimes => pressTimes;

        internal void RecordPressTime(double time)
        {
            pressTimes.Add(time);
            trimPressHistory(time);
            ManiaJudgeHotPathTrace.RecordPressTimesCount(pressTimes.Count);
        }

        private void trimPressHistory(double time)
        {
            if (pressHistoryRetentionMs <= 0)
                return;

            double cutoff = time - pressHistoryRetentionMs;
            int removeCount = 0;

            while (removeCount < pressTimes.Count && pressTimes[removeCount] < cutoff)
                removeCount++;

            if (removeCount > 0)
                pressTimes.RemoveRange(0, removeCount);
        }

        [Obsolete("Use PressTimes with zero-alloc ResolveMissStoredOffset overload.")]
        internal List<double> GetPressTimesSnapshot()
        {
            ManiaJudgeHotPathTrace.RecordPressTimesSnapshotAllocation(pressTimes.Count);
            return new List<double>(pressTimes);
        }

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

            RegisterPool<Note, DrawableNote>(10, 50);
            RegisterPool<HoldNote, DrawableHoldNote>(10, 50);
            RegisterPool<HeadNote, DrawableHoldNoteHead>(10, 50);
            RegisterPool<TailNote, DrawableHoldNoteTail>(10, 50);
            RegisterPool<HoldNoteBody, DrawableHoldNoteBody>(10, 50);

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

            HitObjectArea.Explosions.Add(hitExplosionPool.Get(e => e.Apply(result)));
        }

        private bool isHittable(DrawableHitObject drawable, double time, EzEnumJudgePrecedence precedence)
        {
            ManiaJudgeHotPathTrace.RecordIsHittable();

            if (drawable is DrawableHoldNoteTail)
                return hitPolicyHelper.IsHittableWithPrecedence(drawable, time, precedence);

            return LaneController.IsHittable(drawable, time, precedence);
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

            foreach (var entry in LaneController.EnumerateForceMissBefore(hitObject.HitObject.StartTime))
            {
                if (OrderedHitPolicyHelper.IsUserTriggerJudgeableNow(entry.RoutedObject, judgementTime))
                    continue;

                ((DrawableManiaHitObject)entry.RoutedObject).MissForcefully();
            }

            LaneController.NotifyJudged(hitObject);
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action != Action.Value)
                return false;

            ManiaJudgeHotPathTrace.RecordColumnOnPressed();

            InputAudioLatencyTracker.Instance?.RecordColumnPress(Index);

            if (e.Action == Action.Value)
                RecordPressTime(Time.Current);

            bool routed = false;

            if (drawableRuleset?.ColumnRoutesInput == true)
            {
                columnRoutedPressTarget = null;

                if (drawableRuleset.JudgementRound is { IsO2Jam: true } round)
                    round.NotifyO2InputAt(Time.Current);

                resolvePressRouting(out var precedence);

                var entry = LaneController.SelectPressEntry(Time.Current, precedence);

                if (entry != null)
                    routed = applyRoutedPress(entry.RoutedObject, Time.Current, e);
            }

            if (keySoundPreviewMode != KeySoundPreviewMode.AutoPlayPlus)
                sampleTriggerSource.Play();

            return routed;
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
