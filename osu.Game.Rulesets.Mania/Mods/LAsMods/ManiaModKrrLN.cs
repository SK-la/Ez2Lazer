// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Mods.KrrConversion;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModKrrLN : Mod, IApplicableAfterBeatmapConversion, IHasApplyOrder
    {
        public override string Name => "Krr LN";
        public override string Acronym => "LN";
        public override LocalisableString Description => "[KrrTool] LN Conversion";
        public override double ScoreMultiplier => 1;
        public override ModType Type => ModType.LA_Mod;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;

        [SettingSource("Level", "Transform level")]
        public BindableNumber<int> Level { get; } = new BindableInt(3)
        {
            MinValue = -3,
            MaxValue = 10,
        };

        [SettingSource("Process Original LN", "Skip original LN notes")]
        public BindableBool ProcessOriginalBool { get; } = new BindableBool();

        [SettingSource("Length Threshold", "Border key threshold")]
        public BindableNumber<int> LengthThreshold { get; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 32,
        };

        [SettingSource("Long LN Percentage", "Percentage of long LN conversion")]
        public BindableNumber<int> LongPercentage { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource("Short LN Percentage", "Percentage of short LN conversion")]
        public BindableNumber<int> ShortPercentage { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource("Long LN Limit", "Max long LN per row")]
        public BindableNumber<int> LongLimit { get; } = new BindableInt(4)
        {
            MinValue = 0,
            MaxValue = 32,
        };

        [SettingSource("Short LN Limit", "Max short LN per row")]
        public BindableNumber<int> ShortLimit { get; } = new BindableInt(4)
        {
            MinValue = 0,
            MaxValue = 32,
        };

        [SettingSource("Long Random", "Randomization factor for long LN")]
        public BindableNumber<int> LongRandom { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource("Short Random", "Randomization factor for short LN")]
        public BindableNumber<int> ShortRandom { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource("Alignment", "Enable LN length alignment (1/8..1/1)")]
        public BindableNumber<int> Alignment { get; } = new BindableInt
        {
            MinValue = 0,
            MaxValue = 8,
        };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Seed_Label), nameof(EzModStrings.Seed_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>(114514);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.ApplyOrder_Label), nameof(EzManiaModStrings.ApplyOrder_Description))]
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
                yield return ("Level", $"{Level.Value}");
                yield return ("Long %", $"{LongPercentage.Value}%");
                yield return ("Short %", $"{ShortPercentage.Value}%");
                yield return ("Long Limit", $"{LongLimit.Value}");
                yield return ("Short Limit", $"{ShortLimit.Value}");
                if (Seed.Value is null) yield return ("Seed", "Null");
                else yield return ("Seed", $"Seed {Seed.Value}");
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var options = new KrrLNOptions
            {
                Level = Level.Value,
                ProcessOriginalIsChecked = ProcessOriginalBool.Value,
                LengthThreshold = LengthThreshold.Value,
                LongPercentage = LongPercentage.Value,
                ShortPercentage = ShortPercentage.Value,
                LongLimit = LongLimit.Value,
                ShortLimit = ShortLimit.Value,
                LongRandom = LongRandom.Value,
                ShortRandom = ShortRandom.Value,
                Alignment = Alignment.Value,
                Seed = Seed.Value,
            };

            KrrLNConverter.Transform(maniaBeatmap, options);
        }
    }

    public class KrrLNOptions
    {
        public int Level { get; set; }
        public bool ProcessOriginalIsChecked { get; set; }
        public int LengthThreshold { get; set; } = 4;
        public int LongPercentage { get; set; } = 100;
        public int ShortPercentage { get; set; } = 100;
        public int LongLimit { get; set; } = 4;
        public int ShortLimit { get; set; } = 4;
        public int LongRandom { get; set; } = 50;
        public int ShortRandom { get; set; } = 50;

        /// <summary>
        /// 面尾对齐节拍，0为不启用
        /// </summary>
        public int Alignment { get; set; }

        public int? Seed { get; set; }
    }
}
