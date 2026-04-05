// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Input.Handlers;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.EzMania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania.UI
{
    public partial class DrawableManiaRuleset : DrawableScrollingRuleset<ManiaHitObject>
    {
        /// <summary>
        /// The minimum time range. This occurs at a <see cref="ManiaRulesetSetting.ScrollSpeed"/> of 40.
        /// </summary>
        public const double MIN_TIME_RANGE = 290;

        /// <summary>
        /// The maximum time range. This occurs with a <see cref="ManiaRulesetSetting.ScrollSpeed"/> of 1.
        /// </summary>
        public const double MAX_TIME_RANGE = 11485;

        public new ManiaPlayfield Playfield => (ManiaPlayfield)base.Playfield;

        public new ManiaBeatmap Beatmap => (ManiaBeatmap)base.Beatmap;

        public IEnumerable<BarLine> BarLines;

        public override bool RequiresPortraitOrientation => Beatmap.Stages.Count == 1 && mobileLayout.Value == ManiaMobileLayout.Portrait;

        protected override bool RelativeScaleBeatLengths => true;

        protected new ManiaRulesetConfigManager Config => (ManiaRulesetConfigManager)base.Config;

        private readonly Bindable<ManiaScrollingDirection> configDirection = new Bindable<ManiaScrollingDirection>();
        private readonly BindableDouble configScrollSpeed = new BindableDouble();
        private readonly Bindable<ManiaMobileLayout> mobileLayout = new Bindable<ManiaMobileLayout>();
        private readonly Bindable<bool> touchOverlay = new Bindable<bool>();

        private static EzManiaScrollingStyle scrollingStyleStatic = EzManiaScrollingStyle.ScrollTimeStyleFixed;

        public double TargetTimeRange { get; protected set; }

        // Stores the current speed adjustment active in gameplay.
        private readonly Track speedAdjustmentTrack = new TrackVirtual(0);

        private ISkinSource currentSkin = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private readonly Bindable<EzEnumHitMode> hitModeBindable = new Bindable<EzEnumHitMode>();
        private readonly Bindable<EzManiaScrollingStyle> scrollingStyle = new Bindable<EzManiaScrollingStyle>();
        private readonly BindableDouble configBaseMs = new BindableDouble();
        private readonly BindableDouble configTimePerSpeed = new BindableDouble();
        private readonly BindableDouble hitPositonBindable = new BindableDouble();
        private readonly Bindable<bool> globalHitPosition = new Bindable<bool>();
        private readonly Bindable<bool> barLinesBindable = new Bindable<bool>();

        public DrawableManiaRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
            BarLines = new BarLineGenerator<BarLine>(Beatmap).BarLines;

            TimeRange.MinValue = 1;
            TimeRange.MaxValue = MAX_TIME_RANGE;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource source)
        {
            currentSkin = source;
            currentSkin.SourceChanged += onSkinChange;
            skinChanged();

            foreach (var mod in Mods.OfType<IApplicableToTrack>())
                mod.ApplyToTrack(speedAdjustmentTrack);

            bool isForCurrentRuleset = Beatmap.BeatmapInfo.Ruleset.Equals(Ruleset.RulesetInfo);

            foreach (var p in ControlPoints)
            {
                // Mania doesn't care about global velocity
                p.Velocity = 1;
                p.BaseBeatLength *= Beatmap.Difficulty.SliderMultiplier;

                // For non-mania beatmap, speed changes should only happen through timing points
                if (!isForCurrentRuleset)
                    p.EffectPoint = new EffectControlPoint();
            }

            Config.BindWith(ManiaRulesetSetting.ScrollDirection, configDirection);
            configDirection.BindValueChanged(direction => Direction.Value = (ScrollingDirection)direction.NewValue, true);

            Config.BindWith(ManiaRulesetSetting.ScrollBaseSpeed, configBaseMs);
            Config.BindWith(ManiaRulesetSetting.ScrollTimePerSpeed, configTimePerSpeed);
            Config.BindWith(ManiaRulesetSetting.ScrollSpeed, configScrollSpeed);
            configScrollSpeed.BindValueChanged(speed =>
            {
                if (!AllowScrollSpeedAdjustment)
                    return;

                TargetTimeRange = ComputeScrollTime(speed.NewValue, configBaseMs.Value, configTimePerSpeed.Value);
            });

            TimeRange.Value = TargetTimeRange = ComputeScrollTime(configScrollSpeed.Value, configBaseMs.Value, configTimePerSpeed.Value);

            Config.BindWith(ManiaRulesetSetting.ScrollStyle, scrollingStyle);
            scrollingStyle.BindValueChanged(style =>
            {
                scrollingStyleStatic = style.NewValue;
                updateTimeRange();
            }, true);

            Config.BindWith(ManiaRulesetSetting.MobileLayout, mobileLayout);
            mobileLayout.BindValueChanged(_ => updateMobileLayout(), true);

            Config.BindWith(ManiaRulesetSetting.TouchOverlay, touchOverlay);
            touchOverlay.BindValueChanged(_ => updateMobileLayout(), true);

            // hitPositonBindable = ezConfig.GetBindable<double>(Ez2Setting.HitPosition);
            ezConfig.BindWith(Ez2Setting.HitPosition, hitPositonBindable);
            hitPositonBindable.BindValueChanged(_ => skinChanged(), true);

            ezConfig.BindWith(Ez2Setting.GlobalHitPosition, globalHitPosition);
            globalHitPosition.BindValueChanged(_ => skinChanged(), true);

            ezConfig.BindWith(Ez2Setting.ManiaBarLinesBool, barLinesBindable);
            ezConfig.BindWith(Ez2Setting.ManiaHitMode, hitModeBindable);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            hitModeBindable.BindValueChanged(h =>
            {
                O2HitModeExtension.PILL_COUNT.Value = 0;

                if (h.NewValue == EzEnumHitMode.O2Jam)
                {
                    O2HitModeExtension.SetOriginalBPM(Beatmap.BeatmapInfo.BPM);
                    O2HitModeExtension.SetControlPoints(Beatmap.ControlPointInfo);
                    O2HitModeExtension.PillActivated = true;
                }
            }, true);
            barLinesBindable.BindValueChanged(b =>
            {
                if (b.NewValue)
                {
                    BarLines.ForEach(Playfield.Add);
                }
            }, true);

            // 立即触发并在主线程等待预加载完成，尽量在进入游戏前确保纹理上传完毕以避免首局卡顿。
            if (currentSkin.GetType() == typeof(EzStyleProSkin))
            {
                try
                {
                    var factory = Dependencies.Get<EzLocalTextureFactory>();
                    _ = factory.PreloadGameTextures();
                }
                catch (Exception ex)
                {
                    Logger.Log($"[DrawableManiaRuleset] Preload textures failed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                }
            }
        }

        private ManiaTouchInputArea? touchInputArea;

        private void updateMobileLayout()
        {
            if (touchOverlay.Value)
                KeyBindingInputManager.Add(touchInputArea = new ManiaTouchInputArea(this));
            else
            {
                if (touchInputArea != null)
                    KeyBindingInputManager.Remove(touchInputArea, true);

                touchInputArea = null;
            }
        }

        #region LAlt 2档变速

        private const Key accelerated_scroll_speed_modifier_key = Key.LAlt;
        private const int accelerated_scroll_speed_adjustment_amount = 5;

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (!e.Repeat
                && AllowScrollSpeedAdjustment)
            {
                if (matchesAcceleratedScrollSpeedBinding(e, GlobalAction.IncreaseScrollSpeed))
                {
                    AdjustScrollSpeed(accelerated_scroll_speed_adjustment_amount);
                    return true;
                }

                if (matchesAcceleratedScrollSpeedBinding(e, GlobalAction.DecreaseScrollSpeed))
                {
                    AdjustScrollSpeed(-accelerated_scroll_speed_adjustment_amount);
                    return true;
                }
            }

            return base.OnKeyDown(e);
        }

        private bool matchesAcceleratedScrollSpeedBinding(KeyDownEvent e, GlobalAction action)
        {
            if (!e.CurrentState.Keyboard.Keys.IsPressed(accelerated_scroll_speed_modifier_key))
                return false;

            KeyCombination pressedCombination = KeyCombination.FromInputState(e.CurrentState);
            bool matched = false;

            realm.Run(context =>
            {
                var bindings = context.All<RealmKeyBinding>()
                                      .Where(b => b.RulesetName == null && b.ActionInt == (int)action)
                                      .ToList();

                foreach (var binding in bindings)
                {
                    if (binding.KeyCombination.IsPressed(pressedCombination, e.CurrentState, KeyCombinationMatchingMode.Any))
                    {
                        matched = true;
                        break;
                    }
                }
            });

            return matched;
        }

        protected override int GetScrollSpeedAdjustmentAmount(KeyBindingPressEvent<GlobalAction> e) => e.CurrentState.Keyboard.Keys.IsPressed(Key.LAlt) ? 5 : 1;

        #endregion

        protected override void AdjustScrollSpeed(int amount) => configScrollSpeed.Value += amount;

        protected override void Update()
        {
            base.Update();
            updateTimeRange();
        }

        private ScheduledDelegate? pendingSkinChange;
        private float hitPosition;

        private void onSkinChange()
        {
            // schedule required to avoid calls after disposed.
            // note that this has the side-effect of components only performing a skin change when they are alive.
            pendingSkinChange?.Cancel();
            pendingSkinChange = Scheduler.Add(skinChanged);
        }

        private void skinChanged()
        {
            if (globalHitPosition.Value)
                hitPosition = (float)hitPositonBindable.Value;
            else
            {
                hitPosition = currentSkin.GetConfig<ManiaSkinConfigurationLookup, float>(
                                  new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.HitPosition))?.Value
                              ?? (float)hitPositonBindable.Value;
            }

            pendingSkinChange = null;
        }

        private void updateTimeRange()
        {
            const float length_to_default_hit_position = 768 - LegacyManiaSkinConfiguration.DEFAULT_HIT_POSITION;

            skinChanged();
            float lengthToHitPosition = 768 - hitPosition;

            // This scaling factor preserves the scroll speed as the scroll length varies from changes to the hit position.
            float scale = 1.0f;

            switch (scrollingStyle.Value)
            {
                case EzManiaScrollingStyle.ScrollSpeedStyle:
                case EzManiaScrollingStyle.ScrollTimeStyle:
                    // Preserve the scroll speed as the scroll length varies from changes to the hit position.
                    scale = lengthToHitPosition / length_to_default_hit_position;
                    break;

                case EzManiaScrollingStyle.ScrollTimeForRealJudgement:
                    // 直接使用设置的速度作为时间范围，忽略 hit position 的影响
                    scale = 1.0f;
                    break;

                case EzManiaScrollingStyle.ScrollTimeStyleFixed:
                    // Ensure the travel time from the top of the screen to the hit position remains constant.
                    scale = lengthToHitPosition / 768;
                    break;
            }

            TimeRange.Value = TargetTimeRange * speedAdjustmentTrack.AggregateTempo.Value * speedAdjustmentTrack.AggregateFrequency.Value * scale;
        }

        /// <summary>
        /// Computes a scroll time (in milliseconds) from a scroll speed in the range of 1-40.
        /// </summary>
        /// <param name="scrollSpeed">The scroll speed.</param>
        /// <param name="baseSpeed"></param>
        /// <param name="timePerSpeed"></param>
        /// <returns>The scroll time.</returns>
        public static double ComputeScrollTime(double scrollSpeed, double baseSpeed, double timePerSpeed)
        {
            switch (scrollingStyleStatic)
            {
                case EzManiaScrollingStyle.ScrollSpeedStyle:
                    // 线性缩放，scroll speed 1-40 映射到 MAX_TIME_RANGE-MIN_TIME_RANGE
                    double sp = Math.Clamp(scrollSpeed / 10, 1, 40);
                    return MAX_TIME_RANGE / sp;

                default:
                    return baseSpeed - (scrollSpeed - 200) * timePerSpeed;
            }
        }

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => new ManiaPlayfieldAdjustmentContainer(this);

        protected override Playfield CreatePlayfield() => new ManiaPlayfield(Beatmap.Stages);

        public override int Variant => (int)(Beatmap.Stages.Count == 1 ? PlayfieldType.Single : PlayfieldType.Dual) + Beatmap.TotalColumns;

        protected override PassThroughInputManager CreateInputManager() => new ManiaInputManager(Ruleset.RulesetInfo, Variant);

        public override DrawableHitObject<ManiaHitObject>? CreateDrawableRepresentation(ManiaHitObject h)
        {
            if (h is PunishmentHoldNote punishmentHoldNote)
                return new PunishmentDrawableHoldNote(punishmentHoldNote);

            return null;
        }

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay) => new ManiaFramedReplayInputHandler(replay);

        protected override ReplayRecorder CreateReplayRecorder(Score score) => new ManiaReplayRecorder(score);

        protected override ResumeOverlay CreateResumeOverlay() => new DelayedResumeOverlay();

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (currentSkin.IsNotNull())
                currentSkin.SourceChanged -= onSkinChange;
        }
    }
}
