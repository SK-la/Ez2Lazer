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
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
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

namespace osu.Game.Rulesets.Mania.EzMania.Mods.YuLiangSSSMods
{
    public partial class ManiaModAdjust : ModRateAdjust,
                                          IApplicableAfterBeatmapConversion,
                                          IApplicableToDifficulty,
                                          IApplicableToBeatmap,
                                          IApplicableFailOverride,
                                          // IApplicableToHealthProcessor,
                                          // IApplicableToScoreProcessor,
                                          IApplicableToHUD,
                                          IReadFromConfig,
                                          IApplicableToDrawableRuleset<ManiaHitObject>,
                                          IManiaRateAdjustmentMod,
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

        public override string ExtendedIconInformation => "";

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.SCORE_MULTIPLIER_LABEL))]
        public BindableNumber<double> ScoreMultiplierAdjust { get; } = new BindableDouble(1)
        {
            MinValue = 0,
            MaxValue = 10,
            Precision = 0.01
        };

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.HP_OVERRIDE_LABEL), nameof(AdjustStrings.HP_OVERRIDE_DESCRIPTION), SettingControlType = typeof(DifficultyAdjustSettingsControl))]
        public DifficultyBindable DrainRate { get; } = new DifficultyBindable
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 10,
            ExtendedMaxValue = 15,
            ReadCurrentFromDifficulty = diff => diff.DrainRate
        };

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.OD_OVERRIDE_LABEL), nameof(AdjustStrings.OD_OVERRIDE_DESCRIPTION), SettingControlType = typeof(DifficultyAdjustSettingsControl))]
        public DifficultyBindable OverallDifficulty { get; } = new DifficultyBindable
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 10,
            ExtendedMaxValue = 15,
            ReadCurrentFromDifficulty = diff => diff.OverallDifficulty
        };

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.ENABLE_CUSTOM_RELEASE_LABEL))]
        public BindableBool CustomRelease { get; } = new BindableBool();

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.ENABLE_RELEASE_LENIENCE_LABEL), nameof(AdjustStrings.RELEASE_LENIENCE_DESCRIPTION))]
        public BindableDouble ReleaseLenience { get; } = new BindableDouble(2)
        {
            MaxValue = 4,
            MinValue = 1,
            Precision = 0.25
        };

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.ENABLE_NO_SV_LABEL), nameof(AdjustStrings.ENABLE_NO_SV_DESCRIPTION))]
        public BindableBool ConstantSpeed { get; } = new BindableBool(true);

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SPEED_CHANGE_LABEL), nameof(EzCommonModStrings.SPEED_CHANGE_DESCRIPTION), SettingControlType = typeof(MultiplierSettingsSlider))]
        public override BindableNumber<double> SpeedChange { get; } = new BindableDouble(1)
        {
            MinValue = 0.1,
            MaxValue = 2.5,
            Precision = 0.025
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.ADJUST_PITCH_LABEL), nameof(EzCommonModStrings.ADJUST_PITCH_DESCRIPTION))]
        public BindableBool AdjustPitch { get; } = new BindableBool();

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.MIRROR_LABEL), nameof(EzCommonModStrings.MIRROR_DESCRIPTION))]
        public BindableBool Mirror { get; } = new BindableBool();

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.RANDOM_MIRROR_LABEL), nameof(AdjustStrings.RANDOM_MIRROR_DESCRIPTION))]
        public BindableBool RandomMirror { get; } = new BindableBool(true);

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.NO_FAIL_LABEL))]
        public BindableBool NoFail { get; } = new BindableBool(true);

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.RESTART_LABEL))]
        public BindableBool Restart { get; } = new BindableBool();

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.RANDOM_SELECT_LABEL), nameof(AdjustStrings.RANDOM_SELECT_DESCRIPTION))]
        public BindableBool RandomColumn { get; } = new BindableBool();

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.TRUE_RANDOM_LABEL), nameof(AdjustStrings.TRUE_RANDOM_DESCRIPTION))]
        public BindableBool TrueRandom { get; } = new BindableBool();

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SEED_LABEL), nameof(EzCommonModStrings.SEED_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        #region Custom Hit Range

        [SettingSource(typeof(AdjustStrings), nameof(AdjustStrings.ENABLE_CUSTOM_HIT_RANGE_LABEL))]
        public BindableBool EnableCustomHitRange { get; } = new BindableBool();

        [SettingSource("Perfect HitRange")]
        public BindableDouble PerfectHitRange { get; } = new BindableDouble(22.4D)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Great HitRange")]
        public BindableDouble GreatHitRange { get; } = new BindableDouble(64)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Good HitRange")]
        public BindableDouble GoodHitRange { get; } = new BindableDouble(97)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Ok HitRange")]
        public BindableDouble OkHitRange { get; } = new BindableDouble(127)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Meh HitRange")]
        public BindableDouble MehHitRange { get; } = new BindableDouble(151)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Miss HitRange")]
        public BindableDouble MissHitRange { get; } = new BindableDouble(188)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        #endregion

        #region Custom Proportion Score

        [SettingSource("Custom Proportion Score")]
        public BindableBool CustomProportionScore { get; } = new BindableBool();

        [SettingSource("Perfect Score")]
        public BindableInt PerfectScore { get; } = new BindableInt(300)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Great Score")]
        public BindableInt GreatScore { get; } = new BindableInt(300)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Good Score")]
        public BindableInt GoodScore { get; } = new BindableInt(200)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Ok Score")]
        public BindableInt OkScore { get; } = new BindableInt(100)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Meh Score")]
        public BindableInt MehScore { get; } = new BindableInt(50)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Miss Score")]
        public BindableInt MissScore { get; } = new BindableInt(0)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        #endregion

        #if DEBUG
        [SettingSource("Test")]
        public BindableBool Test { get; } = new BindableBool();
        # endif

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!ScoreMultiplierAdjust.IsDefault) yield return (AdjustStrings.SCORE_MULTIPLIER_LABEL, $"{ScoreMultiplierAdjust.Value:N3}");

                if (!DrainRate.IsDefault) yield return (AdjustStrings.HP_OVERRIDE_LABEL, $"{DrainRate.Value:N1}");

                if (!OverallDifficulty.IsDefault && !EnableCustomHitRange.Value) yield return (AdjustStrings.OD_OVERRIDE_LABEL, $"{OverallDifficulty.Value:N1}");

                if (CustomRelease.Value) yield return (AdjustStrings.ENABLE_CUSTOM_RELEASE_LABEL, new EzLocalizationManager.EzLocalisableString("开启", "On"));

                if (CustomRelease.Value) yield return (AdjustStrings.ENABLE_RELEASE_LENIENCE_LABEL, $"{ReleaseLenience.Value:N1}");

                if (!SpeedChange.IsDefault) yield return (EzCommonModStrings.SPEED_CHANGE_LABEL, $"{SpeedChange.Value:N3}");

                if (AdjustPitch.Value) yield return (EzCommonModStrings.ADJUST_PITCH_LABEL, new EzLocalizationManager.EzLocalisableString("开启", "On"));

                if (ConstantSpeed.Value) yield return (AdjustStrings.ENABLE_NO_SV_LABEL, new EzLocalizationManager.EzLocalisableString("开启", "On"));

                if (Mirror.Value) yield return (EzCommonModStrings.MIRROR_LABEL, new EzLocalizationManager.EzLocalisableString("开启", "On"));

                if (RandomMirror.Value) yield return (AdjustStrings.RANDOM_MIRROR_LABEL, new EzLocalizationManager.EzLocalisableString("开启", "On"));

                if (NoFail.Value) yield return (AdjustStrings.NO_FAIL_LABEL, new EzLocalizationManager.EzLocalisableString("开启", "On"));

                if (Restart.Value) yield return (AdjustStrings.RESTART_LABEL, new EzLocalizationManager.EzLocalisableString("开启", "On"));

                if (RandomColumn.Value) yield return (AdjustStrings.RANDOM_SELECT_LABEL, new EzLocalizationManager.EzLocalisableString("开启", "On"));

                if (TrueRandom.Value) yield return (AdjustStrings.TRUE_RANDOM_LABEL, new EzLocalizationManager.EzLocalisableString("开启", "On"));

                if (Seed.Value is not null) yield return (EzCommonModStrings.SEED_LABEL, $"Seed {Seed.Value}");

                if (EnableCustomHitRange.Value)
                {
                    yield return (AdjustStrings.ENABLE_CUSTOM_HIT_RANGE_LABEL, new EzLocalizationManager.EzLocalisableString("开启", "On"));
                    yield return ("Perfect HitRange", $"{PerfectHitRange.Value}ms");
                    yield return ("Great HitRange", $"{GreatHitRange.Value}ms");
                    yield return ("Good HitRange", $"{GoodHitRange.Value}ms");
                    yield return ("Ok HitRange", $"{OkHitRange.Value}ms");
                    yield return ("Meh HitRange", $"{MehHitRange.Value}ms");
                    yield return ("Miss HitRange", $"{MissHitRange.Value}ms");
                }

                if (CustomProportionScore.Value)
                {
                    yield return ("Perfect Score", $"{PerfectScore.Value}");
                    yield return ("Great Score", $"{GreatScore.Value}");
                    yield return ("Good Score", $"{GoodScore.Value}");
                    yield return ("Ok Score", $"{OkScore.Value}");
                    yield return ("Meh Score", $"{MehScore.Value}");
                    yield return ("Miss Score", $"{MissScore.Value}");
                }
            }
        }

        private readonly RateAdjustModHelper rateAdjustHelper;

        public ManiaModAdjust()
        {
            rateAdjustHelper = new RateAdjustModHelper(SpeedChange);

            foreach (var (_, property) in this.GetOrderedSettingsSourceProperties())
            {
                if (property.GetValue(this) is DifficultyBindable diffAdjustBindable)
                    diffAdjustBindable.ExtendedLimits.Value = true;
            }

            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);
        }

        // private double originalOD;

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            // 设置会读取默认值，再对比设定值和原谱数值，如果不同则进行覆写。
            if (DrainRate.Value != null && !Precision.AlmostEquals(DrainRate.Value.Value, difficulty.DrainRate, 0.01f))
                difficulty.DrainRate = DrainRate.Value.Value;

            if (OverallDifficulty.Value != null && !EnableCustomHitRange.Value && !Precision.AlmostEquals(OverallDifficulty.Value.Value, difficulty.OverallDifficulty, 0.01f))
            {
                // originalOD = difficulty.OverallDifficulty;
                difficulty.OverallDifficulty = OverallDifficulty.Value.Value;
            }

            if (CustomRelease.Value)
                TailNote.RELEASE_WINDOW_LENIENCE = ReleaseLenience.Value;
            else
            {
                TailNote.RELEASE_WINDOW_LENIENCE = 1.5;
            }
            // AdjustHoldNote.ReleaseLenience = ReleaseLenience.Value;
            // AdjustTailNote.ReleaseLenience = ReleaseLenience.Value;
            // AdjustDrawableHoldNoteTail.ReleaseLenience = ReleaseLenience.Value;

            if (EnableCustomHitRange.Value)
            {
                ManiaHitWindows.SetModOverride(new ManiaModifyHitRange(
                    PerfectHitRange.Value,
                    GreatHitRange.Value,
                    GoodHitRange.Value,
                    OkHitRange.Value,
                    MehHitRange.Value,
                    MissHitRange.Value
                ));
            }
            else
            {
                ManiaHitWindows.ClearModOverride();
            }
        }

        public override void ApplyToTrack(IAdjustableAudioComponent track)
        {
            rateAdjustHelper.ApplyToTrack(track);
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
            var maniaRuleset = (DrawableManiaRuleset)drawableRuleset;

            if (ConstantSpeed.Value) maniaRuleset.VisualisationMethod = ScrollVisualisationMethod.Constant;

            // if (CustomRelease.Value)
            // {
            //     foreach (var stage in maniaRuleset.Playfield.Stages)
            //     {
            //         foreach (var column in stage.Columns) column.RegisterPool<AdjustTailNote, AdjustDrawableHoldNoteTail>(10, 50);
            //     }
            // }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            #if DEBUG
            if (Test.Value)
            {
                var obj = maniaBeatmap;
                var groups = obj.HitObjects.GroupBy(c => c.Column).OrderBy(c => c.Key);
                // int note = obj.HitObjects.Select(h => h.GetEndTime() != h.StartTime).Count();
                // int note = obj.HitObjects.Count - note;
                foreach (var column in groups)
                    Logger.Log($"Column {column.Key + 1}: {column.Count()} notes", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                //Logger.Log($"Test:\nThis beatmap has {obj.HitObjects.Count} HitObjects.\n", level: LogLevel.Important);
            }
            # endif

            if (RandomColumn.Value)
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

        #region NoFail

        private readonly Bindable<bool> showHealthBar = new Bindable<bool>();

        public bool PerformFail() => !NoFail.Value;

        public bool RestartOnFail => !NoFail.Value && Restart.Value;

        public void ReadFromConfig(OsuConfigManager config)
        {
            if (!NoFail.Value) return;

            config.BindWith(OsuSetting.ShowHealthDisplayWhenCantFail, showHealthBar);
        }

        public void ApplyToHUD(HUDOverlay overlay)
        {
            if (!NoFail.Value) return;

            overlay.ShowHealthBar.BindTo(showHealthBar);
        }

        #endregion

        // private readonly BindableInt combo = new BindableInt();

        // private readonly BindableDouble accuracy = new BindableDouble();

        // private Action? triggerFailureDelegate;

        // public void ApplyToHealthProcessor(HealthProcessor healthProcessor)
        // {
        //     triggerFailureDelegate = healthProcessor.TriggerFailure;
        // }

        // public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        // {
        //     combo.UnbindAll();
        //     accuracy.UnbindAll();
        //     combo.BindTo(scoreProcessor.Combo);
        //     accuracy.BindTo(scoreProcessor.Accuracy);
        // }

        // public ScoreRank AdjustRank(ScoreRank rank, double accuracy)
        // {
        //     return rank;
        // }

        // public void ApplyToBeatmapAfterConversion(IBeatmap beatmap)
        // {
        //     var maniaBeatmap = (ManiaBeatmap)beatmap;
        //
        //     if (CustomRelease.Value)
        //     {
        //         var hitObjects = maniaBeatmap.HitObjects.Select(obj =>
        //         {
        //             if (obj is HoldNote hold)
        //                 return new AdjustHoldNote(hold);
        //
        //             return obj;
        //         }).ToList();
        //
        //         maniaBeatmap.HitObjects = hitObjects;
        //     }
        // }

        public override void ResetSettingsToDefaults()
        {
            base.ResetSettingsToDefaults();
            ManiaHitWindows.ClearModOverride();
            TailNote.RELEASE_WINDOW_LENIENCE = 1.5;
        }

        // public partial class AdjustDrawableHoldNoteTail : DrawableHoldNoteTail
        // {
        //     public static double ReleaseLenience;
        //
        //     protected override void CheckForResult(bool userTriggered, double timeOffset)
        //     {
        //         base.CheckForResult(userTriggered, timeOffset * TailNote.RELEASE_WINDOW_LENIENCE / ReleaseLenience);
        //     }
        // }

        // private class AdjustTailNote : TailNote
        // {
        //     public override double MaximumJudgementOffset => base.MaximumJudgementOffset / RELEASE_WINDOW_LENIENCE * ReleaseLenience;
        //     public static double ReleaseLenience;
        // }

        // private class AdjustHoldNote : HoldNote
        // {
        //     public override double MaximumJudgementOffset => base.MaximumJudgementOffset / TailNote.RELEASE_WINDOW_LENIENCE * ReleaseLenience;
        //     public static double ReleaseLenience;
        //
        //     public AdjustHoldNote(HoldNote hold)
        //     {
        //         StartTime = hold.StartTime;
        //         Duration = hold.Duration;
        //         Column = hold.Column;
        //         NodeSamples = hold.NodeSamples;
        //     }
        //
        //     protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        //     {
        //         AddNested(Head = new HeadNote
        //         {
        //             StartTime = StartTime,
        //             Column = Column,
        //             Samples = GetNodeSamples(0)
        //         });
        //
        //         AddNested(Tail = new AdjustTailNote
        //         {
        //             StartTime = EndTime,
        //             Column = Column,
        //             Samples = GetNodeSamples(NodeSamples?.Count - 1 ?? 1)
        //         });
        //
        //         AddNested(Body = new HoldNoteBody
        //         {
        //             StartTime = StartTime,
        //             Column = Column
        //         });
        //     }
        // }

        public static class AdjustStrings
        {
            public static readonly LocalisableString ADJUST_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("凉雨Mod一卡通", "LiangYu Mod — all-in-one mod pack (LiangYu Mod All-in-One)");

            public static readonly LocalisableString RANDOM_MIRROR_LABEL = new EzLocalizationManager.EzLocalisableString("随机镜像", "Random Mirror");
            public static readonly LocalisableString RANDOM_MIRROR_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("随机决定是否镜像音符", "Random Mirror. Mirror or not mirror notes by random.");
            public static readonly LocalisableString NO_FAIL_LABEL = new EzLocalizationManager.EzLocalisableString("不会失败", "No Fail");
            public static readonly LocalisableString RESTART_LABEL = new EzLocalizationManager.EzLocalisableString("失败后重启", "Restart on fail");
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

            public static readonly LocalisableString ENABLE_CUSTOM_HP_LABEL = new EzLocalizationManager.EzLocalisableString("启用自定义HP", "Enable Custom HP");
            public static readonly LocalisableString HP_OVERRIDE_LABEL = new EzLocalizationManager.EzLocalisableString("HP 覆盖", "HP Drain Override");
            public static readonly LocalisableString HP_OVERRIDE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("修改谱面的掉血速率", "Override a beatmap's HP drain rate.");

            public static readonly LocalisableString ENABLE_CUSTOM_OD_LABEL = new EzLocalizationManager.EzLocalisableString("启用自定义OD", "Enable Custom OD");
            public static readonly LocalisableString OD_OVERRIDE_LABEL = new EzLocalizationManager.EzLocalisableString("OD 覆盖", "OD Override");
            public static readonly LocalisableString OD_OVERRIDE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("修改谱面的判定严格程度", "Override a beatmap's overall difficulty.");

            public static readonly LocalisableString ENABLE_CUSTOM_RELEASE_LABEL = new EzLocalizationManager.EzLocalisableString("启用自定义LN释放", "Enable Custom LN Release");
            public static readonly LocalisableString ENABLE_RELEASE_LENIENCE_LABEL = new EzLocalizationManager.EzLocalisableString("调整 LN 尾部的判定宽容度", "Custom LN Release Lenience");

            public static readonly LocalisableString RELEASE_LENIENCE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
                "数值越大，判定越轻松。Lazer默认为1.5x。",
                "The larger the value, the more lenient the judgement. Lazer's default is 1.5x.");

            public static readonly LocalisableString EXTENDED_LIMITS_LABEL = new EzLocalizationManager.EzLocalisableString("扩展限制", "Extended Limits");
            public static readonly LocalisableString EXTENDED_LIMITS_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("调整难度超出合理限制", "Adjust difficulty beyond sane limits.");

            public static readonly LocalisableString ENABLE_NO_SV_LABEL = new EzLocalizationManager.EzLocalisableString("恒定滚动速度, 无SV", "Constant Scrolling Speed, No SV");

            public static readonly LocalisableString ENABLE_NO_SV_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
                "游戏中途不会出现滚动速度变化",
                "No scrolling speed changes will occur during gameplay.");

            public static readonly LocalisableString ENABLE_CUSTOM_HIT_RANGE_LABEL = new EzLocalizationManager.EzLocalisableString("启用自定义判定区间", "Enable Custom Hit Range");
        }
    }
}
