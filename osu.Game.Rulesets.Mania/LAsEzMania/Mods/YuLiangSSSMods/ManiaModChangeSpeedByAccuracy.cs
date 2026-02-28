// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Localization;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.LAsEzMania.Mods.YuLiangSSSMods
{
    public class ManiaModChangeSpeedByAccuracy : Mod, IUpdatableByPlayfield, IApplicableToScoreProcessor, IApplicableToRate
    {
        public override string Name => "Speed & Accuracy";

        public override string Acronym => "SA";

        public override LocalisableString Description => ChangeSpeedByAccuracyStrings.CHANGE_SPEED_BY_ACCURACY_DESCRIPTION;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override IconUsage? Icon => FontAwesome.Solid.ChartLine;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;

        public override Type[] IncompatibleMods => new[] { typeof(ModTimeRamp) };

        private readonly BindableDouble accuracy = new BindableDouble();

        private readonly RateAdjustModHelper rateAdjustHelper;

        public BindableNumber<double> SpeedChange { get; } = new BindableDouble(1)
        {
            Precision = 0.01
        };

        private double targetSpeed = 1;

        [SettingSource(typeof(ChangeSpeedByAccuracyStrings), nameof(ChangeSpeedByAccuracyStrings.CHANGE_SPEED_ACCURACY_LABEL), nameof(ChangeSpeedByAccuracyStrings.CHANGE_SPEED_ACCURACY_DESCRIPTION))]
        public BindableDouble Accuracy { get; } = new BindableDouble(95)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 0.5,
        };

        [SettingSource(typeof(ChangeSpeedByAccuracyStrings), nameof(ChangeSpeedByAccuracyStrings.MAX_SPEED_LABEL), nameof(ChangeSpeedByAccuracyStrings.MAX_SPEED_DESCRIPTION))]
        public BindableDouble MaxSpeed { get; } = new BindableDouble(1.5)
        {
            MinValue = 1,
            MaxValue = 2,
            Precision = 0.1,
        };

        [SettingSource(typeof(ChangeSpeedByAccuracyStrings), nameof(ChangeSpeedByAccuracyStrings.MIN_SPEED_LABEL), nameof(ChangeSpeedByAccuracyStrings.MIN_SPEED_DESCRIPTION))]
        public BindableDouble MinSpeed { get; } = new BindableDouble(0.5)
        {
            MinValue = 0.5,
            MaxValue = 1,
            Precision = 0.1,
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.ADJUST_PITCH_LABEL), nameof(EzCommonModStrings.ADJUST_PITCH_DESCRIPTION))]
        public virtual BindableBool AdjustPitch { get; } = new BindableBool();

        public ManiaModChangeSpeedByAccuracy()
        {
            rateAdjustHelper = new RateAdjustModHelper(SpeedChange);
            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);
        }

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Target Accuracy", $"{Accuracy.Value}%");
                yield return ("Max Speed", $"{MaxSpeed.Value:0.##}x");
                yield return ("Min Speed", $"{MinSpeed.Value:0.##}x");

                if (AdjustPitch.Value) yield return ("Adjust Pitch", "On");

                yield return ("Speed Change", $"{SpeedChange.Value:0.###}x");
            }
        }

        public void Update(Playfield playfield)
        {
            UpdateTargetSpeed();
            SpeedChange.Value = Interpolation.DampContinuously(SpeedChange.Value, targetSpeed, 40, playfield.Clock.ElapsedFrameTime);
        }

        public void UpdateTargetSpeed()
        {
            double currentAccuracy = accuracy.Value;

            double accuracyDifference = currentAccuracy - Accuracy.Value;

            if (accuracyDifference > 0)
            {
                targetSpeed = Math.Min(MaxSpeed.Value, targetSpeed + accuracyDifference * 0.01);
            }
            else
            {
                targetSpeed = Math.Max(MinSpeed.Value, targetSpeed - Math.Abs(accuracyDifference) * 0.01);
            }

            SpeedChange.Value = targetSpeed;
        }

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            accuracy.UnbindAll();
            accuracy.BindTo(scoreProcessor.Accuracy);
        }

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy) => rank;

        public double ApplyToRate(double time, double rate = 1) => rate * SpeedChange.Value;

        public void ApplyToTrack(IAdjustableAudioComponent track)
        {
            rateAdjustHelper.ApplyToTrack(track);
        }

        public void ApplyToSample(IAdjustableAudioComponent sample)
        {
            sample.AddAdjustment(AdjustableProperty.Frequency, SpeedChange);
        }
    }

    public static class ChangeSpeedByAccuracyStrings
    {
        public static readonly LocalisableString CHANGE_SPEED_BY_ACCURACY_DESCRIPTION =
            new EzLocalizationManager.EzLocalisableString("根据准确度调整游戏速度", "Adapt the speed of the game based on the accuracy.");

        public static readonly LocalisableString CHANGE_SPEED_ACCURACY_LABEL = new EzLocalizationManager.EzLocalisableString("准确度", "Accuracy");
        public static readonly LocalisableString CHANGE_SPEED_ACCURACY_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("应用速度变化的准确度", "Accuracy. Accuracy for speed change to be applied.");
        public static readonly LocalisableString MAX_SPEED_LABEL = new EzLocalizationManager.EzLocalisableString("最大速度", "Max Speed");
        public static readonly LocalisableString MAX_SPEED_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("最大速度", "Max Speed");
        public static readonly LocalisableString MIN_SPEED_LABEL = new EzLocalizationManager.EzLocalisableString("最小速度", "Min Speed");
        public static readonly LocalisableString MIN_SPEED_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("最小速度", "Min Speed");
    }
}
