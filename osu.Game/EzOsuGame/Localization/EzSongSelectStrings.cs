// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Localization
{
    public static class EzSongSelectStrings
    {
        public static readonly EzLocalizationManager.EzLocalisableString SAVE_TO_COLLECTION = new EzLocalizationManager.EzLocalisableString(
            "将当前过滤结果保存到收藏夹",
            "Add all visible beatmaps to collection");

        public static readonly EzLocalizationManager.EzLocalisableString REMOVE_FROM_COLLECTION = new EzLocalizationManager.EzLocalisableString(
            "从收藏夹移除当前过滤结果",
            "Remove current filter result from collection");

        public static readonly EzLocalizationManager.EzLocalisableString REMOVE_FROM_COLLECTION_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "收藏夹已包含所有筛选结果，是否从收藏夹中移除这些谱面？",
            "The collection already contains all the filtered results, do you want to remove these beatmaps from the collection?");

        public static readonly EzLocalizationManager.EzLocalisableString PARTIALLY_OVERLAPPED = new EzLocalizationManager.EzLocalisableString(
            "筛选结果与收藏夹部分重",
            "The visible beatmaps is partially overlapped with the collection");

        public static readonly EzLocalizationManager.EzLocalisableString SELECT_ACTION_FOR_OVERLAP = new EzLocalizationManager.EzLocalisableString(
            " 个谱面已存在。请选择要执行的操作：",
            " beatmaps already exist. Please select the action to perform:");

        public static readonly EzLocalizationManager.EzLocalisableString ADD_DIFFERENCE = new EzLocalizationManager.EzLocalisableString(
            "添加差集",
            "Add difference");

        public static readonly EzLocalizationManager.EzLocalisableString REMOVE_INTERSECTION = new EzLocalizationManager.EzLocalisableString(
            "移除交集",
            "Remove intersection");

        public static readonly LocalisableString XXY_SR_FILTER_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "使用 xxy SR 进行难度过滤、排序、分组, 可能会增加加载时间",
            "Use xxy SR for difficulty filtering, sorting, and grouping, which may increase load times");

        public static readonly LocalisableString KEY_SOUND_PREVIEW_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "按键音预览：\n0 关闭; \n1 蓝灯开启 (全量音效预览); \n2 黄灯开启 (全量音效预览, 游戏中自动播放 note 音效, 按键不再触发样本播放) ",
            "Key sound preview: \n0 Off; \n1 BlueLight (keypress triggers samples); "
            + "\n2 GoldLight (preserve preview in song select; in gameplay auto-play note samples, keypresses no longer trigger sample playback)");

        public static readonly LocalisableString CLEAR_SELECTION = new EzLocalizationManager.EzLocalisableString(
            "点此处可清除过滤选择。\n(CS: cs ±0.5)",
            "Click here to clear the filter selection.\n(CS: cs ±0.5)");

        public static readonly LocalisableString MULTI_SELECT_BUTTON_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "多选模式",
            "Multi-select mode");
    }
}
