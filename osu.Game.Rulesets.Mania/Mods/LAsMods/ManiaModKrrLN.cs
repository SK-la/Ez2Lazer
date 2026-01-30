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

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrLN_LongLevel_Label), nameof(EzManiaModStrings.KrrLN_LongLevel_Description))]
        public BindableNumber<int> LongLevel { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrLN_ShortLevel_Label), nameof(EzManiaModStrings.KrrLN_ShortLevel_Description))]
        public BindableNumber<int> ShortLevel { get; } = new BindableInt(8)
        {
            MinValue = 0,
            MaxValue = 256,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrLN_ProcessOriginal_Label), nameof(EzManiaModStrings.KrrLN_ProcessOriginal_Description))]
        public BindableBool ProcessOriginalBool { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrLN_LengthThreshold_Label), nameof(EzManiaModStrings.KrrLN_LengthThreshold_Description))]
        public BindableNumber<int> LengthThreshold { get; } = new BindableInt(16)
        {
            MinValue = 0,
            MaxValue = 65,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrLN_LongPercentage_Label), nameof(EzManiaModStrings.KrrLN_LongPercentage_Description))]
        public BindableNumber<int> LongPercentage { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrLN_ShortPercentage_Label), nameof(EzManiaModStrings.KrrLN_ShortPercentage_Description))]
        public BindableNumber<int> ShortPercentage { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrLN_LongLimit_Label), nameof(EzManiaModStrings.KrrLN_LongLimit_Description))]
        public BindableNumber<int> LongLimit { get; } = new BindableInt(10)
        {
            MinValue = 0,
            MaxValue = 10,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrLN_ShortLimit_Label), nameof(EzManiaModStrings.KrrLN_ShortLimit_Description))]
        public BindableNumber<int> ShortLimit { get; } = new BindableInt(10)
        {
            MinValue = 0,
            MaxValue = 10,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrLN_LongRandom_Label), nameof(EzManiaModStrings.KrrLN_LongRandom_Description))]
        public BindableNumber<int> LongRandom { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrLN_ShortRandom_Label), nameof(EzManiaModStrings.KrrLN_ShortRandom_Description))]
        public BindableNumber<int> ShortRandom { get; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrLN_Alignment_Label), nameof(EzManiaModStrings.KrrLN_Alignment_Description))]
        public BindableNumber<int> Alignment { get; } = new BindableInt(5)
        {
            MinValue = 0,
            MaxValue = 8,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrLN_LNAlignment_Label), nameof(EzManiaModStrings.KrrLN_LNAlignment_Description))]
        public BindableNumber<int> LNAlignment { get; } = new BindableInt(6)
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
