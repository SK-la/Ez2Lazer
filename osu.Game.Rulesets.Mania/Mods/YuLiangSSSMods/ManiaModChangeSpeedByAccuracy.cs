// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModChangeSpeedByAccuracy : Mod, IUpdatableByPlayfield, IApplicableToScoreProcessor, IApplicableToRate
    {
        public override string Name => "Speed & Accuracy";

        public override string Acronym => "SA";

        public override LocalisableString Description => EzManiaModStrings.ChangeSpeedByAccuracy_Description;

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

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.ChangeSpeedAccuracy_Label), nameof(EzManiaModStrings.ChangeSpeedAccuracy_Description))]
        public BindableDouble Accuracy { get; } = new BindableDouble(95)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 0.5,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.MaxSpeed_Label), nameof(EzManiaModStrings.MaxSpeed_Description))]
        public BindableDouble MaxSpeed { get; } = new BindableDouble(1.5)
        {
            MinValue = 1,
            MaxValue = 2,
            Precision = 0.1,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.MinSpeed_Label), nameof(EzManiaModStrings.MinSpeed_Description))]
        public BindableDouble MinSpeed { get; } = new BindableDouble(0.5)
        {
            MinValue = 0.5,
            MaxValue = 1,
            Precision = 0.1,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.AdjustPitch_Label), nameof(EzManiaModStrings.AdjustPitch_Description))]
        public virtual BindableBool AdjustPitch { get; } = new BindableBool();

        public ManiaModChangeSpeedByAccuracy()
        {
            rateAdjustHelper = new RateAdjustModHelper(SpeedChange);
            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);
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
}
