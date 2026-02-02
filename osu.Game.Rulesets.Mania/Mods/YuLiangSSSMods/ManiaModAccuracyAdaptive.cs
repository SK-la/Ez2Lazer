// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Overlays.Settings;
using System.Collections.Generic;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Scoring;
using osu.Framework.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Scoring;
using System.Linq;
using osu.Game.Beatmaps.Timing;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    /// <summary>
    /// 根据 accuracy 改变速度
    /// </summary>
    public class ManiaModAccuracyAdaptive : Mod, IApplicableToRate, IApplicableToBeatmap, IApplicableToScoreProcessor, IApplicableToDrawableHitObject, IUpdatableByPlayfield
    {
        public override string Name => "Accuracy Adaptive";

        public override string Acronym => "AA";

        public override LocalisableString Description => "Adapt track speed based on your accuracy over time.";

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Accuracy Threshold", (AccuracyThreshold.Value * 100).ToString("0.00") + "%");
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

        // Accuracy threshold (A)
        [SettingSource("Accuracy threshold", "If current accuracy is above this value the rate will increase over time.", SettingControlType = typeof(SettingsPercentageSlider<double>))]
        public BindableNumber<double> AccuracyThreshold { get; } = new BindableDouble(0.91)
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Precision = 0.01
        };

        // Increase every n seconds (n)
        [SettingSource("Increase interval (s)", "Seconds between rate increases when accuracy is above threshold.")]
        public BindableNumber<double> IncreaseInterval { get; } = new BindableDouble(8)
        {
            MinValue = 0.1,
            MaxValue = 20,
            Precision = 0.1
        };

        // Increase amount (x)
        [SettingSource("Increase amount", "Amount to increase rate by each interval when accuracy is above threshold.", SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> IncreaseAmount { get; } = new BindableDouble(0.01)
        {
            MinValue = 0.001,
            MaxValue = 0.1,
            Precision = 0.001
        };

        // Decrease every m seconds (m)
        [SettingSource("Decrease interval (s)", "Seconds between rate decreases when accuracy is below threshold.")]
        public BindableNumber<double> DecreaseInterval { get; } = new BindableDouble(1)
        {
            MinValue = 0.1,
            MaxValue = 20,
            Precision = 0.1
        };

        // Decrease amount (y)
        [SettingSource("Decrease amount", "Amount to decrease rate by each interval when accuracy is below threshold.", SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> DecreaseAmount { get; } = new BindableDouble(0.01)
        {
            MinValue = 0.001,
            MaxValue = 0.1,
            Precision = 0.001
        };

        // When enabled, upon accuracy recovering from below threshold to >= threshold,
        // immediately reset the track rate to the current InitialRate value.
        [SettingSource("Reset to initial on recovery", "When accuracy rises from below the threshold to meet or exceed it, immediately reset speed to initial rate.")]
        public BindableBool ResetToInitialOnRecovery { get; } = new BindableBool(true);

        [SettingSource("Reset to initial on drop", "When accuracy falls from at/above the threshold to below it, smoothly return speed to initial rate.")]
        public BindableBool ResetToInitialOnDrop { get; } = new BindableBool(false);

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

        private readonly Bindable<double> currentAccuracy = new BindableDouble();

        // Tracks whether accuracy was previously below the configured threshold to detect recovery transitions.
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

                // snapshot current state for potential rewind
                ratesForRewinding.Add(result.HitObject, new Snapshot(targetRate, increaseTimer, decreaseTimer) { Speed = SpeedChange.Value });
            };

            drawable.OnRevertResult += (_, result) =>
            {
                if (!ratesForRewinding.TryGetValue(result.HitObject, out Snapshot s)) return;
                if (!shouldProcessResult(result)) return;

                // restore snapshot
                targetRate = s.TargetRate;
                increaseTimer = s.IncreaseTimer;
                decreaseTimer = s.DecreaseTimer;
                SpeedChange.Value = s.Speed;

                ratesForRewinding.Remove(result.HitObject);
            };
        }

        public ManiaModAccuracyAdaptive()
        {
            rateAdjustHelper = new RateAdjustModHelper(SpeedChange);
            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);

            // Ensure SpeedChange bounds follow configured min/max and clamp values on changes.
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
            // Cache frequently-read values to reduce per-frame overhead
            double elapsed = playfield.Clock.ElapsedFrameTime;
            double currentTime = playfield.Time.Current;
            double accuracy = currentAccuracy.Value;
            double threshold = AccuracyThreshold.Value;
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
                // manual loop is faster than LINQ Any for per-frame checks
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

            // If accuracy is above threshold -> increase over time
            if (accuracy > threshold)
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
            else if (accuracy < threshold)
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
                // accuracy exactly equals threshold -> do not change timers
                increaseTimer = 0;
                decreaseTimer = 0;
            }
        }

        public double ApplyToRate(double time, double rate = 1) => rate * InitialRate.Value;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            try
            {
                StartTime = beatmap.HitObjects.First().StartTime;
                EndTime = beatmap.HitObjects.OrderBy(obj => obj.GetEndTime()).ToArray().Last().GetEndTime();
                BreakPeriods = beatmap.Breaks.ToList();
                // cache breaks into an array for faster per-frame membership checks
                breakPeriodsArray = BreakPeriods.Count > 0 ? BreakPeriods.ToArray() : Array.Empty<BreakPeriod>();
            }
            catch
            {
            }
        }

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            // Bind to the live accuracy value
            currentAccuracy.BindTo(scoreProcessor.Accuracy);

            // initialize previous state and subscribe to changes to detect recovery transitions
            wasBelowThreshold = scoreProcessor.Accuracy.Value < AccuracyThreshold.Value;

            currentAccuracy.BindValueChanged(a =>
            {
                bool nowBelow = a.NewValue < AccuracyThreshold.Value;

                // Detect transition from below threshold to >= threshold (recovery)
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

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy)
        {
            return rank;
        }
    }
}
