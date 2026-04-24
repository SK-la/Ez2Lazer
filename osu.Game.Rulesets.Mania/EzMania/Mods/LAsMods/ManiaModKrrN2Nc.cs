// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.KrrConversion;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods
{
    public class ManiaModKrrN2Nc : Mod, IApplicableAfterBeatmapConversion, IHasSeed, IEzApplyOrder
    {
        public override string Name => "Krr N2N Converter";

        public override string Acronym => "N2N";

        public override LocalisableString Description => KrrN2NcStrings.KRR_N2N_DESCRIPTION;

        public override double ScoreMultiplier => 1;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        [SettingSource(typeof(KrrN2NcStrings), nameof(KrrN2NcStrings.KRR_N2N_TARGET_KEYS_LABEL), nameof(KrrN2NcStrings.KRR_N2N_TARGET_KEYS_DESCRIPTION))]
        public BindableNumber<int> TargetKeys { get; } = new BindableInt(8)
        {
            MinValue = 1,
            MaxValue = 18,
        };

        [SettingSource(typeof(KrrN2NcStrings), nameof(KrrN2NcStrings.KRR_N2N_MAX_KEYS_LABEL), nameof(KrrN2NcStrings.KRR_N2N_MAX_KEYS_DESCRIPTION))]
        public BindableNumber<int> MaxKeys { get; } = new BindableInt(6)
        {
            MinValue = 0,
            MaxValue = 10
        };

        [SettingSource(typeof(KrrN2NcStrings), nameof(KrrN2NcStrings.KRR_N2N_MIN_KEYS_LABEL), nameof(KrrN2NcStrings.KRR_N2N_MIN_KEYS_DESCRIPTION))]
        public BindableNumber<int> MinKeys { get; } = new BindableInt(2)
        {
            MinValue = 0,
            MaxValue = 10
        };

        [SettingSource(typeof(KrrN2NcStrings), nameof(KrrN2NcStrings.KRR_N2N_BEAT_SPEED_LABEL), nameof(KrrN2NcStrings.BEAT_SPEED_DESCRIPTION))]
        public BindableNumber<int> BeatSpeed { get; } = new BindableInt(4)
        {
            MinValue = 0,
            MaxValue = 8
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SEED_LABEL), nameof(EzCommonModStrings.SEED_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>(114514);

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (KrrN2NcStrings.KRR_N2N_TARGET_KEYS_LABEL, $"{TargetKeys.Value}");
                yield return (KrrN2NcStrings.KRR_N2N_MAX_KEYS_LABEL, $"{MaxKeys.Value}");
                yield return (KrrN2NcStrings.KRR_N2N_MIN_KEYS_LABEL, $"{MinKeys.Value}");
                yield return (KrrN2NcStrings.KRR_N2N_BEAT_SPEED_LABEL, $"{BeatSpeed.Value}");

                if (Seed.Value is null)
                    yield return (EzCommonModStrings.SEED_LABEL, new EzLocalizationManager.EzLocalisableString("无", "Null"));
                else
                    yield return (EzCommonModStrings.SEED_LABEL, $"Seed {Seed.Value}");

                yield return (EzCommonModStrings.APPLY_ORDER_LABEL, $"{ApplyOrderIndex.Value}");
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var options = new KrrOptions
            {
                TargetKeys = TargetKeys.Value,
                MaxKeys = MaxKeys.Value,
                MinKeys = MinKeys.Value,
                BeatSpeed = BeatSpeed.Value,
                Seed = Seed.Value
            };

            // 转换器内部负责：先重建对象，再更新列数
            KrrN2NcConverter.Transform(maniaBeatmap, options);

            // 最后更新谱面总列数，避免越界
            try
            {
                maniaBeatmap.Stages.Clear();
                maniaBeatmap.Stages.Add(new StageDefinition(options.TargetKeys));
                maniaBeatmap.Difficulty.CircleSize = options.TargetKeys;
            }
            catch (Exception ex)
            {
                Logger.Log($"[ManiaModKrrN2Nc] Failed to update stages: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
            }
        }
    }

    public static class KrrN2NcStrings
    {
        public static readonly LocalisableString KRR_N2N_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("[KrrTool] N2N 转换器", "[KrrTool] N2N Converter");
        public static readonly LocalisableString KRR_N2N_TARGET_KEYS_LABEL = new EzLocalizationManager.EzLocalisableString("目标键数", "Target Keys");
        public static readonly LocalisableString KRR_N2N_TARGET_KEYS_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("目标键数（用于修改列数）", "Target key count (change columns).");
        public static readonly LocalisableString KRR_N2N_MAX_KEYS_LABEL = new EzLocalizationManager.EzLocalisableString("密度上限", "Density Max");
        public static readonly LocalisableString KRR_N2N_MAX_KEYS_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("每行最大键数", "Max keys per row.");
        public static readonly LocalisableString KRR_N2N_MIN_KEYS_LABEL = new EzLocalizationManager.EzLocalisableString("密度下限", "Density Min");
        public static readonly LocalisableString KRR_N2N_MIN_KEYS_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("每行最小键数", "Min keys per row.");
        public static readonly LocalisableString KRR_N2N_BEAT_SPEED_LABEL = new EzLocalizationManager.EzLocalisableString("转换的节拍速度", "Transform Beat Speed");

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
    }

    public class KrrOptions
    {
        public int TargetKeys { get; set; } = 8;
        public int MaxKeys { get; set; } = 4;
        public int MinKeys { get; set; } = 1;
        public int BeatSpeed { get; set; } = 4;
        public int? Seed { get; set; }
    }
}
