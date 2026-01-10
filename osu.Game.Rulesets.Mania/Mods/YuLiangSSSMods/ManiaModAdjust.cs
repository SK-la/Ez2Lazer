// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public partial class ManiaModAdjust : ModRateAdjust, IApplicableAfterConversion, IApplicableToDifficulty, IApplicableToBeatmap, IManiaRateAdjustmentMod,
                                          IApplicableToDrawableRuleset<ManiaHitObject>, IApplicableFailOverride, IApplicableToHUD, IReadFromConfig, IApplicableToHealthProcessor,
                                          IApplicableToScoreProcessor, IHasSeed //, IUpdatableByPlayfield
    {
        public override string Name => @"Adjust";

        public override LocalisableString Description => @"Set your settings.";

        public override string Acronym => "AJ";

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override IconUsage? Icon => FontAwesome.Solid.Atlas;

        public override double ScoreMultiplier => ScoreMultiplierAdjust.Value;
        //{
        //    get
        //    {
        //        double SpeedValue = SpeedChange.Value;
        //        float? ODValue = OverallDifficulty.Value;
        //        if (ODValue is not null)
        //        {
        //            return SpeedValue * (double)(ODValue / 5 + 1);
        //        }
        //        return SpeedValue;
        //    }
        //}

        public override bool Ranked => false;

        public override Type[] IncompatibleMods => new[] { typeof(ModEasy), typeof(ModHardRock), typeof(ModTimeRamp), typeof(ModAdaptiveSpeed), typeof(ModRateAdjust) };

        public BindableDouble OriginalOD = new BindableDouble();

        [SettingSource("Score Multiplier")]
        public BindableNumber<double> ScoreMultiplierAdjust { get; } = new BindableDouble(1)
        {
            MinValue = 0,
            MaxValue = 10,
            Precision = 0.01
        };

        public ManiaHitWindows HitWindows { get; set; } = new ManiaHitWindows();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.HPDrain_Label), nameof(EzManiaModStrings.HPDrain_Description), SettingControlType = typeof(DifficultyAdjustSettingsControl))]
        public DifficultyBindable DrainRate { get; } = new DifficultyBindable(0)
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 10,
            ExtendedMaxValue = 15,
            ReadCurrentFromDifficulty = diff => diff.DrainRate
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.AdjustAccuracy_Label), nameof(EzManiaModStrings.AdjustAccuracy_Description), SettingControlType = typeof(DifficultyAdjustSettingsControl))]
        public DifficultyBindable OverallDifficulty { get; } = new DifficultyBindable(0)
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 10,
            ExtendedMaxValue = 15,
            ReadCurrentFromDifficulty = diff => diff.OverallDifficulty
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.ReleaseLenience_Label), nameof(EzManiaModStrings.ReleaseLenience_Description))]
        public BindableDouble ReleaseLenience { get; } = new BindableDouble(2)
        {
            MaxValue = 4,
            MinValue = 0.1,
            Precision = 0.1
        };

        [SettingSource("Custom HP")]
        public BindableBool CustomHP { get; } = new BindableBool(false);

        [SettingSource("Custom OD")]
        public BindableBool CustomOD { get; } = new BindableBool(true);

        [SettingSource("Custom Release")]
        public BindableBool CustomRelease { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.ExtendedLimits_Label), nameof(EzManiaModStrings.ExtendedLimits_Description))]
        public BindableBool ExtendedLimits { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.AdjustConstantSpeed_Label), nameof(EzManiaModStrings.AdjustConstantSpeed_Description))]
        public BindableBool ConstantSpeed { get; } = new BindableBool(true);

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!ScoreMultiplierAdjust.IsDefault) yield return ("Score Multiplier", $"{ScoreMultiplierAdjust.Value:N3}");

                if (CustomHP.Value) yield return ("HP", $"{DrainRate.Value:N1}");

                if (CustomOD.Value) yield return ("OD", $"{OverallDifficulty.Value:N1}");

                if (CustomRelease.Value) yield return ("Release Lenience", $"{ReleaseLenience.Value:N1}");

                if (!SpeedChange.IsDefault) yield return ("Speed", $"{SpeedChange.Value:N3}");

                if (AdjustPitch.Value) yield return ("Adjust Pitch", "On");

                if (ConstantSpeed.Value) yield return ("Constant Speed", "On");

                if (Mirror.Value) yield return ("Mirror", "On");

                if (RandomMirror.Value) yield return ("Random Mirror", "On");

                if (NoFail.Value) yield return ("No Fail", "On");

                if (Restart.Value) yield return ("Restart", "On");

                if (RandomSelect.Value) yield return ("Random", "On");

                if (TrueRandom.Value) yield return ("True Random", "On");

                if (Seed.Value is not null) yield return ("Seed", $"Seed {Seed.Value}");

                if (CustomHitRange.Value)
                {
                    yield return ("Perfect Hit", $"{PerfectHit.Value}ms");
                    yield return ("Great Hit", $"{GreatHit.Value}ms");
                    yield return ("Good Hit", $"{GoodHit.Value}ms");
                    yield return ("Ok Hit", $"{OkHit.Value}ms");
                    yield return ("Meh Hit", $"{MehHit.Value}ms");
                    yield return ("Miss Hit", $"{MissHit.Value}ms");
                }

                if (CustomProportionScore.Value)
                {
                    yield return ("Perfect", $"{Perfect.Value}");
                    yield return ("Great", $"{Great.Value}");
                    yield return ("Good", $"{Good.Value}");
                    yield return ("Ok", $"{Ok.Value}");
                    yield return ("Meh", $"{Meh.Value}");
                    yield return ("Miss", $"{Miss.Value}");
                }
            }
        }

        public override string ExtendedIconInformation => "";

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.SpeedChange_Label), nameof(EzManiaModStrings.SpeedChange_Description), SettingControlType = typeof(MultiplierSettingsSlider))]
        public override BindableNumber<double> SpeedChange { get; } = new BindableDouble(1)
        {
            MinValue = 0.1,
            MaxValue = 2.5,
            Precision = 0.025
        };

        public override void ApplyToSample(IAdjustableAudioComponent sample)
        {
            //if (UseBPM.Value && BPM.Value is not null)
            //{
            //    var newBindable = new Bindable<double>
            //    {
            //        Value = (double)BPM.Value / NowBeatmapBPM
            //    };
            //    sample.AddAdjustment(AdjustableProperty.Frequency, newBindable);
            //}
            //else
            {
                base.ApplyToSample(sample);
            }
        }

        public override double ApplyToRate(double time, double rate)
        {
            //if (UseBPM.Value && BPM.Value is not null)
            //{
            //    try
            //    {
            //        return rate * (double)(BPM.Value / NowBeatmapBPM);
            //    }
            //    catch
            //    {
            //        return rate;
            //    }
            //}
            return base.ApplyToRate(time, rate);
        }

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.AdjustPitch_Label), nameof(EzManiaModStrings.AdjustPitch_Description))]
        public virtual BindableBool AdjustPitch { get; } = new BindableBool();

        private readonly RateAdjustModHelper rateAdjustHelper;

        //public double NowBeatmapBPM
        //{
        //    get
        //    {
        //        double result;
        //        if (BeatmapTitleWedge.SelectedWorkingBeatmap is not null)
        //        {
        //            result = BeatmapTitleWedge.SelectedWorkingBeatmap.BeatmapInfo.BPM;
        //        }
        //        else
        //        {
        //            result = 200;
        //        }
        //        if (BPM.Value is not null)
        //        {
        //            try
        //            {
        //                SpeedChange.Value = (double)BPM.Value / result;
        //            }
        //            catch
        //            {
        //                SpeedChange.Value = 1;
        //            }
        //        }
        //        return result;
        //    }
        //}

        //[SettingSource("BPM", "Change the BPM of the beatmap.", SettingControlType = typeof(SettingsNumberBox))]
        //public Bindable<int?> BPM { get; set; } = new Bindable<int?>(200);

        //[SettingSource("Use BPM", "Use BPM instead of speed rate.")]
        //public BindableBool UseBPM { get; set; } = new BindableBool(false);

        public void ReadFromDifficulty(IBeatmapDifficultyInfo difficulty)
        {
        }

        public ManiaModAdjust()
        {
            //if (UseBPM.Value && BPM.Value is not null)
            //{
            //    var newBindable = new BindableNumber<double>
            //    {
            //        Value = (double)BPM.Value / NowBeatmapBPM
            //    };
            //    rateAdjustHelper = new RateAdjustModHelper(newBindable);
            //}
            //else
            {
                rateAdjustHelper = new RateAdjustModHelper(SpeedChange);
            }

            foreach (var (_, property) in this.GetOrderedSettingsSourceProperties())
            {
                if (property.GetValue(this) is DifficultyBindable diffAdjustBindable)
                    diffAdjustBindable.ExtendedLimits.BindTo(ExtendedLimits);
            }

            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);
        }

        /// <summary>
        /// Apply all custom settings to the provided beatmap.
        /// </summary>
        /// <param name="difficulty">The beatmap to have settings applied.</param>
        protected void ApplySettings(BeatmapDifficulty difficulty)
        {
            if (DrainRate.Value != null && CustomHP.Value)
                difficulty.DrainRate = DrainRate.Value.Value;

            if (OverallDifficulty.Value != null && CustomOD.Value && !CustomHitRange.Value)
            {
                OriginalOD.Value = difficulty.OverallDifficulty;
                difficulty.OverallDifficulty = OverallDifficulty.Value.Value;
            }
        }

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            HitWindows.SpeedMultiplier = SpeedChange.Value;

            if (CustomHitRange.Value)
            {
                double[] ranges =
                {
                    PerfectHit.Value,
                    GreatHit.Value,
                    GoodHit.Value,
                    OkHit.Value,
                    MehHit.Value,
                    MissHit.Value,
                };
                HitWindows.SetSpecialDifficultyRange(ranges);
            }
            else
            {
                HitWindows.ResetRange();
                HitWindows.SetDifficulty(difficulty.OverallDifficulty);
            }

            ApplySettings(difficulty);
            AdjustHoldNote.ReleaseLenience = ReleaseLenience.Value;
            AdjustTailNote.ReleaseLenience = ReleaseLenience.Value;
            AdjustDrawableHoldNoteTail.ReleaseLenience = ReleaseLenience.Value;
        }

        public override void ApplyToTrack(IAdjustableAudioComponent track)
        {
            rateAdjustHelper.ApplyToTrack(track);
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
            var maniaRuleset = (DrawableManiaRuleset)drawableRuleset;

            if (ConstantSpeed.Value) maniaRuleset.VisualisationMethod = ScrollVisualisationMethod.Constant;

            if (CustomRelease.Value)
            {
                foreach (var stage in maniaRuleset.Playfield.Stages)
                {
                    foreach (var column in stage.Columns) column.RegisterPool<AdjustTailNote, AdjustDrawableHoldNoteTail>(10, 50);
                }
            }
        }

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Mirror_Label), nameof(EzManiaModStrings.Mirror_Description))]
        public BindableBool Mirror { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.RandomMirror_Label), nameof(EzManiaModStrings.RandomMirror_Description))]
        public BindableBool RandomMirror { get; } = new BindableBool(true);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.NoFail_Label), nameof(EzManiaModStrings.NoFail_Description))]
        public BindableBool NoFail { get; } = new BindableBool(true);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Restart_Label), nameof(EzManiaModStrings.Restart_Description))]
        public BindableBool Restart { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.RandomSelect_Label), nameof(EzManiaModStrings.RandomSelect_Description))]
        public BindableBool RandomSelect { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.TrueRandom_Label), nameof(EzManiaModStrings.TrueRandom_Description))]
        public BindableBool TrueRandom { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Seed_Label), nameof(EzManiaModStrings.Seed_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            if (Test.Value)
            {
                var mobj = maniaBeatmap;
                var groups = mobj.HitObjects.GroupBy(c => c.Column).OrderBy(c => c.Key);
                int hote = mobj.HitObjects.Select(h => h.GetEndTime() != h.StartTime).Count();
                int note = mobj.HitObjects.Count - hote;
                foreach (var column in groups) Logger.Log($"Column {column.Key + 1}: {column.Count()} notes", level: LogLevel.Important);
                //Logger.Log($"Test:\nThis beatmap has {mobj.HitObjects.Count} HitObjects.\n", level: LogLevel.Important);
            }

            if (RandomSelect.Value)
            {
                Seed.Value ??= RNG.Next();
                var rng = new Random((int)Seed.Value);

                int availableColumns = maniaBeatmap.TotalColumns;
                var shuffledColumns = Enumerable.Range(0, availableColumns).OrderBy(_ => rng.Next()).ToList();
                beatmap.HitObjects.OfType<ManiaHitObject>().ForEach(h => h.Column = shuffledColumns[h.Column]);
            }

            if (Mirror.Value)
            {
                int availableColumns = maniaBeatmap.TotalColumns;
                beatmap.HitObjects.OfType<ManiaHitObject>().ForEach(h => h.Column = availableColumns - 1 - h.Column);
            }

            if (RandomMirror.Value)
            {
                Seed.Value ??= RNG.Next();
                var rng = new Random((int)Seed.Value);

                if (rng.Next() % 2 == 0)
                {
                    int availableColumns = maniaBeatmap.TotalColumns;
                    beatmap.HitObjects.OfType<ManiaHitObject>().ForEach(h => h.Column = availableColumns - 1 - h.Column);
                }
            }

            if (TrueRandom.Value)
            {
                Seed.Value ??= RNG.Next();
                var rng = new Random((int)Seed.Value);
                int availableColumns = maniaBeatmap.TotalColumns;

                foreach (var obj in beatmap.HitObjects.OfType<ManiaHitObject>().GroupBy(c => c.StartTime))
                {
                    var columnList = new List<int>();
                    foreach (var hit in obj) columnList.Add(hit.Column);
                    var newColumn = Enumerable.Range(0, availableColumns).SelectRandom(rng, columnList.Count).ToList();
                    int index = 0;

                    foreach (var hit in obj)
                    {
                        hit.Column = newColumn[index];
                        index++;
                    }
                }
            }
        }

        //------Fail Condition------
        private Action? triggerFailureDelegate;

        private readonly Bindable<bool> showHealthBar = new Bindable<bool>();

        public bool PerformFail()
        {
            return !NoFail.Value;
        }

        public bool RestartOnFail
        {
            get
            {
                if (NoFail.Value) return !NoFail.Value;

                return Restart.Value;
            }
        }

        public void ReadFromConfig(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.ShowHealthDisplayWhenCantFail, showHealthBar);
        }

        public void ApplyToHUD(HUDOverlay overlay)
        {
            overlay.ShowHealthBar.BindTo(showHealthBar);
        }

        public void ApplyToHealthProcessor(HealthProcessor healthProcessor)
        {
            triggerFailureDelegate = healthProcessor.TriggerFailure;
            //healthProcessor.FailConditions += FailCondition;
        }

        //protected bool FailCondition(HealthProcessor healthProcessor, JudgementResult result)
        //{
        //    return result.Type.AffectsCombo()
        //       && !result.IsHit;
        //}
        //------Fail Condition------

        protected void TriggerFailure()
        {
            triggerFailureDelegate?.Invoke();
        }

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.CustomHitRange_Label), nameof(EzManiaModStrings.CustomHitRange_Description))]
        public BindableBool CustomHitRange { get; } = new BindableBool();

        [SettingSource("Perfect")]
        public BindableDouble PerfectHit { get; } = new BindableDouble(22.4D)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Great")]
        public BindableDouble GreatHit { get; } = new BindableDouble(64)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Good")]
        public BindableDouble GoodHit { get; } = new BindableDouble(97)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Ok")]
        public BindableDouble OkHit { get; } = new BindableDouble(127)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Meh")]
        public BindableDouble MehHit { get; } = new BindableDouble(151)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Miss")]
        public BindableDouble MissHit { get; } = new BindableDouble(188)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Custom Proportion Score")]
        public BindableBool CustomProportionScore { get; } = new BindableBool();

        [SettingSource("Perfect")]
        public BindableInt Perfect { get; } = new BindableInt(300)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Great")]
        public BindableInt Great { get; } = new BindableInt(300)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Good")]
        public BindableInt Good { get; } = new BindableInt(200)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Ok")]
        public BindableInt Ok { get; } = new BindableInt(100)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Meh")]
        public BindableInt Meh { get; } = new BindableInt(50)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Miss")]
        public BindableInt Miss { get; } = new BindableInt(0)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Test")]
        public BindableBool Test { get; } = new BindableBool();

        private readonly BindableInt combo = new BindableInt();

        private readonly BindableDouble accuracy = new BindableDouble();

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy)
        {
            return rank;
        }

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            // var mania = (ManiaScoreProcessor)scoreProcessor;

            // if (CustomProportionScore.Value)
            // {
            //     mania.HitProportionScore.Perfect = Perfect.Value;
            //     mania.HitProportionScore.Great = Great.Value;
            //     mania.HitProportionScore.Good = Good.Value;
            //     mania.HitProportionScore.Ok = Ok.Value;
            //     mania.HitProportionScore.Meh = Meh.Value;
            //     mania.HitProportionScore.Miss = Miss.Value;
            // }

            combo.UnbindAll();
            accuracy.UnbindAll();
            combo.BindTo(scoreProcessor.Combo);
            accuracy.BindTo(scoreProcessor.Accuracy);
        }

        public override void ResetSettingsToDefaults()
        {
            base.ResetSettingsToDefaults();
            HitWindows.ResetRange();
        }

        public void ApplyToBeatmapAfterConversion(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            if (CustomRelease.Value)
            {
                var hitObjects = maniaBeatmap.HitObjects.Select(obj =>
                {
                    if (obj is HoldNote hold)
                        return new AdjustHoldNote(hold);

                    return obj;
                }).ToList();

                maniaBeatmap.HitObjects = hitObjects;
            }
        }

        public partial class AdjustDrawableHoldNoteTail : DrawableHoldNoteTail
        {
            public static double ReleaseLenience;

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
                base.CheckForResult(userTriggered, timeOffset * TailNote.RELEASE_WINDOW_LENIENCE / ReleaseLenience);
            }
        }

        private class AdjustTailNote : TailNote
        {
            public static double ReleaseLenience;

            public override double MaximumJudgementOffset => base.MaximumJudgementOffset / RELEASE_WINDOW_LENIENCE * ReleaseLenience;
        }

        private class AdjustHoldNote : HoldNote
        {
            public static double ReleaseLenience;

            public AdjustHoldNote(HoldNote hold)
            {
                StartTime = hold.StartTime;
                Duration = hold.Duration;
                Column = hold.Column;
                NodeSamples = hold.NodeSamples;
            }

            protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
            {
                AddNested(Head = new HeadNote
                {
                    StartTime = StartTime,
                    Column = Column,
                    Samples = GetNodeSamples(0)
                });

                AddNested(Tail = new AdjustTailNote
                {
                    StartTime = EndTime,
                    Column = Column,
                    Samples = GetNodeSamples(NodeSamples?.Count - 1 ?? 1)
                });

                AddNested(Body = new HoldNoteBody
                {
                    StartTime = StartTime,
                    Column = Column
                });
            }

            public override double MaximumJudgementOffset => base.MaximumJudgementOffset / TailNote.RELEASE_WINDOW_LENIENCE * ReleaseLenience;
        }
    }
}
