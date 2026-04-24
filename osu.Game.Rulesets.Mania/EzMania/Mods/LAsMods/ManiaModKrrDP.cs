// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.KrrConversion;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods
{
    public class ManiaModKrrDP : Mod, IApplicableAfterBeatmapConversion, IEzApplyOrder
    {
        public override string Name => "Krr DP Converter";
        public override string Acronym => "KDP";
        public override LocalisableString Description => KrrDPStrings.KRR_DP_DESCRIPTION;
        public override double ScoreMultiplier => 1;
        public override ModType Type => ModType.LA_Mod;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(KrrDPStrings), nameof(KrrDPStrings.KRR_DP_ENABLE_MODIFY_KEYS_LABEL), nameof(KrrDPStrings.KRR_DP_ENABLE_MODIFY_KEYS_DESCRIPTION))]
        public BindableBool EnableModifyKeys { get; } = new BindableBool();

        [SettingSource(typeof(KrrDPStrings), nameof(KrrDPStrings.KRR_DP_TARGET_KEYS_LABEL), nameof(KrrDPStrings.KRR_DP_TARGET_KEYS_DESCRIPTION))]
        public BindableNumber<int> TargetKeys { get; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 9,
        };

        [SettingSource(typeof(KrrDPStrings), nameof(KrrDPStrings.KRR_DP_LEFT_MIRROR_LABEL), nameof(KrrDPStrings.KRR_DP_LEFT_MIRROR_DESCRIPTION))]
        public BindableBool LMirror { get; set; } = new BindableBool(false);

        [SettingSource(typeof(KrrDPStrings), nameof(KrrDPStrings.KRR_DP_LEFT_DENSITY_LABEL), nameof(KrrDPStrings.KRR_DP_LEFT_DENSITY_DESCRIPTION))]
        public BindableBool LDensity { get; set; } = new BindableBool(false);

        [SettingSource(typeof(KrrDPStrings), nameof(KrrDPStrings.KRR_DP_LEFT_REMOVE_LABEL), nameof(KrrDPStrings.KRR_DP_LEFT_REMOVE_DESCRIPTION))]
        public BindableBool LRemove { get; set; } = new BindableBool(false);

        [SettingSource(typeof(KrrDPStrings), nameof(KrrDPStrings.KRR_DP_LEFT_MAX_LABEL), nameof(KrrDPStrings.KRR_DP_LEFT_MAX_DESCRIPTION))]
        public BindableNumber<int> LMaxKeys { get; set; } = new BindableInt(5)
        {
            MinValue = 1,
            MaxValue = 5,
        };

        [SettingSource(typeof(KrrDPStrings), nameof(KrrDPStrings.KRR_DP_LEFT_MIN_LABEL), nameof(KrrDPStrings.KRR_DP_LEFT_MIN_DESCRIPTION))]
        public BindableNumber<int> LMinKeys { get; set; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 5,
        };

        [SettingSource(typeof(KrrDPStrings), nameof(KrrDPStrings.KRR_DP_RIGHT_MIRROR_LABEL), nameof(KrrDPStrings.KRR_DP_RIGHT_MIRROR_DESCRIPTION))]
        public BindableBool RMirror { get; set; } = new BindableBool(false);

        [SettingSource(typeof(KrrDPStrings), nameof(KrrDPStrings.KRR_DP_RIGHT_DENSITY_LABEL), nameof(KrrDPStrings.KRR_DP_RIGHT_DENSITY_DESCRIPTION))]
        public BindableBool RDensity { get; set; } = new BindableBool(false);

        [SettingSource(typeof(KrrDPStrings), nameof(KrrDPStrings.KRR_DP_RIGHT_REMOVE_LABEL), nameof(KrrDPStrings.KRR_DP_RIGHT_REMOVE_DESCRIPTION))]
        public BindableBool RRemove { get; set; } = new BindableBool(false);

        [SettingSource(typeof(KrrDPStrings), nameof(KrrDPStrings.KRR_DP_RIGHT_MAX_LABEL), nameof(KrrDPStrings.KRR_DP_RIGHT_MAX_DESCRIPTION))]
        public BindableNumber<int> RMaxKeys { get; set; } = new BindableInt(5)
        {
            MinValue = 1,
            MaxValue = 5,
        };

        [SettingSource(typeof(KrrDPStrings), nameof(KrrDPStrings.KRR_DP_RIGHT_MIN_LABEL), nameof(KrrDPStrings.KRR_DP_RIGHT_MIN_DESCRIPTION))]
        public BindableNumber<int> RMinKeys { get; set; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 5,
        };

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
                yield return (KrrDPStrings.KRR_DP_ENABLE_MODIFY_KEYS_LABEL, EnableModifyKeys.Value ? "On" : "Off");
                yield return (KrrDPStrings.KRR_DP_TARGET_KEYS_LABEL, $"{TargetKeys.Value}");

                if (LDensity.Value)
                {
                    yield return (KrrDPStrings.KRR_DP_LEFT_DENSITY_LABEL, "On");
                    yield return (KrrDPStrings.KRR_DP_LEFT_MAX_LABEL, $"{LMaxKeys.Value}");
                    yield return (KrrDPStrings.KRR_DP_LEFT_MIN_LABEL, $"{LMinKeys.Value}");
                }

                if (LRemove.Value)
                    yield return (KrrDPStrings.KRR_DP_LEFT_REMOVE_LABEL, "On");

                yield return (KrrDPStrings.KRR_DP_LEFT_MIRROR_LABEL, LMirror.Value ? "On" : "Off");

                if (RDensity.Value)
                {
                    yield return (KrrDPStrings.KRR_DP_RIGHT_DENSITY_LABEL, "On");
                    yield return (KrrDPStrings.KRR_DP_RIGHT_MAX_LABEL, $"{RMaxKeys.Value}");
                    yield return (KrrDPStrings.KRR_DP_RIGHT_MIN_LABEL, $"{RMinKeys.Value}");
                }

                if (RRemove.Value)
                    yield return (KrrDPStrings.KRR_DP_RIGHT_REMOVE_LABEL, "On");

                yield return (KrrDPStrings.KRR_DP_RIGHT_MIRROR_LABEL, RMirror.Value ? "On" : "Off");
                yield return (EzCommonModStrings.APPLY_ORDER_LABEL, $"{ApplyOrderIndex.Value}");
            }
        }

        private int originalKeys;
        private int finalKeys;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            originalKeys = (int)maniaBeatmap.Difficulty.CircleSize;

            if (EnableModifyKeys.Value)
                finalKeys = TargetKeys.Value * 2;
            else if (originalKeys <= 9)
                finalKeys = originalKeys * 2;
            else return;

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

            finalKeys = Math.Clamp(finalKeys, 1, 18);
            maniaBeatmap.Stages.Clear();
            maniaBeatmap.Stages.Add(new StageDefinition(finalKeys));
            maniaBeatmap.Difficulty.CircleSize = finalKeys;
        }
    }

    public static class KrrDPStrings
    {
        public static readonly LocalisableString KRR_DP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("[KrrTool] DP转换器", "[KrrTool] DP Converter");
        public static readonly LocalisableString KRR_DP_ENABLE_MODIFY_KEYS_LABEL = new EzLocalizationManager.EzLocalisableString("启用键数修改", "Enable Modify Keys");
        public static readonly LocalisableString KRR_DP_ENABLE_MODIFY_KEYS_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("启用后可指定左右各自的键数", "Enable to set keys per side.");
        public static readonly LocalisableString KRR_DP_TARGET_KEYS_LABEL = new EzLocalizationManager.EzLocalisableString("目标键数", "Target Keys");
        public static readonly LocalisableString KRR_DP_TARGET_KEYS_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("左右每侧的目标键数", "Target keys per side.");
        public static readonly LocalisableString KRR_DP_LEFT_MIRROR_LABEL = new EzLocalizationManager.EzLocalisableString("左侧镜像", "Left Mirror");
        public static readonly LocalisableString KRR_DP_LEFT_MIRROR_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("左侧镜像翻转", "Mirror left side.");
        public static readonly LocalisableString KRR_DP_RIGHT_MIRROR_LABEL = new EzLocalizationManager.EzLocalisableString("右侧镜像", "Right Mirror");
        public static readonly LocalisableString KRR_DP_RIGHT_MIRROR_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("右侧镜像翻转", "Mirror right side.");
        public static readonly LocalisableString KRR_DP_LEFT_DENSITY_LABEL = new EzLocalizationManager.EzLocalisableString("左侧密度", "Left Density");
        public static readonly LocalisableString KRR_DP_LEFT_DENSITY_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("调整左侧密度", "Adjust left density.");
        public static readonly LocalisableString KRR_DP_RIGHT_DENSITY_LABEL = new EzLocalizationManager.EzLocalisableString("右侧密度", "Right Density");
        public static readonly LocalisableString KRR_DP_RIGHT_DENSITY_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("调整右侧密度", "Adjust right density.");
        public static readonly LocalisableString KRR_DP_LEFT_REMOVE_LABEL = new EzLocalizationManager.EzLocalisableString("移除左侧", "Remove Left");
        public static readonly LocalisableString KRR_DP_LEFT_REMOVE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("移除左侧所有音符", "Remove left side.");
        public static readonly LocalisableString KRR_DP_RIGHT_REMOVE_LABEL = new EzLocalizationManager.EzLocalisableString("移除右侧", "Remove Right");
        public static readonly LocalisableString KRR_DP_RIGHT_REMOVE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("移除右侧所有音符", "Remove right side.");
        public static readonly LocalisableString KRR_DP_LEFT_MAX_LABEL = new EzLocalizationManager.EzLocalisableString("左侧最大键数", "Left Max Keys");
        public static readonly LocalisableString KRR_DP_LEFT_MAX_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("左侧密度最大键数", "Left density max keys.");
        public static readonly LocalisableString KRR_DP_LEFT_MIN_LABEL = new EzLocalizationManager.EzLocalisableString("左侧最小键数", "Left Min Keys");
        public static readonly LocalisableString KRR_DP_LEFT_MIN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("左侧密度最小键数", "Left density min keys.");
        public static readonly LocalisableString KRR_DP_RIGHT_MAX_LABEL = new EzLocalizationManager.EzLocalisableString("右侧最大键数", "Right Max Keys");
        public static readonly LocalisableString KRR_DP_RIGHT_MAX_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("右侧密度最大键数", "Right density max keys.");
        public static readonly LocalisableString KRR_DP_RIGHT_MIN_LABEL = new EzLocalizationManager.EzLocalisableString("右侧最小键数", "Right Min Keys");
        public static readonly LocalisableString KRR_DP_RIGHT_MIN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("右侧密度最小键数", "Right density min keys.");
    }
}
