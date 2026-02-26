// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.LAsEzExtensions.Mods
{
    public class ModReconcile : Mod, IApplicableToHUD
    {
        public override string Name => "Reconcile";
        public override string Acronym => "RC";
        public override LocalisableString Description => ReconcileStrings.RECONCILE_DESCRIPTION;
        public override ModType Type => ModType.LA_Mod;
        public override double ScoreMultiplier => 1;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;
        public override IconUsage? Icon => FontAwesome.Solid.Handshake;

        [SettingSource(typeof(ReconcileStrings), nameof(ReconcileStrings.RECONCILE_REWIND_ENABLED_LABEL), nameof(ReconcileStrings.RECONCILE_REWIND_ENABLED_DESCRIPTION))]
        public BindableBool RewindEnabled { get; } = new BindableBool(false);

        [SettingSource(typeof(ReconcileStrings), nameof(ReconcileStrings.RECONCILE_ENABLE_MISS_LABEL), nameof(ReconcileStrings.RECONCILE_ENABLE_MISS_DESCRIPTION))]
        public BindableBool EnableMissCondition { get; } = new BindableBool(false);

        [SettingSource(typeof(ReconcileStrings), nameof(ReconcileStrings.RECONCILE_MISS_JUDGEMENT_LABEL), nameof(ReconcileStrings.RECONCILE_MISS_JUDGEMENT_DESCRIPTION), SettingControlType = typeof(SettingsEnumDropdown<HitResult>))]
        public Bindable<HitResult> MissJudgement { get; } = new Bindable<HitResult>(HitResult.Miss);

        [SettingSource(typeof(ReconcileStrings), nameof(ReconcileStrings.RECONCILE_MISS_COUNT_LABEL), nameof(ReconcileStrings.RECONCILE_MISS_COUNT_DESCRIPTION))]
        public BindableNumber<int> MissCountThreshold { get; } = new BindableInt(3)
        {
            MinValue = 1,
            MaxValue = 20,
            Precision = 1
        };

        [SettingSource(typeof(ReconcileStrings), nameof(ReconcileStrings.RECONCILE_ENABLE_ACC_LABEL), nameof(ReconcileStrings.RECONCILE_ENABLE_ACC_DESCRIPTION))]
        public BindableBool EnableAccCondition { get; } = new BindableBool(false);

        [SettingSource(typeof(ReconcileStrings), nameof(ReconcileStrings.RECONCILE_ACC_THRESHOLD_LABEL), nameof(ReconcileStrings.RECONCILE_ACC_THRESHOLD_DESCRIPTION))]
        public BindableNumber<double> AccThresholdPercent { get; } = new BindableDouble(94)
        {
            MinValue = 50,
            MaxValue = 100,
            Precision = 0.1
        };

        [SettingSource(typeof(ReconcileStrings), nameof(ReconcileStrings.RECONCILE_ENABLE_HEALTH_LABEL), nameof(ReconcileStrings.RECONCILE_ENABLE_HEALTH_DESCRIPTION))]
        public BindableBool EnableHealthCondition { get; } = new BindableBool(true);

        [SettingSource(typeof(ReconcileStrings), nameof(ReconcileStrings.RECONCILE_HEALTH_THRESHOLD_LABEL), nameof(ReconcileStrings.RECONCILE_HEALTH_THRESHOLD_DESCRIPTION))]
        public BindableNumber<int> HealthThresholdPercent { get; } = new BindableInt(30)
        {
            MinValue = 10,
            MaxValue = 90,
        };

        public void ApplyToHUD(HUDOverlay overlay)
        {
            overlay.Add(new ReconcileClockProvider(this));
        }
    }

    public sealed partial class ReconcileClockProvider : CompositeDrawable
    {
        private readonly ModReconcile mod;

        public ReconcileClockProvider(ModReconcile mod)
        {
            this.mod = mod;
        }

        [Resolved]
        private GameplayClockContainer gameplayClockContainer { get; set; } = null!;

        [Resolved]
        private Player player { get; set; } = null!;

        [Resolved]
        private ScoreProcessor scoreProcessor { get; set; } = null!;

        [Resolved]
        private HealthProcessor healthProcessor { get; set; } = null!;

        private IBindable<HitResult> missJudgement = null!;
        private IBindable<int> missCountThreshold = null!;
        private IBindable<double> accThresholdPercent = null!;
        private IBindable<int> healthThresholdPercent = null!;
        private IBindable<bool> rewindEnabled = null!;
        private IBindable<bool> enableMissCondition = null!;
        private IBindable<bool> enableAccCondition = null!;
        private IBindable<bool> enableHealthCondition = null!;

        private int currentJudgementCount;
        private double? lastJudgementTargetTime;
        private double? lastAccTargetTime;
        private double? lastHealthTargetTime;
        private double? cooldownUntilTime;
        private bool pauseAfterSeek;

        private const double pause_cooldown_ms = 1000;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            missJudgement = mod.MissJudgement.GetBoundCopy();
            missCountThreshold = mod.MissCountThreshold.GetBoundCopy();
            accThresholdPercent = mod.AccThresholdPercent.GetBoundCopy();
            healthThresholdPercent = mod.HealthThresholdPercent.GetBoundCopy();
            rewindEnabled = mod.RewindEnabled.GetBoundCopy();
            enableMissCondition = mod.EnableMissCondition.GetBoundCopy();
            enableAccCondition = mod.EnableAccCondition.GetBoundCopy();
            enableHealthCondition = mod.EnableHealthCondition.GetBoundCopy();

            scoreProcessor.NewJudgement += onNewJudgement;
            scoreProcessor.JudgementReverted += onJudgementReverted;
            scoreProcessor.OnResetFromReplayFrame += resetCounts;

            missJudgement.BindValueChanged(_ => resetJudgementTracking(), true);
            accThresholdPercent.BindValueChanged(_ => lastAccTargetTime = null);
            player.GameplayState.PlayingState.BindValueChanged(_ =>
            {
                if (player.GameplayState.PlayingState.Value == LocalUserPlayingState.NotPlaying)
                    resetCounts();
            });

            gameplayClockContainer.OnSeek += onSeekCompleted;
        }

        protected override void Update()
        {
            base.Update();

            if (player.GameplayState.HasCompleted)
                return;

            if (player.GameplayState.PlayingState.Value != LocalUserPlayingState.Playing)
                return;

            double currentTime = gameplayClockContainer.CurrentTime;

            if (cooldownUntilTime is { } cooldownUntil && currentTime < cooldownUntil)
                return;

            var triggerState = getTriggerState();

            if (triggerState.ShouldTrigger)
                handleTrigger(currentTime, triggerState);
        }

        private TriggerState getTriggerState()
        {
            bool missTriggered = enableMissCondition.Value && missCountThreshold.Value > 0 && currentJudgementCount >= missCountThreshold.Value;
            bool accTriggered = enableAccCondition.Value && scoreProcessor.Accuracy.Value * 100 < accThresholdPercent.Value;
            bool healthTriggered = enableHealthCondition.Value && healthProcessor.Health.Value * 100 < healthThresholdPercent.Value;

            return new TriggerState(missTriggered, accTriggered, healthTriggered);
        }

        private void handleTrigger(double currentTime, TriggerState triggerState)
        {
            if (rewindEnabled.Value)
            {
                double? targetTime = getRewindTargetTime(triggerState);

                if (targetTime is { } target && target <= currentTime)
                {
                    gameplayClockContainer.Stop();
                    pauseAfterSeek = true;
                    player.Seek(target);
                    return;
                }
            }

            if (player.Pause())
            {
                resetCounts();
                cooldownUntilTime = gameplayClockContainer.CurrentTime + pause_cooldown_ms;
            }
        }

        private void onSeekCompleted()
        {
            if (!pauseAfterSeek)
                return;

            pauseAfterSeek = false;

            if (!player.Pause())
            {
                gameplayClockContainer.Start();
                return;
            }

            resetCounts();
            cooldownUntilTime = gameplayClockContainer.CurrentTime + pause_cooldown_ms;
        }

        private void onNewJudgement(JudgementResult result)
        {
            if (!result.IsFinal)
                return;

            if (result.Type != HitResult.None && result.Type == missJudgement.Value)
            {
                currentJudgementCount++;

                int relaxedCount = getRelaxedMissCount();
                if (currentJudgementCount == relaxedCount)
                    lastJudgementTargetTime = result.TimeAbsolute;
            }

            if (scoreProcessor.Accuracy.Value * 100 >= getRelaxedAccThreshold())
                lastAccTargetTime = result.TimeAbsolute;

            if (healthProcessor.Health.Value * 100 >= getRelaxedHealthThreshold())
                lastHealthTargetTime = result.TimeAbsolute;
        }

        private void onJudgementReverted(JudgementResult result)
        {
            if (!result.IsFinal)
                return;

            if (result.Type != HitResult.None && result.Type == missJudgement.Value)
            {
                currentJudgementCount = currentJudgementCount > 0 ? currentJudgementCount - 1 : 0;

                if (currentJudgementCount < getRelaxedMissCount())
                    lastJudgementTargetTime = null;
            }
        }

        private void resetCounts()
        {
            currentJudgementCount = 0;
        }

        private void resetJudgementTracking()
        {
            currentJudgementCount = 0;
            lastJudgementTargetTime = null;
        }

        private int getRelaxedMissCount()
        {
            int threshold = missCountThreshold.Value;
            return (int)Math.Ceiling(threshold / 3d);
        }

        private double getRelaxedAccThreshold()
        {
            double threshold = accThresholdPercent.Value;
            return threshold + (100 - threshold) / 3d;
        }

        private double getRelaxedHealthThreshold()
        {
            double threshold = healthThresholdPercent.Value;
            return threshold + (100 - threshold) * 0.8d;
        }

        private double? getRewindTargetTime(TriggerState triggerState)
        {
            double? targetTime = null;

            if (triggerState.MissTriggered && lastJudgementTargetTime is { } missTarget)
                targetTime = selectEarlier(targetTime, missTarget);

            if (triggerState.AccTriggered && lastAccTargetTime is { } accTarget)
                targetTime = selectEarlier(targetTime, accTarget);

            if (triggerState.HealthTriggered && lastHealthTargetTime is { } healthTarget)
                targetTime = selectEarlier(targetTime, healthTarget);

            return targetTime;

            static double selectEarlier(double? current, double candidate) => current.HasValue ? Math.Min(current.Value, candidate) : candidate;
        }

        private readonly record struct TriggerState(bool MissTriggered, bool AccTriggered, bool HealthTriggered)
        {
            public bool ShouldTrigger => MissTriggered || AccTriggered || HealthTriggered;
        }
    }

    public static class ReconcileStrings
    {
        public static readonly LocalisableString RECONCILE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "满足条件时暂停，可选回溯到上一个目标位置。",
            "Pause when conditions are met, optionally rewinding to the previous target position.");

        public static readonly LocalisableString RECONCILE_ENABLE_MISS_LABEL = new EzLocalizationManager.EzLocalisableString("启用判定计数", "Enable judgement count");
        public static readonly LocalisableString RECONCILE_ENABLE_MISS_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("当指定判定累计到阈值时触发", "Trigger when the selected judgement reaches the threshold.");
        public static readonly LocalisableString RECONCILE_MISS_JUDGEMENT_LABEL = new EzLocalizationManager.EzLocalisableString("判定类型", "Judgement Type");
        public static readonly LocalisableString RECONCILE_MISS_JUDGEMENT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("选择要计数的判定类型", "Select the judgement to count.");
        public static readonly LocalisableString RECONCILE_MISS_COUNT_LABEL = new EzLocalizationManager.EzLocalisableString("判定计数阈值", "Judgement Count Threshold");
        public static readonly LocalisableString RECONCILE_MISS_COUNT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("达到该数量时触发暂停", "Trigger pause when this count is reached.");
        public static readonly LocalisableString RECONCILE_ENABLE_ACC_LABEL = new EzLocalizationManager.EzLocalisableString("启用Acc条件", "Enable accuracy condition");
        public static readonly LocalisableString RECONCILE_ENABLE_ACC_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("当Acc低于阈值时触发", "Trigger when accuracy falls below the threshold.");
        public static readonly LocalisableString RECONCILE_ACC_THRESHOLD_LABEL = new EzLocalizationManager.EzLocalisableString("Acc阈值(%)", "Accuracy Threshold (%)");
        public static readonly LocalisableString RECONCILE_ACC_THRESHOLD_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("低于此Acc触发暂停", "Trigger pause when accuracy is below this value.");
        public static readonly LocalisableString RECONCILE_ENABLE_HEALTH_LABEL = new EzLocalizationManager.EzLocalisableString("启用血量条件", "Enable health condition");
        public static readonly LocalisableString RECONCILE_ENABLE_HEALTH_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("当血量低于阈值时触发", "Trigger when health falls below the threshold.");
        public static readonly LocalisableString RECONCILE_HEALTH_THRESHOLD_LABEL = new EzLocalizationManager.EzLocalisableString("血量阈值(%)", "Health Threshold (%)");
        public static readonly LocalisableString RECONCILE_HEALTH_THRESHOLD_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("低于此血量触发暂停", "Trigger pause when health is below this value.");

        public static readonly LocalisableString RECONCILE_REWIND_ENABLED_LABEL = new EzLocalizationManager.EzLocalisableString("启用回溯", "Enable rewind");

        public static readonly LocalisableString RECONCILE_REWIND_ENABLED_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "触发后回溯到目标位置再暂停。规则："
            + "\n判定回溯到阈值的2/3处；"
            + "\nAcc回溯到阈值+(100-阈值)/3；"
            + "\n血量回溯到阈值+(100-阈值)*0.8。",
            "Rewind to the target position before pausing. Rules: "
            + "\nJudgement rewinds to 2/3 of the threshold; "
            + "\nAcc rewinds to threshold+(100-threshold)/3; "
            + "\nHealth rewinds to threshold+(100-threshold)*0.8.");
    }
}
