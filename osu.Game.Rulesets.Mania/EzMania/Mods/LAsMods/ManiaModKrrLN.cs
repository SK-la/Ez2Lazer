// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.KrrConversion;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods
{
    public class ManiaModKrrLN : Mod, IApplicableAfterBeatmapConversion, IEzApplyOrder
    {
        public override string Name => "Krr LN Converter";
        public override string Acronym => "KLN";
        public override LocalisableString Description => KrrLNStrings.KRR_LN_DESCRIPTION;
        public override double ScoreMultiplier => 1;
        public override ModType Type => ModType.LA_Mod;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;

        [SettingSource(typeof(KrrLNStrings), nameof(KrrLNStrings.KRR_LN_LONG_LEVEL_LABEL), nameof(KrrLNStrings.KRR_LN_LONG_LEVEL_DESCRIPTION))]
        public BindableNumber<int> LongLevel { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource(typeof(KrrLNStrings), nameof(KrrLNStrings.KRR_LN_SHORT_LEVEL_LABEL), nameof(KrrLNStrings.KRR_LN_SHORT_LEVEL_DESCRIPTION))]
        public BindableNumber<int> ShortLevel { get; } = new BindableInt(8)
        {
            MinValue = 0,
            MaxValue = 256,
        };

        [SettingSource(typeof(KrrLNStrings), nameof(KrrLNStrings.KRR_LN_PROCESS_ORIGINAL_LABEL), nameof(KrrLNStrings.KRR_LN_PROCESS_ORIGINAL_DESCRIPTION))]
        public BindableBool ProcessOriginalBool { get; } = new BindableBool();

        [SettingSource(typeof(KrrLNStrings), nameof(KrrLNStrings.KRR_LN_LENGTH_THRESHOLD_LABEL), nameof(KrrLNStrings.KRR_LN_LENGTH_THRESHOLD_DESCRIPTION))]
        public BindableNumber<int> LengthThreshold { get; } = new BindableInt(16)
        {
            MinValue = 0,
            MaxValue = 65,
        };

        [SettingSource(typeof(KrrLNStrings), nameof(KrrLNStrings.KRR_LN_LONG_PERCENTAGE_LABEL), nameof(KrrLNStrings.KRR_LN_LONG_PERCENTAGE_DESCRIPTION))]
        public BindableNumber<int> LongPercentage { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource(typeof(KrrLNStrings), nameof(KrrLNStrings.KRR_LN_SHORT_PERCENTAGE_LABEL), nameof(KrrLNStrings.KRR_LN_SHORT_PERCENTAGE_DESCRIPTION))]
        public BindableNumber<int> ShortPercentage { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource(typeof(KrrLNStrings), nameof(KrrLNStrings.KRR_LN_LONG_LIMIT_LABEL), nameof(KrrLNStrings.KRR_LN_LONG_LIMIT_DESCRIPTION))]
        public BindableNumber<int> LongLimit { get; } = new BindableInt(10)
        {
            MinValue = 0,
            MaxValue = 10,
        };

        [SettingSource(typeof(KrrLNStrings), nameof(KrrLNStrings.KRR_LN_SHORT_LIMIT_LABEL), nameof(KrrLNStrings.KRR_LN_SHORT_LIMIT_DESCRIPTION))]
        public BindableNumber<int> ShortLimit { get; } = new BindableInt(10)
        {
            MinValue = 0,
            MaxValue = 10,
        };

        [SettingSource(typeof(KrrLNStrings), nameof(KrrLNStrings.KRR_LN_LONG_RANDOM_LABEL), nameof(KrrLNStrings.KRR_LN_LONG_RANDOM_DESCRIPTION))]
        public BindableNumber<int> LongRandom { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource(typeof(KrrLNStrings), nameof(KrrLNStrings.KRR_LN_SHORT_RANDOM_LABEL), nameof(KrrLNStrings.KRR_LN_SHORT_RANDOM_DESCRIPTION))]
        public BindableNumber<int> ShortRandom { get; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource(typeof(KrrLNStrings), nameof(KrrLNStrings.KRR_LN_ALIGNMENT_LABEL), nameof(KrrLNStrings.KRR_LN_ALIGNMENT_DESCRIPTION))]
        public BindableNumber<int> Alignment { get; } = new BindableInt(5)
        {
            MinValue = 0,
            MaxValue = 8,
        };

        [SettingSource(typeof(KrrLNStrings), nameof(KrrLNStrings.KRR_LN_LN_ALIGNMENT_LABEL), nameof(KrrLNStrings.KRR_LN_LN_ALIGNMENT_DESCRIPTION))]
        public BindableNumber<int> LNAlignment { get; } = new BindableInt(6)
        {
            MinValue = 0,
            MaxValue = 8,
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
                yield return ("Long Level", $"{LongLevel.Value}");
                yield return ("Short Level", $"{ShortLevel.Value}");
                yield return ("Long %", $"{LongPercentage.Value}%");
                yield return ("Short %", $"{ShortPercentage.Value}%");
                yield return ("Long Limit", $"{LongLimit.Value}");
                yield return ("Short Limit", $"{ShortLimit.Value}");
                yield return ("Alignment", $"{Alignment.Value}");
                yield return ("LN Alignment", $"{LNAlignment.Value}");

                if (Seed.Value is null)
                    yield return ("Seed", "Null");

                else
                    yield return ("Seed", $"Seed {Seed.Value}");
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var options = new KrrLNOptions
            {
                LongLevel = LongLevel.Value,
                ShortLevel = ShortLevel.Value,
                ProcessOriginalIsChecked = ProcessOriginalBool.Value,
                LengthThreshold = LengthThreshold.Value,
                LongPercentage = LongPercentage.Value,
                ShortPercentage = ShortPercentage.Value,
                LongLimit = LongLimit.Value,
                ShortLimit = ShortLimit.Value,
                LongRandom = LongRandom.Value,
                ShortRandom = ShortRandom.Value,
                Alignment = Alignment.Value,
                LNAlignment = LNAlignment.Value,
                Seed = Seed.Value,
            };

            KrrLNConverter.Transform(maniaBeatmap, options);
        }
    }

    public static class KrrLNStrings
    {
        public static readonly LocalisableString KRR_LN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("[KrrTool] LN转换器", "[KrrTool] LN Converter");
        public static readonly LocalisableString KRR_LN_LONG_LEVEL_LABEL = new EzLocalizationManager.EzLocalisableString("长按等级", "Long Level");
        public static readonly LocalisableString KRR_LN_LONG_LEVEL_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("长按长度强度（0-100）", "Long length level (0-100).");
        public static readonly LocalisableString KRR_LN_SHORT_LEVEL_LABEL = new EzLocalizationManager.EzLocalisableString("短按等级", "Short Level");
        public static readonly LocalisableString KRR_LN_SHORT_LEVEL_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("短按长度强度（0-256）", "Short length level (0-256).");
        public static readonly LocalisableString KRR_LN_PROCESS_ORIGINAL_LABEL = new EzLocalizationManager.EzLocalisableString("处理原始LN", "Process Original LN");
        public static readonly LocalisableString KRR_LN_PROCESS_ORIGINAL_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("关闭时跳过原始LN", "Skip original LN when disabled.");
        public static readonly LocalisableString KRR_LN_LENGTH_THRESHOLD_LABEL = new EzLocalizationManager.EzLocalisableString("长度阈值", "Length Threshold");
        public static readonly LocalisableString KRR_LN_LENGTH_THRESHOLD_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("长短按判定阈值", "Threshold between long/short.");
        public static readonly LocalisableString KRR_LN_LONG_PERCENTAGE_LABEL = new EzLocalizationManager.EzLocalisableString("长按比例", "Long Percentage");
        public static readonly LocalisableString KRR_LN_LONG_PERCENTAGE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("长按转换比例", "Percentage of long conversion.");
        public static readonly LocalisableString KRR_LN_SHORT_PERCENTAGE_LABEL = new EzLocalizationManager.EzLocalisableString("短按比例", "Short Percentage");
        public static readonly LocalisableString KRR_LN_SHORT_PERCENTAGE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("短按转换比例", "Percentage of short conversion.");
        public static readonly LocalisableString KRR_LN_LONG_LIMIT_LABEL = new EzLocalizationManager.EzLocalisableString("长按上限", "Long Limit");
        public static readonly LocalisableString KRR_LN_LONG_LIMIT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("每行长按上限", "Max long notes per row.");
        public static readonly LocalisableString KRR_LN_SHORT_LIMIT_LABEL = new EzLocalizationManager.EzLocalisableString("短按上限", "Short Limit");
        public static readonly LocalisableString KRR_LN_SHORT_LIMIT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("每行短按上限", "Max short notes per row.");
        public static readonly LocalisableString KRR_LN_LONG_RANDOM_LABEL = new EzLocalizationManager.EzLocalisableString("长按随机", "Long Random");
        public static readonly LocalisableString KRR_LN_LONG_RANDOM_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("长按随机强度", "Randomness for long notes.");
        public static readonly LocalisableString KRR_LN_SHORT_RANDOM_LABEL = new EzLocalizationManager.EzLocalisableString("短按随机", "Short Random");
        public static readonly LocalisableString KRR_LN_SHORT_RANDOM_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("短按随机强度", "Randomness for short notes.");
        public static readonly LocalisableString KRR_LN_ALIGNMENT_LABEL = new EzLocalizationManager.EzLocalisableString("对齐", "Alignment");
        public static readonly LocalisableString KRR_LN_ALIGNMENT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("普通音符对齐节拍", "Snap normal notes to beat grid.");
        public static readonly LocalisableString KRR_LN_LN_ALIGNMENT_LABEL = new EzLocalizationManager.EzLocalisableString("LN对齐", "LN Alignment");
        public static readonly LocalisableString KRR_LN_LN_ALIGNMENT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("长按尾部对齐节拍", "Snap hold tails to beat grid.");
    }

    public class KrrLNOptions
    {
        public int LongLevel { get; set; } = 50;
        public int ShortLevel { get; set; } = 8;
        public bool ProcessOriginalIsChecked { get; set; }
        public int LengthThreshold { get; set; } = 16;
        public int LongPercentage { get; set; } = 50;
        public int ShortPercentage { get; set; } = 100;
        public int LongLimit { get; set; } = 10;
        public int ShortLimit { get; set; } = 10;
        public int LongRandom { get; set; } = 50;
        public int ShortRandom { get; set; }

        /// <summary>
        /// 面尾对齐节拍，0为不启用
        /// </summary>
        public int Alignment { get; set; } = 5;

        /// <summary>
        /// LN面尾对齐节拍，0为不启用
        /// </summary>
        public int LNAlignment { get; set; } = 6;

        public int? Seed { get; set; }
    }
}
