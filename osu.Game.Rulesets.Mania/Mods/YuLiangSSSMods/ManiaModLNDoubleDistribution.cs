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
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModLNDoubleDistribution : Mod, IApplicableAfterBeatmapConversion, IHasSeed
    {
        public override string Name => "LN Double Distribution";

        public override string Acronym => "DD";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => "LN Transformer another version.";// "From YuLiangSSS' LN Transformer.";

        public override IconUsage? Icon => FontAwesome.Solid.YinYang;

        public override ModType Type => ModType.CustomMod;

        public override bool Ranked => false;

        public readonly int[] DivideNumber = [2, 4, 8, 3, 6, 9, 5, 7, 12, 16, 48, 35, 64];

        public readonly double ERROR = 2;
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

        [SettingSource("Divide 1", "Use 1/?", 0)]
        public BindableNumber<int> Divide1 { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1,
        };

        [SettingSource("Divide 2", "Use 1/?", 1)]
        public BindableNumber<int> Divide2 { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1,
        };

        [SettingSource("Mu 1", "Mu in distribution (Percentage).", 2)]
        public BindableNumber<int> Mu1 { get; set; } = new BindableInt(20)
        {
            MinValue = -1,
            MaxValue = 100,
            Precision = 1,
        };

        [SettingSource("Mu 2", "Mu in distribution (Percentage).", 3)]
        public BindableNumber<int> Mu2 { get; set; } = new BindableInt(70)
        {
            MinValue = -1,
            MaxValue = 100,
            Precision = 1,
        };

        [SettingSource("Mu 1 / Mu 2", "Percentage", 4)]
        public BindableInt Mu1DMu2 { get; set; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 1,
        };

        [SettingSource("Sigma Integer Part", "Sigma Divisor (not sigma).", 5)]
        public BindableInt SigmaInteger { get; set; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource("Sigma Decimal Part", "Sigma Divisor (not sigma).", 6)]
        public BindableDouble SigmaDouble { get; set; } = new BindableDouble(0.85)
        {
            MinValue = 0.01,
            MaxValue = 0.99,
            Precision = 0.01,
        };

        [SettingSource("Percentage", "LN Content")]
        public BindableNumber<int> Percentage { get; set; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 5,
        };

        [SettingSource("Original LN", "Original LN won't be converted.")]
        public BindableBool OriginalLN { get; set; } = new BindableBool(false);

        [SettingSource("Column Num", "Select the number of column to transform.")]
        public BindableInt SelectColumn { get; set; } = new BindableInt(10)
        {
            MinValue = 1,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource("Gap", "For changing random columns after transforming the gap's number of notes.")]
        public BindableInt Gap { get; set; } = new BindableInt(12)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource("Duration Limit", "The max duration(second) of a LN.(No limit when set to 0)")]
        public BindableDouble DurationLimit { get; set; } = new BindableDouble(5)
        {
            MinValue = 0,
            MaxValue = 15,
            Precision = 0.5,
        };

        [SettingSource("Line Spacing", "Transfrom every line when set to 0.")]
        public BindableInt LineSpacing { get; set; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource("Invert Line Spacing", "Invert the Line Spacing.")]
        public BindableBool InvertLineSpacing { get; set; } = new BindableBool(false);

        [SettingSource("Seed", "Use a custom seed instead of a random one.", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            var newObjects = new List<ManiaHitObject>();
            var originalLNObjects = new List<ManiaHitObject>();
            int keys = maniaBeatmap.TotalColumns;
            var notTransformColumn = new List<int>();

            Random? Rng;
            Seed.Value ??= RNG.Next();
            Rng = new Random((int)Seed.Value);

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                if (notTransformColumn.Contains(column.Key))
                {
                    ManiaModHelper.AddOriginalNoteByColumn(newObjects, column);
                    continue;
                }
                originalLNObjects = ManiaModHelper.Transform(Rng, Mu1.Value, SigmaDouble.Value + SigmaInteger.Value, Divide1.Value, Percentage.Value, ERROR, OriginalLN.Value, beatmap, newObjects, column, Divide2.Value, Mu2.Value, Mu1DMu2.Value);
            }

            ManiaModHelper.AfterTransform(newObjects, originalLNObjects, maniaBeatmap, Rng, OriginalLN.Value, Gap.Value, SelectColumn.Value, DurationLimit.Value, LineSpacing.Value, InvertLineSpacing.Value);

            maniaBeatmap.Breaks.Clear();
        }
    }
}
