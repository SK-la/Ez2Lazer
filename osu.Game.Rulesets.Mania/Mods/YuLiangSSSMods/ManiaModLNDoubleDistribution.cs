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
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModLNDoubleDistribution : Mod, IApplicableAfterBeatmapConversion, IHasSeed
    {
        public override string Name => "LN Double Distribution";

        public override string Acronym => "DD";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => EzManiaModStrings.LNDoubleDistribution_Description;

        public override IconUsage? Icon => FontAwesome.Solid.YinYang;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;
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

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Divide1_Label), nameof(EzManiaModStrings.Divide1_Description), 0)]
        public BindableNumber<int> Divide1 { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Divide2_Label), nameof(EzManiaModStrings.Divide2_Description), 1)]
        public BindableNumber<int> Divide2 { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Mu1_Label), nameof(EzManiaModStrings.Mu1_Description), 2)]
        public BindableNumber<int> Mu1 { get; set; } = new BindableInt(20)
        {
            MinValue = -1,
            MaxValue = 100,
            Precision = 1,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Mu2_Label), nameof(EzManiaModStrings.Mu2_Description), 3)]
        public BindableNumber<int> Mu2 { get; set; } = new BindableInt(70)
        {
            MinValue = -1,
            MaxValue = 100,
            Precision = 1,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.MuRatio_Label), nameof(EzManiaModStrings.MuRatio_Description), 4)]
        public BindableInt Mu1DMu2 { get; set; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 1,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.SigmaInteger_Label), nameof(EzManiaModStrings.SigmaInteger_Description), 5)]
        public BindableInt SigmaInteger { get; set; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.SigmaDecimal_Label), nameof(EzManiaModStrings.SigmaDecimal_Description), 6)]
        public BindableDouble SigmaDouble { get; set; } = new BindableDouble(0.85)
        {
            MinValue = 0.01,
            MaxValue = 0.99,
            Precision = 0.01,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Percentage_Label), nameof(EzManiaModStrings.Percentage_Description))]
        public BindableNumber<int> Percentage { get; set; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 5,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.OriginalLN_Label), nameof(EzManiaModStrings.OriginalLN_Description))]
        public BindableBool OriginalLN { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.ColumnNum_Label), nameof(EzManiaModStrings.ColumnNum_Description))]
        public BindableInt SelectColumn { get; set; } = new BindableInt(10)
        {
            MinValue = 1,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Gap_Label), nameof(EzManiaModStrings.Gap_Description))]
        public BindableInt Gap { get; set; } = new BindableInt(12)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.DurationLimit_Label), nameof(EzManiaModStrings.DurationLimit_Description))]
        public BindableDouble DurationLimit { get; set; } = new BindableDouble(5)
        {
            MinValue = 0,
            MaxValue = 15,
            Precision = 0.5,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.LineSpacing_Label), nameof(EzManiaModStrings.LineSpacing_Description))]
        public BindableInt LineSpacing { get; set; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.InvertLineSpacing_Label), nameof(EzManiaModStrings.InvertLineSpacing_Description))]
        public BindableBool InvertLineSpacing { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Seed_Label), nameof(EzModStrings.Seed_Description), SettingControlType = typeof(SettingsNumberBox))]
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

                originalLNObjects = ManiaModYuModHelper.Transform(rng, Mu1.Value, SigmaDouble.Value + SigmaInteger.Value, Divide1.Value, Percentage.Value, ERROR, OriginalLN.Value, beatmap, newObjects,
                    column, Divide2.Value, Mu2.Value, Mu1DMu2.Value);
            }

            ManiaModYuModHelper.AfterTransform(newObjects, originalLNObjects, maniaBeatmap, rng, OriginalLN.Value, Gap.Value, SelectColumn.Value, DurationLimit.Value, LineSpacing.Value,
                InvertLineSpacing.Value);

            maniaBeatmap.Breaks.Clear();
        }
    }
}
