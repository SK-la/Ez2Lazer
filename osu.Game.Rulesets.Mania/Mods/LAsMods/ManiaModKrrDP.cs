using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Mania.Mods.KrrConversion;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModKrrDP : Mod, IApplicableAfterBeatmapConversion, IHasApplyOrder
    {
        public override string Name => "Krr DP";
        public override string Acronym => "DP";
        public override LocalisableString Description => "[KrrTool] Convert to Dual Play mode";
        public override double ScoreMultiplier => 1;
        public override ModType Type => ModType.LA_Mod;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrDP_EnableModifyKeys_Label), nameof(EzManiaModStrings.KrrDP_EnableModifyKeys_Description))]
        public BindableBool EnableModifyKeys { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrDP_TargetKeys_Label), nameof(EzManiaModStrings.KrrDP_TargetKeys_Description))]
        public BindableNumber<int> TargetKeys { get; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 9,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrDP_LeftMirror_Label), nameof(EzManiaModStrings.KrrDP_LeftMirror_Description))]
        public BindableBool LMirror { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrDP_LeftDensity_Label), nameof(EzManiaModStrings.KrrDP_LeftDensity_Description))]
        public BindableBool LDensity { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrDP_LeftRemove_Label), nameof(EzManiaModStrings.KrrDP_LeftRemove_Description))]
        public BindableBool LRemove { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrDP_LeftMax_Label), nameof(EzManiaModStrings.KrrDP_LeftMax_Description))]
        public BindableNumber<int> LMaxKeys { get; set; } = new BindableInt(5)
        {
            MinValue = 1,
            MaxValue = 5,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrDP_LeftMin_Label), nameof(EzManiaModStrings.KrrDP_LeftMin_Description))]
        public BindableNumber<int> LMinKeys { get; set; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 5,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrDP_RightMirror_Label), nameof(EzManiaModStrings.KrrDP_RightMirror_Description))]
        public BindableBool RMirror { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrDP_RightDensity_Label), nameof(EzManiaModStrings.KrrDP_RightDensity_Description))]
        public BindableBool RDensity { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrDP_RightRemove_Label), nameof(EzManiaModStrings.KrrDP_RightRemove_Description))]
        public BindableBool RRemove { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrDP_RightMax_Label), nameof(EzManiaModStrings.KrrDP_RightMax_Description))]
        public BindableNumber<int> RMaxKeys { get; set; } = new BindableInt(5)
        {
            MinValue = 1,
            MaxValue = 5,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.KrrDP_RightMin_Label), nameof(EzManiaModStrings.KrrDP_RightMin_Description))]
        public BindableNumber<int> RMinKeys { get; set; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 5,
        };

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
                yield return ("Enable Modify Keys", EnableModifyKeys.Value ? "On" : "Off");
                yield return ("Target Keys", $"{TargetKeys.Value}");
                yield return ("Left Mirror", LMirror.Value ? "On" : "Off");
                yield return ("Right Mirror", RMirror.Value ? "On" : "Off");
            }
        }

        private int originalKeys;
        private int finalKeys;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            originalKeys = (int)maniaBeatmap.Difficulty.CircleSize;

            var options = new KrrDpOptions
            {
                ModifyKeys = EnableModifyKeys.Value ? TargetKeys.Value : null,
                LMirror = LMirror.Value,
                LDensity = LDensity.Value,
                LRemove = LRemove.Value,
                LMaxKeys = LMaxKeys.Value,
                LMinKeys = LMinKeys.Value,
                RMirror = RMirror.Value,
                RDensity = RDensity.Value,
                RRemove = RRemove.Value,
                RMaxKeys = RMaxKeys.Value,
                RMinKeys = RMinKeys.Value
            };

            KrrDpConverter.Transform(maniaBeatmap, options);

            if (EnableModifyKeys.Value)
                finalKeys = TargetKeys.Value * 2;
            else
                finalKeys = originalKeys * 2;

            finalKeys = Math.Clamp(finalKeys, 1, 18);
            maniaBeatmap.Stages.Clear();
            maniaBeatmap.Stages.Add(new StageDefinition(finalKeys));
            maniaBeatmap.Difficulty.CircleSize = finalKeys;
        }
    }
}
