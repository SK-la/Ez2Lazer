// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.LAsEzMania.Mods.YuLiangSSSMods
{
    public class ManiaModLNDoubleDistribution : Mod, IApplicableAfterBeatmapConversion, IHasSeed
    {
        public override string Name => "LN Double Distribution";

        public override string Acronym => "DD";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => LNDoubleDistributionStrings.LN_DOUBLE_DISTRIBUTION_DESCRIPTION;

        public override IconUsage? Icon => FontAwesome.Solid.YinYang;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;
        public readonly int[] DivideNumber = [2, 4, 8, 3, 6, 9, 5, 7, 12, 16, 48, 35, 64];

        public readonly double Error = 2;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Divide 1", $"1/{Divide1.Value}");
                yield return ("Divide 2", $"1/{Divide2.Value}");
                yield return ("Mu 1", $"{Mu1.Value}");
                yield return ("Mu 2", $"{Mu2.Value}");
                yield return ("Mu 1 : Mu 2", $"{Mu1DMu2.Value} : {1 - Mu1DMu2.Value}");
                yield return ("Sigma", $"{SigmaInteger.Value + SigmaDouble.Value}");
                yield return ("Percentage", $"{Percentage.Value}%");

                if (OriginalLN.Value)
                {
                    yield return ("Original LN", "On");
                }

                yield return ("Column Num", $"{SelectColumn.Value}");
                yield return ("Gap", $"{Gap.Value}");

                if (DurationLimit.Value > 0)
                {
                    yield return ("Duration Limit", $"{DurationLimit.Value}s");
                }

                yield return ("Seed", $"{(Seed.Value == null ? "Null" : Seed.Value)}");
            }
        }

        [SettingSource(typeof(LNDoubleDistributionStrings), nameof(LNDoubleDistributionStrings.DIVIDE1_LABEL), nameof(LNDoubleDistributionStrings.DIVIDE1_DESCRIPTION), 0)]
        public BindableNumber<int> Divide1 { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1,
        };

        [SettingSource(typeof(LNDoubleDistributionStrings), nameof(LNDoubleDistributionStrings.DIVIDE2_LABEL), nameof(LNDoubleDistributionStrings.DIVIDE2_DESCRIPTION), 1)]
        public BindableNumber<int> Divide2 { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1,
        };

        [SettingSource(typeof(LNDoubleDistributionStrings), nameof(LNDoubleDistributionStrings.MU1_LABEL), nameof(LNDoubleDistributionStrings.MU1_DESCRIPTION), 2)]
        public BindableNumber<int> Mu1 { get; set; } = new BindableInt(20)
        {
            MinValue = -1,
            MaxValue = 100,
            Precision = 1,
        };

        [SettingSource(typeof(LNDoubleDistributionStrings), nameof(LNDoubleDistributionStrings.MU2_LABEL), nameof(LNDoubleDistributionStrings.MU2_DESCRIPTION), 3)]
        public BindableNumber<int> Mu2 { get; set; } = new BindableInt(70)
        {
            MinValue = -1,
            MaxValue = 100,
            Precision = 1,
        };

        [SettingSource(typeof(LNDoubleDistributionStrings), nameof(LNDoubleDistributionStrings.MU_RATIO_LABEL), nameof(LNDoubleDistributionStrings.MU_RATIO_DESCRIPTION), 4)]
        public BindableInt Mu1DMu2 { get; set; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 1,
        };

        [SettingSource(typeof(LNDoubleDistributionStrings), nameof(LNDoubleDistributionStrings.SIGMA_INTEGER_LABEL), nameof(LNDoubleDistributionStrings.SIGMA_INTEGER_DESCRIPTION), 5)]
        public BindableInt SigmaInteger { get; set; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource(typeof(LNDoubleDistributionStrings), nameof(LNDoubleDistributionStrings.SIGMA_DECIMAL_LABEL), nameof(LNDoubleDistributionStrings.SIGMA_DECIMAL_DESCRIPTION), 6)]
        public BindableDouble SigmaDouble { get; set; } = new BindableDouble(0.85)
        {
            MinValue = 0.01,
            MaxValue = 0.99,
            Precision = 0.01,
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.PERCENTAGE_LABEL), nameof(EzCommonModStrings.PERCENTAGE_DESCRIPTION))]
        public BindableNumber<int> Percentage { get; set; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 5,
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.ORIGINAL_LN_LABEL), nameof(EzCommonModStrings.ORIGINAL_LN_DESCRIPTION))]
        public BindableBool OriginalLN { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.COLUMN_NUM_LABEL), nameof(EzCommonModStrings.COLUMN_NUM_DESCRIPTION))]
        public BindableInt SelectColumn { get; set; } = new BindableInt(10)
        {
            MinValue = 1,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.GAP_LABEL), nameof(EzCommonModStrings.GAP_DESCRIPTION))]
        public BindableInt Gap { get; set; } = new BindableInt(12)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.DURATION_LIMIT_LABEL), nameof(EzCommonModStrings.DURATION_LIMIT_DESCRIPTION))]
        public BindableDouble DurationLimit { get; set; } = new BindableDouble(5)
        {
            MinValue = 0,
            MaxValue = 15,
            Precision = 0.5,
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.LINE_SPACING_LABEL), nameof(EzCommonModStrings.LINE_SPACING_DESCRIPTION))]
        public BindableInt LineSpacing { get; set; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.INVERT_LINE_SPACING_LABEL), nameof(EzCommonModStrings.INVERT_LINE_SPACING_DESCRIPTION))]
        public BindableBool InvertLineSpacing { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SEED_LABEL), nameof(EzCommonModStrings.SEED_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            var newObjects = new List<ManiaHitObject>();
            var originalLNObjects = new List<ManiaHitObject>();
            // int keys = maniaBeatmap.TotalColumns;
            var notTransformColumn = new List<int>();

            Seed.Value ??= RNG.Next();
            var rng = new Random((int)Seed.Value);

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                if (notTransformColumn.Contains(column.Key))
                {
                    ManiaModYuModHelper.AddOriginalNoteByColumn(newObjects, column);
                    continue;
                }

                originalLNObjects = ManiaModYuModHelper.Transform(rng, Mu1.Value, SigmaDouble.Value + SigmaInteger.Value, Divide1.Value, Percentage.Value, Error, OriginalLN.Value, beatmap, newObjects,
                    column, Divide2.Value, Mu2.Value, Mu1DMu2.Value);
            }

            ManiaModYuModHelper.AfterTransform(newObjects, originalLNObjects, maniaBeatmap, rng, OriginalLN.Value, Gap.Value, SelectColumn.Value, DurationLimit.Value, LineSpacing.Value,
                InvertLineSpacing.Value);

            maniaBeatmap.Breaks.Clear();
        }
    }

    public static class LNDoubleDistributionStrings
    {
        public static readonly LocalisableString LN_DOUBLE_DISTRIBUTION_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("LN转换器另一个版本", "LN Transformer another version.");
        public static readonly LocalisableString DIVIDE1_LABEL = new EzLocalizationManager.EzLocalisableString("分割1", "Divide 1");
        public static readonly LocalisableString DIVIDE1_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("使用1/?", "Use 1/?");
        public static readonly LocalisableString DIVIDE2_LABEL = new EzLocalizationManager.EzLocalisableString("分割2", "Divide 2");
        public static readonly LocalisableString DIVIDE2_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("使用1/?", "Use 1/?");
        public static readonly LocalisableString MU1_LABEL = new EzLocalizationManager.EzLocalisableString("μ1", "Mu 1");
        public static readonly LocalisableString MU1_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("分布中的μ（百分比）", "Mu in distribution (Percentage).");
        public static readonly LocalisableString MU2_LABEL = new EzLocalizationManager.EzLocalisableString("μ2", "Mu 2");
        public static readonly LocalisableString MU2_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("分布中的μ（百分比）", "Mu in distribution (Percentage).");
        public static readonly LocalisableString MU_RATIO_LABEL = new EzLocalizationManager.EzLocalisableString("μ1/μ2", "Mu 1 / Mu 2");
        public static readonly LocalisableString MU_RATIO_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("百分比", "Percentage");
        public static readonly LocalisableString SIGMA_INTEGER_LABEL = new EzLocalizationManager.EzLocalisableString("σ整数部分", "Sigma Integer Part");
        public static readonly LocalisableString SIGMA_INTEGER_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("σ除数（不是σ）", "Sigma Divisor (not sigma).");
        public static readonly LocalisableString SIGMA_DECIMAL_LABEL = new EzLocalizationManager.EzLocalisableString("σ小数部分", "Sigma Decimal Part");
        public static readonly LocalisableString SIGMA_DECIMAL_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("σ除数（不是σ）", "Sigma Divisor (not sigma).");
    }
}
