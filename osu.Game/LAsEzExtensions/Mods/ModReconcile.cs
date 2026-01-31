// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.LAsEzExtensions.Mods
{
    public class ModReconcile : Mod, IApplicableToHUD
    {
        public override string Name => "Reconcile";
        public override string Acronym => "RC";
        public override LocalisableString Description => EzModStrings.Reconcile_Description;
        public override ModType Type => ModType.LA_Mod;
        public override double ScoreMultiplier => 1;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;
        public override IconUsage? Icon => FontAwesome.Solid.Handshake;

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Reconcile_RewindEnabled_Label), nameof(EzModStrings.Reconcile_RewindEnabled_Description))]
        public BindableBool RewindEnabled { get; } = new BindableBool(false);

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Reconcile_EnableMiss_Label), nameof(EzModStrings.Reconcile_EnableMiss_Description))]
        public BindableBool EnableMissCondition { get; } = new BindableBool(false);

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Reconcile_MissJudgement_Label), nameof(EzModStrings.Reconcile_MissJudgement_Description), SettingControlType = typeof(SettingsEnumDropdown<HitResult>))]
        public Bindable<HitResult> MissJudgement { get; } = new Bindable<HitResult>(HitResult.Miss);

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Reconcile_MissCount_Label), nameof(EzModStrings.Reconcile_MissCount_Description))]
        public BindableNumber<int> MissCountThreshold { get; } = new BindableInt(3)
        {
            MinValue = 1,
            MaxValue = 20,
            Precision = 1
        };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Reconcile_EnableAcc_Label), nameof(EzModStrings.Reconcile_EnableAcc_Description))]
        public BindableBool EnableAccCondition { get; } = new BindableBool(false);

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Reconcile_AccThreshold_Label), nameof(EzModStrings.Reconcile_AccThreshold_Description))]
        public BindableNumber<double> AccThresholdPercent { get; } = new BindableDouble(94)
        {
            MinValue = 50,
            MaxValue = 100,
            Precision = 0.1
        };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Reconcile_EnableHealth_Label), nameof(EzModStrings.Reconcile_EnableHealth_Description))]
        public BindableBool EnableHealthCondition { get; } = new BindableBool(true);

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Reconcile_HealthThreshold_Label), nameof(EzModStrings.Reconcile_HealthThreshold_Description))]
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
            return (int)System.Math.Ceiling(threshold / 3d);
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

            static double selectEarlier(double? current, double candidate) => current.HasValue ? System.Math.Min(current.Value, candidate) : candidate;
        }

        private readonly record struct TriggerState(bool MissTriggered, bool AccTriggered, bool HealthTriggered)
        {
            public bool ShouldTrigger => MissTriggered || AccTriggered || HealthTriggered;
        }
    }
}
