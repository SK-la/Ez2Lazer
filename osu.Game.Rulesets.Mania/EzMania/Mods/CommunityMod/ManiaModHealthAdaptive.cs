// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod
{
    /// <summary>
    /// 根据玩家血量动态调整速度
    /// </summary>
    public class ManiaModHealthAdaptive : Mod, IApplicableToRate, IApplicableToBeatmap, IApplicableToScoreProcessor, IApplicableToHealthProcessor, IApplicableToDrawableHitObject, IUpdatableByPlayfield
    {
        public override string Name => "Health Adaptive";

        public override string Acronym => "HA";

        public override LocalisableString Description => "Adapt track speed based on your health over time.";

        public override ModType Type => ModType.CommunityMod;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Accuracy Threshold", (HealthThreshold.Value * 100).ToString("0.00") + "%");
                yield return ("Initial Rate", InitialRate.Value.ToString("0.00") + "x");
                yield return ("Min Rate", MinAllowableRate.Value.ToString("0.00") + "x");
                yield return ("Max Rate", MaxAllowableRate.Value.ToString("0.00") + "x");
                yield return ("Increase every", IncreaseInterval.Value.ToString("0.0") + "s by " + IncreaseAmount.Value.ToString("0.000") + "x");
                yield return ("Decrease every", DecreaseInterval.Value.ToString("0.0") + "s by " + DecreaseAmount.Value.ToString("0.000") + "x");
                yield return ("Reset on Recovery", ResetToInitialOnRecovery.Value ? "On" : "Off");
                yield return ("Reset on Drop", ResetToInitialOnDrop.Value ? "On" : "Off");
                yield return ("Adjust Pitch", AdjustPitch.Value ? "On" : "Off");
            }
        }

        public static bool InSong = false;
        public static double StartTime;
        public static double EndTime;
        public static List<BreakPeriod> BreakPeriods = new List<BreakPeriod>();
        private BreakPeriod[] breakPeriodsArray = Array.Empty<BreakPeriod>();

        public override Type[] IncompatibleMods => new[] { typeof(ModRateAdjust), typeof(ModTimeRamp), typeof(ModAutoplay) };

        [SettingSource("Initial rate", "The starting speed of the track.", SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> InitialRate { get; } = new BindableDouble(1)
        {
            MinValue = 0.5,
            MaxValue = 2,
            Precision = 0.01
        };

        [SettingSource("Adjust pitch", "Should pitch be adjusted with speed")]
        public BindableBool AdjustPitch { get; } = new BindableBool(true);

        [SettingSource("Min allowable rate", "Minimum allowed track rate.", SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> MinAllowableRate { get; } = new BindableDouble(0.7)
        {
            MinValue = 0.1,
            MaxValue = 1.0,
            Precision = 0.01
        };

        [SettingSource("Max allowable rate", "Maximum allowed track rate.", SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> MaxAllowableRate { get; } = new BindableDouble(1.3)
        {
            MinValue = 1.0,
            MaxValue = 2.5,
            Precision = 0.01
        };

        // Health threshold (A)
        [SettingSource("Health threshold", "If current health is above this value the rate will increase over time.", SettingControlType = typeof(SettingsPercentageSlider<double>))]
        public BindableNumber<double> HealthThreshold { get; } = new BindableDouble(0.55)
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Precision = 0.01
        };

        // Increase every n seconds (n)
        [SettingSource("Increase interval (s)", "Seconds between rate increases when health is above threshold.")]
        public BindableNumber<double> IncreaseInterval { get; } = new BindableDouble(7.5)
        {
            MinValue = 0.1,
            MaxValue = 20,
            Precision = 0.1
        };

        // Increase amount (x)
        [SettingSource("Increase amount", "Amount to increase rate by each interval when health is above threshold.", SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> IncreaseAmount { get; } = new BindableDouble(0.01)
        {
            MinValue = 0.005,
            MaxValue = 0.1,
            Precision = 0.005
        };

        // Decrease every m seconds (m)
        [SettingSource("Decrease interval (s)", "Seconds between rate decreases when health is below threshold.")]
        public BindableNumber<double> DecreaseInterval { get; } = new BindableDouble(1.5)
        {
            MinValue = 0.1,
            MaxValue = 20,
            Precision = 0.1
        };

        // Decrease amount (y)
        [SettingSource("Decrease amount", "Amount to decrease rate by each interval when health is below threshold.", SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> DecreaseAmount { get; } = new BindableDouble(0.01)
        {
            MinValue = 0.005,
            MaxValue = 0.1,
            Precision = 0.005
        };

        // When enabled, upon health recovering from below threshold to >= threshold,
        // immediately reset the track rate to the current InitialRate value.
        [SettingSource("Reset to initial on recovery", "When health rises from below the threshold to meet or exceed it, immediately reset speed to initial rate.")]
        public BindableBool ResetToInitialOnRecovery { get; } = new BindableBool(false);

        [SettingSource("Reset to initial on drop", "When health falls from at/above the threshold to below it, smoothly return speed to initial rate.")]
        public BindableBool ResetToInitialOnDrop { get; } = new BindableBool(true);

        public BindableNumber<double> SpeedChange { get; } = new BindableDouble(1)
        {
            MinValue = 0.4,
            MaxValue = 2.5,
        };

        private readonly RateAdjustModHelper rateAdjustHelper;

        private double targetRate = 1d;

        // timers track time since last change for increase/decrease
        private double increaseTimer;
        private double decreaseTimer;

        private readonly Bindable<double> currentHealth = new BindableDouble();

        // Tracks whether health was previously below the configured threshold to detect recovery transitions.
        private bool wasBelowThreshold;

        private struct Snapshot
        {
            public readonly double TargetRate;
            public readonly double IncreaseTimer;
            public readonly double DecreaseTimer;
            public double Speed;

            public Snapshot(double t, double i, double d)
            {
                TargetRate = t;
                IncreaseTimer = i;
                DecreaseTimer = d;
                Speed = t;
            }
        }

        private readonly Dictionary<HitObject, Snapshot> ratesForRewinding = new Dictionary<HitObject, Snapshot>();

        private bool shouldProcessResult(JudgementResult result) => result.Type.AffectsAccuracy();

        public void ApplyToDrawableHitObject(DrawableHitObject drawable)
        {
            drawable.OnNewResult += (_, result) =>
            {
                if (ratesForRewinding.ContainsKey(result.HitObject)) return;
                if (!shouldProcessResult(result)) return;

                ratesForRewinding.Add(result.HitObject, new Snapshot(targetRate, increaseTimer, decreaseTimer) { Speed = SpeedChange.Value });
            };

            drawable.OnRevertResult += (_, result) =>
            {
                if (!ratesForRewinding.TryGetValue(result.HitObject, out Snapshot s)) return;
                if (!shouldProcessResult(result)) return;

                targetRate = s.TargetRate;
                increaseTimer = s.IncreaseTimer;
                decreaseTimer = s.DecreaseTimer;
                SpeedChange.Value = s.Speed;

                ratesForRewinding.Remove(result.HitObject);
            };
        }

        public ManiaModHealthAdaptive()
        {
            rateAdjustHelper = new RateAdjustModHelper(SpeedChange);
            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);

            MinAllowableRate.BindValueChanged(v =>
            {
                SpeedChange.MinValue = v.NewValue;
                if (SpeedChange.Value < v.NewValue) SpeedChange.Value = v.NewValue;
                if (targetRate < v.NewValue) targetRate = v.NewValue;
                if (v.NewValue > MaxAllowableRate.Value) MinAllowableRate.Value = MaxAllowableRate.Value;
            }, true);

            MaxAllowableRate.BindValueChanged(v =>
            {
                SpeedChange.MaxValue = v.NewValue;
                if (SpeedChange.Value > v.NewValue) SpeedChange.Value = v.NewValue;
                if (targetRate > v.NewValue) targetRate = v.NewValue;
                if (v.NewValue < MinAllowableRate.Value) MaxAllowableRate.Value = MinAllowableRate.Value;
            }, true);

            InitialRate.BindValueChanged(val =>
            {
                double clamped = Math.Clamp(val.NewValue, MinAllowableRate.Value, MaxAllowableRate.Value);
                SpeedChange.Value = clamped;
                targetRate = clamped;
            }, true);
        }

        public void ApplyToTrack(IAdjustableAudioComponent track)
        {
            InitialRate.TriggerChange();
            rateAdjustHelper.ApplyToTrack(track);
        }

        public void ApplyToSample(IAdjustableAudioComponent sample)
        {
            sample.AddAdjustment(AdjustableProperty.Frequency, SpeedChange);
        }

        public void Update(Playfield playfield)
        {
            // Cache frequently-read values to avoid repeated property access and calculations.
            double elapsed = playfield.Clock.ElapsedFrameTime;
            double currentTime = playfield.Time.Current;
            double health = currentHealth.Value;
            double threshold = HealthThreshold.Value;
            double increaseIntervalMs = IncreaseInterval.Value * 1000;
            double decreaseIntervalMs = DecreaseInterval.Value * 1000;
            double increaseAmount = IncreaseAmount.Value;
            double decreaseAmount = DecreaseAmount.Value;
            double minRate = SpeedChange.MinValue;
            double maxRate = SpeedChange.MaxValue;

            // Smooth towards target rate
            SpeedChange.Value = Interpolation.DampContinuously(SpeedChange.Value, targetRate, 50, elapsed);

            bool withinSong = currentTime >= StartTime && currentTime <= EndTime;
            bool inBreak = false;

            if (withinSong && breakPeriodsArray.Length > 0)
            {
                for (int i = 0; i < breakPeriodsArray.Length; i++)
                {
                    var br = breakPeriodsArray[i];

                    if (currentTime >= br.StartTime && currentTime <= br.EndTime)
                    {
                        inBreak = true;
                        break;
                    }
                }
            }

            bool shouldAccumulate = withinSong && !inBreak;

            // If health is above threshold -> increase over time
            if (health > threshold)
            {
                if (shouldAccumulate)
                    increaseTimer += elapsed;
                decreaseTimer = 0;

                if (increaseTimer >= increaseIntervalMs)
                {
                    targetRate = Math.Clamp(targetRate + increaseAmount, minRate, maxRate);
                    increaseTimer = 0;
                }
            }
            else if (health < threshold)
            {
                if (shouldAccumulate)
                    decreaseTimer += elapsed;
                increaseTimer = 0;

                if (decreaseTimer >= decreaseIntervalMs)
                {
                    targetRate = Math.Clamp(targetRate - decreaseAmount, minRate, maxRate);
                    decreaseTimer = 0;
                }
            }
            else
            {
                // health exactly equals threshold -> do not change timers
                increaseTimer = 0;
                decreaseTimer = 0;
            }
        }

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
        }

        public void ApplyToHealthProcessor(HealthProcessor healthProcessor)
        {
            currentHealth.BindTo(healthProcessor.Health);

            wasBelowThreshold = healthProcessor.Health.Value < HealthThreshold.Value;

            currentHealth.BindValueChanged(h =>
            {
                bool nowBelow = h.NewValue < HealthThreshold.Value;

                // Detect transition from below threshold to >= threshold
                if (wasBelowThreshold && !nowBelow && ResetToInitialOnRecovery.Value)
                {
                    // reset target rate to initial rate and let Update smooth SpeedChange towards it
                    if (targetRate < InitialRate.Value)
                        targetRate = Math.Clamp(InitialRate.Value, MinAllowableRate.Value, MaxAllowableRate.Value);
                    increaseTimer = 0;
                    decreaseTimer = 0;
                }

                // Detect transition from >= threshold to below threshold (drop)
                if (!wasBelowThreshold && nowBelow && ResetToInitialOnDrop.Value)
                {
                    if (targetRate > InitialRate.Value)
                        targetRate = Math.Clamp(InitialRate.Value, MinAllowableRate.Value, MaxAllowableRate.Value);
                    increaseTimer = 0;
                    decreaseTimer = 0;
                }

                wasBelowThreshold = nowBelow;
            }, true);
        }

        public double ApplyToRate(double time, double rate = 1) => rate * InitialRate.Value;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            try
            {
                StartTime = beatmap.HitObjects.First().StartTime;
                var lastHit = beatmap.HitObjects.OrderBy(obj => obj.GetEndTime()).LastOrDefault();
                EndTime = lastHit?.GetEndTime() ?? StartTime;
                BreakPeriods = beatmap.Breaks.ToList();
                breakPeriodsArray = BreakPeriods.Count > 0 ? BreakPeriods.ToArray() : Array.Empty<BreakPeriod>();
            }
            catch
            {
            }
        }

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy)
        {
            return rank;
        }
    }
}
