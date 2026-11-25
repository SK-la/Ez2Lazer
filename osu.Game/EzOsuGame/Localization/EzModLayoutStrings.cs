// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Localization
{
    public static class EzModLayoutStrings
    {
        public static readonly EzLocalizationManager.EzLocalisableString NO_RESULTS =
            new EzLocalizationManager.EzLocalisableString("没有匹配的 Mod", "No matching mods");

        public static readonly EzLocalizationManager.EzLocalisableString HORIZONTAL =
            new EzLocalizationManager.EzLocalisableString("横向", "Horizontal");

        public static readonly EzLocalizationManager.EzLocalisableString VERTICAL =
            new EzLocalizationManager.EzLocalisableString("纵向", "Vertical");

        public static readonly EzLocalizationManager.EzLocalisableString EXPANDED =
            new EzLocalizationManager.EzLocalisableString("展开", "Expanded");

        public static readonly EzLocalizationManager.EzLocalisableString COMPACT =
            new EzLocalizationManager.EzLocalisableString("压缩", "Compact");

        public static readonly EzLocalizationManager.EzLocalisableString EDIT =
            new EzLocalizationManager.EzLocalisableString("编辑", "Edit");

        public static readonly EzLocalizationManager.EzLocalisableString DONE =
            new EzLocalizationManager.EzLocalisableString("完成", "Done");

        public static readonly EzLocalizationManager.EzLocalisableString NEW_GROUP =
            new EzLocalizationManager.EzLocalisableString("新分组", "New group");

        public static readonly EzLocalizationManager.EzLocalisableString HIDDEN_MODS =
            new EzLocalizationManager.EzLocalisableString("隐藏的 Mod", "Hidden mods");

        public static readonly EzLocalizationManager.EzLocalisableString COLUMN_SHEAR =
            new EzLocalizationManager.EzLocalisableString("倾斜角度", "Column shear");

        public static readonly EzLocalizationManager.EzLocalisableString DELETE_GROUP =
            new EzLocalizationManager.EzLocalisableString("删除分组", "Delete group");

        public static readonly EzLocalizationManager.EzLocalisableString GROUP_NAME_PLACEHOLDER =
            new EzLocalizationManager.EzLocalisableString("分组名称", "Group name");

        public static string SeedGroupName(ModType type) => (type switch
        {
            ModType.LA_Mod => seed_la_mod,
            ModType.CommunityMod => seed_community,
            ModType.DifficultyReduction => seed_difficulty_reduction,
            ModType.DifficultyIncrease => seed_difficulty_increase,
            ModType.Automation => seed_automation,
            ModType.Conversion => seed_conversion,
            ModType.Fun => seed_fun,
            _ => seed_other,
        }).ToString();

        private static readonly EzLocalizationManager.EzLocalisableString seed_la_mod =
            new EzLocalizationManager.EzLocalisableString("LA 模组", "LA mods");

        private static readonly EzLocalizationManager.EzLocalisableString seed_community =
            new EzLocalizationManager.EzLocalisableString("社区模组", "Community mods");

        private static readonly EzLocalizationManager.EzLocalisableString seed_difficulty_reduction =
            new EzLocalizationManager.EzLocalisableString("降低难度", "Difficulty reduction");

        private static readonly EzLocalizationManager.EzLocalisableString seed_difficulty_increase =
            new EzLocalizationManager.EzLocalisableString("提高难度", "Difficulty increase");

        private static readonly EzLocalizationManager.EzLocalisableString seed_automation =
            new EzLocalizationManager.EzLocalisableString("自动化", "Automation");

        private static readonly EzLocalizationManager.EzLocalisableString seed_conversion =
            new EzLocalizationManager.EzLocalisableString("转换", "Conversion");

        private static readonly EzLocalizationManager.EzLocalisableString seed_fun =
            new EzLocalizationManager.EzLocalisableString("趣味", "Fun");

        private static readonly EzLocalizationManager.EzLocalisableString seed_other =
            new EzLocalizationManager.EzLocalisableString("其他", "Other");
    }
}
