// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Mods
{
    public abstract partial class ModDynamicSpeedAdjust : ModRateAdjust, ILinkedDynamicSpeedHUD
    {
        /// <summary>
        /// Full-precision runtime speed used for audio binding and per-frame interpolation.
        /// <see cref="SpeedChange"/> remains the display/settings bindable with <see cref="BindableNumber{T}.Precision"/>.
        /// </summary>
        public BindableDouble GameplaySpeed { get; } = new BindableDouble(1);

        protected RateAdjustModHelper RateAdjustHelper { get; private set; } = null!;

        [SettingSource(typeof(DynamicSpeedHUDStrings), nameof(DynamicSpeedHUDStrings.LINK_SPEED_HUD_LABEL), nameof(DynamicSpeedHUDStrings.LINK_SPEED_HUD_DESCRIPTION))]
        public BindableBool LinkSpeedHUD { get; } = new BindableBool(true);

        [SettingSource(typeof(DynamicSpeedHUDStrings), nameof(DynamicSpeedHUDStrings.SHOW_SPEED_TEXT_LABEL), nameof(DynamicSpeedHUDStrings.SHOW_SPEED_TEXT_DESCRIPTION))]
        public BindableBool ShowSpeedText { get; } = new BindableBool(true);

        [SettingSource(typeof(DynamicSpeedHUDStrings), nameof(DynamicSpeedHUDStrings.SHOW_SPEED_LINE_LABEL), nameof(DynamicSpeedHUDStrings.SHOW_SPEED_LINE_DESCRIPTION))]
        public BindableBool ShowSpeedLine { get; } = new BindableBool(true);

        [SettingSource(typeof(DynamicSpeedAdjustStrings), nameof(DynamicSpeedAdjustStrings.RATE_CHANGE_STEP_LABEL), nameof(DynamicSpeedAdjustStrings.RATE_CHANGE_STEP_DESCRIPTION),
            SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> RateChangeStep { get; } = new BindableDouble(0.002)
        {
            MinValue = 0.001,
            MaxValue = 0.1,
            Precision = 0.001,
        };

        /// <summary>
        /// Lower bound for a single judgement-driven rate multiplier factor.
        /// </summary>
        protected double MinRateChangeFactor => 1 - RateChangeStep.Value;

        /// <summary>
        /// Upper bound for a single judgement-driven rate multiplier factor.
        /// </summary>
        protected double MaxRateChangeFactor => 1 + RateChangeStep.Value;

        /// <summary>
        /// Rate multiplier factor applied when a miss threshold is reached.
        /// </summary>
        protected double MissRateChangeFactor => MinRateChangeFactor;

        public void ApplyToHUD(HUDOverlay overlay) => DynamicSpeedHUDApplicator.Apply(this, overlay);

        /// <summary>
        /// Wire <see cref="GameplaySpeed"/> to audio adjustments and sync display speed from runtime speed.
        /// Call from subclass constructors after <see cref="SpeedChange"/> is initialised.
        /// </summary>
        protected void InitialiseDynamicSpeedAdjust(BindableBool adjustPitch)
        {
            SyncSpeedBoundsFromDisplay();

            RateAdjustHelper = new RateAdjustModHelper(GameplaySpeed);
            RateAdjustHelper.HandleAudioAdjustments(adjustPitch);

            GameplaySpeed.BindValueChanged(v => SpeedChange.Value = v.NewValue, true);
        }

        /// <summary>
        /// Copy min/max from <see cref="SpeedChange"/> to <see cref="GameplaySpeed"/> and keep them aligned on future changes.
        /// </summary>
        protected void SyncSpeedBoundsFromDisplay()
        {
            GameplaySpeed.MinValue = SpeedChange.MinValue;
            GameplaySpeed.MaxValue = SpeedChange.MaxValue;

            SpeedChange.MinValueChanged += _ => GameplaySpeed.MinValue = SpeedChange.MinValue;
            SpeedChange.MaxValueChanged += _ => GameplaySpeed.MaxValue = SpeedChange.MaxValue;
        }

        protected void SetGameplayAndDisplaySpeed(double value)
        {
            GameplaySpeed.Value = value;
        }

        protected void DampGameplaySpeedTowards(double targetRate, double elapsedFrameTime)
        {
            GameplaySpeed.Value = Interpolation.DampContinuously(GameplaySpeed.Value, targetRate, 50, elapsedFrameTime);
        }

        public override void ApplyToSample(IAdjustableAudioComponent sample)
        {
            sample.AddAdjustment(AdjustableProperty.Frequency, GameplaySpeed);
        }
    }
}
