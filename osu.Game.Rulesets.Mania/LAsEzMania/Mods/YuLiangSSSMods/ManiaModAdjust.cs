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
using osu.Game.LAsEzExtensions.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.LAsEzMania.Mods.YuLiangSSSMods
{
    public partial class ManiaModAdjust : ModRateAdjust,
                                          IApplicableAfterConversion,
                                          IApplicableToDifficulty,
                                          IApplicableToBeatmap,
                                          IManiaRateAdjustmentMod,
                                          IApplicableToDrawableRuleset<ManiaHitObject>,
                                          IApplicableFailOverride,
                                          IApplicableToHUD,
                                          IReadFromConfig,
                                          IApplicableToHealthProcessor,
                                          IApplicableToScoreProcessor,
                                          IHasSeed
    {
        public override string Name => @"Adjust";

        public override LocalisableString Description => AdjustStrings.ADJUST_DESCRIPTION;

        public override string Acronym => "AJ";

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override IconUsage? Icon => FontAwesome.Solid.Atlas;

        public override double ScoreMultiplier => ScoreMultiplierAdjust.Value;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => false;
        public override Type[] IncompatibleMods => new[] { typeof(ModEasy), typeof(ModHardRock), typeof(ModTimeRamp), typeof(ModAdaptiveSpeed), typeof(ModRateAdjust) };

        public BindableDouble OriginalOD = new BindableDouble();

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.SCORE_MULTIPLIER_LABEL))]
        public BindableNumber<double> ScoreMultiplierAdjust { get; } = new BindableDouble(1)
        {
            MinValue = 0,
            MaxValue = 10,
            Precision = 0.01
        };

        public ManiaHitWindows HitWindows { get; set; } = new ManiaHitWindows();

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.HP_DRAIN_LABEL), nameof(AdjustStrings.HP_DRAIN_DESCRIPTION), SettingControlType = typeof(DifficultyAdjustSettingsControl))]
        public DifficultyBindable DrainRate { get; } = new DifficultyBindable(0)
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 10,
            ExtendedMaxValue = 15,
            ReadCurrentFromDifficulty = diff => diff.DrainRate
        };

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.ADJUST_ACCURACY_LABEL), nameof(AdjustStrings.ADJUST_ACCURACY_DESCRIPTION),
            SettingControlType = typeof(DifficultyAdjustSettingsControl))]
        public DifficultyBindable OverallDifficulty { get; } = new DifficultyBindable(0)
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 10,
            ExtendedMaxValue = 15,
            ReadCurrentFromDifficulty = diff => diff.OverallDifficulty
        };

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.RELEASE_LENIENCE_LABEL), nameof(AdjustStrings.RELEASE_LENIENCE_DESCRIPTION))]
        public BindableDouble ReleaseLenience { get; } = new BindableDouble(2)
        {
            MaxValue = 4,
            MinValue = 0.1,
            Precision = 0.1
        };

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.CUSTOM_HP_LABEL))]
        public BindableBool CustomHP { get; } = new BindableBool(false);

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.CUSTOM_OD_LABEL))]
        public BindableBool CustomOD { get; } = new BindableBool(true);

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.CUSTOM_RELEASE_LABEL))]
        public BindableBool CustomRelease { get; } = new BindableBool();

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.EXTENDED_LIMITS_LABEL), nameof(AdjustStrings.EXTENDED_LIMITS_DESCRIPTION))]
        public BindableBool ExtendedLimits { get; } = new BindableBool();

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.ADJUST_CONSTANT_SPEED_LABEL), nameof(AdjustStrings.ADJUST_CONSTANT_SPEED_DESCRIPTION))]
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

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SPEED_CHANGE_LABEL), nameof(EzCommonModStrings.SPEED_CHANGE_DESCRIPTION),
            SettingControlType = typeof(MultiplierSettingsSlider))]
        public override BindableNumber<double> SpeedChange { get; } = new BindableDouble(1)
        {
            MinValue = 0.1,
            MaxValue = 2.5,
            Precision = 0.025
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.ADJUST_PITCH_LABEL), nameof(EzCommonModStrings.ADJUST_PITCH_DESCRIPTION))]
        public virtual BindableBool AdjustPitch { get; } = new BindableBool();

        private readonly RateAdjustModHelper rateAdjustHelper;

        public ManiaModAdjust()
        {
            rateAdjustHelper = new RateAdjustModHelper(SpeedChange);

            foreach (var (_, property) in this.GetOrderedSettingsSourceProperties())
            {
                if (property.GetValue(this) is DifficultyBindable diffAdjustBindable)
                    diffAdjustBindable.ExtendedLimits.BindTo(ExtendedLimits);
            }

            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);

            CustomHitRange.BindValueChanged(_ => updateCustomHitRange());
            PerfectHit.BindValueChanged(_ => updateCustomHitRange());
            GreatHit.BindValueChanged(_ => updateCustomHitRange());
            GoodHit.BindValueChanged(_ => updateCustomHitRange());
            OkHit.BindValueChanged(_ => updateCustomHitRange());
            MehHit.BindValueChanged(_ => updateCustomHitRange());
            MissHit.BindValueChanged(_ => updateCustomHitRange());
        }

        private void updateCustomHitRange()
        {
            if (CustomHitRange.Value)
            {
                HitWindows.ModifyManiaHitRange(new ManiaModifyHitRange(
                    PerfectHit.Value,
                    GreatHit.Value,
                    GoodHit.Value,
                    OkHit.Value,
                    MehHit.Value,
                    MissHit.Value
                ));
            }
            else
            {
                HitWindows.ResetRange();
            }
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

            HitWindows.SetDifficulty(difficulty.OverallDifficulty);

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

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.MIRROR_LABEL), nameof(EzCommonModStrings.MIRROR_DESCRIPTION))]
        public BindableBool Mirror { get; } = new BindableBool();

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.RANDOM_MIRROR_LABEL), nameof(AdjustStrings.RANDOM_MIRROR_DESCRIPTION))]
        public BindableBool RandomMirror { get; } = new BindableBool(true);

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.NO_FAIL_LABEL), nameof(AdjustStrings.NO_FAIL_DESCRIPTION))]
        public BindableBool NoFail { get; } = new BindableBool(true);

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.RESTART_LABEL), nameof(AdjustStrings.RESTART_DESCRIPTION))]
        public BindableBool Restart { get; } = new BindableBool();

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.RANDOM_SELECT_LABEL), nameof(AdjustStrings.RANDOM_SELECT_DESCRIPTION))]
        public BindableBool RandomSelect { get; } = new BindableBool();

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.TRUE_RANDOM_LABEL), nameof(AdjustStrings.TRUE_RANDOM_DESCRIPTION))]
        public BindableBool TrueRandom { get; } = new BindableBool();

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SEED_LABEL), nameof(EzCommonModStrings.SEED_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            if (Test.Value)
            {
                var obj = maniaBeatmap;
                var groups = obj.HitObjects.GroupBy(c => c.Column).OrderBy(c => c.Key);
                // int note = obj.HitObjects.Select(h => h.GetEndTime() != h.StartTime).Count();
                // int note = obj.HitObjects.Count - note;
                foreach (var column in groups) Logger.Log($"Column {column.Key + 1}: {column.Count()} notes", level: LogLevel.Important);
                //Logger.Log($"Test:\nThis beatmap has {obj.HitObjects.Count} HitObjects.\n", level: LogLevel.Important);
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
        }

        protected void TriggerFailure()
        {
            triggerFailureDelegate?.Invoke();
        }

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.CUSTOM_HIT_RANGE_LABEL), nameof(AdjustStrings.CUSTOM_HIT_RANGE_DESCRIPTION))]
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

        public static class AdjustStrings
        {
            public static readonly LocalisableString ADJUST_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("凉雨Mod一卡通", "Set your settings.");

            public static readonly LocalisableString RANDOM_MIRROR_LABEL = new EzLocalizationManager.EzLocalisableString("随机镜像", "Random Mirror");
            public static readonly LocalisableString RANDOM_MIRROR_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("随机决定是否镜像音符", "Random Mirror. Mirror or not mirror notes by random.");
            public static readonly LocalisableString NO_FAIL_LABEL = new EzLocalizationManager.EzLocalisableString("无失败", "No Fail");
            public static readonly LocalisableString NO_FAIL_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("无论如何都不会失败", "No Fail. You can't fail, no matter what.");
            public static readonly LocalisableString RESTART_LABEL = new EzLocalizationManager.EzLocalisableString("失败重启", "Restart on fail");
            public static readonly LocalisableString RESTART_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("失败时自动重启", "Restart on fail. Automatically restarts when failed.");
            public static readonly LocalisableString RANDOM_SELECT_LABEL = new EzLocalizationManager.EzLocalisableString("随机选择", "Random");
            public static readonly LocalisableString RANDOM_SELECT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("随机排列按键", "Random. Shuffle around the keys.");
            public static readonly LocalisableString TRUE_RANDOM_LABEL = new EzLocalizationManager.EzLocalisableString("真随机", "True Random");

            public static readonly LocalisableString TRUE_RANDOM_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
                "随机排列所有音符（使用NoLN（LN转换器等级-3），否则可能会重叠）",
                "True Random. Shuffle all notes(Use NoLN(LN Transformer Level -3), or you will get overlapping notes otherwise).");

            public static readonly LocalisableString BEAT_SPEED_LABEL = new EzLocalizationManager.EzLocalisableString("转换的节拍速度", "Transform Beat Speed");

            public static readonly LocalisableString BEAT_SPEED_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
                "| Index | Beat Length |\n" +
                "|-------|-------------|\n" +
                "| 0        | 1/8 Beat    |\n" +
                "| 1        | 1/4 Beat    |\n" +
                "| 2        | 1/2 Beat    |\n" +
                "| 3        | 3/4 Beat    |\n" +
                "| 4        | 1 Beat      |\n" +
                "| 5        | 2 Beats     |\n" +
                "| 6        | 3 Beats     |\n" +
                "| 7        | 4 Beats     |\n" +
                "| 8        | Free        |"
            );

            public static readonly LocalisableString SCORE_MULTIPLIER_LABEL = new EzLocalizationManager.EzLocalisableString("分数倍数", "Score Multiplier");
            public static readonly LocalisableString HP_DRAIN_LABEL = new EzLocalizationManager.EzLocalisableString("HP消耗", "HP Drain");
            public static readonly LocalisableString HP_DRAIN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("覆盖谱面的HP设置", "Override a beatmap's set HP.");
            public static readonly LocalisableString ADJUST_ACCURACY_LABEL = new EzLocalizationManager.EzLocalisableString("准确度", "Accuracy");
            public static readonly LocalisableString ADJUST_ACCURACY_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("覆盖谱面的OD设置", "Override a beatmap's set OD.");
            public static readonly LocalisableString RELEASE_LENIENCE_LABEL = new EzLocalizationManager.EzLocalisableString("释放宽容度", "Release Lenience");

            public static readonly LocalisableString RELEASE_LENIENCE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
                "调整LN尾部释放窗口宽容度。（Score v2中的尾部默认有1.5倍打击窗口）",
                "Adjust LN tail release window lenience.(Tail in Score v2 has default 1.5x hit window)");

            public static readonly LocalisableString CUSTOM_HP_LABEL = new EzLocalizationManager.EzLocalisableString("自定义HP", "Custom HP");
            public static readonly LocalisableString CUSTOM_OD_LABEL = new EzLocalizationManager.EzLocalisableString("自定义OD", "Custom OD");
            public static readonly LocalisableString CUSTOM_RELEASE_LABEL = new EzLocalizationManager.EzLocalisableString("自定义释放", "Custom Release");
            public static readonly LocalisableString EXTENDED_LIMITS_LABEL = new EzLocalizationManager.EzLocalisableString("扩展限制", "Extended Limits");
            public static readonly LocalisableString EXTENDED_LIMITS_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("调整难度超出合理限制", "Adjust difficulty beyond sane limits.");
            public static readonly LocalisableString ADJUST_CONSTANT_SPEED_LABEL = new EzLocalizationManager.EzLocalisableString("恒定速度", "Constant Speed");
            public static readonly LocalisableString ADJUST_CONSTANT_SPEED_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("不再有棘手的速度变化", "No more tricky speed changes.");

            public static readonly LocalisableString CUSTOM_HIT_RANGE_LABEL = new EzLocalizationManager.EzLocalisableString("自定义打击窗口", "Custom Hit Range");
            public static readonly LocalisableString CUSTOM_HIT_RANGE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("自定义每个判定的打击窗口", "Customize hit windows for each judgement.");
        }
    }
}
