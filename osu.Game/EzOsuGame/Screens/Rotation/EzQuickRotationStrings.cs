// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public static class EzQuickRotationStrings
    {
        public static readonly EzLocalizationManager.EzLocalisableString TRANSITION_TITLE = new EzLocalizationManager.EzLocalisableString("即将进入快速轮换", "Entering Quick Rotation");

        public static readonly EzLocalizationManager.EzLocalisableString TRANSITION_SKIP_HINT = new EzLocalizationManager.EzLocalisableString("点击或按跳过键继续", "Click or press skip to continue");

        public static readonly EzLocalizationManager.EzLocalisableString PICK_TITLE = new EzLocalizationManager.EzLocalisableString("选择下一张谱面", "Pick the next beatmap");

        public static readonly EzLocalizationManager.EzLocalisableString PICK_CAPTION = new EzLocalizationManager.EzLocalisableString("选择一张卡牌开始游玩", "Select a card to play!");

        public static readonly EzLocalizationManager.EzLocalisableString PICK_BASELINE = new EzLocalizationManager.EzLocalisableString("基准难度: {0:0.00}", "Baseline: {0:0.00}");

        public static readonly EzLocalizationManager.EzLocalisableString PICK_ENTER_RANDOM = new EzLocalizationManager.EzLocalisableString("按回车随机选择", "Press enter for a random pick");

        public static readonly EzLocalizationManager.EzLocalisableString PICK_PLAY = new EzLocalizationManager.EzLocalisableString("开始", "Play");

        public static readonly EzLocalizationManager.EzLocalisableString PICK_END_SESSION = new EzLocalizationManager.EzLocalisableString("结束轮换", "End rotation");

        public static readonly EzLocalizationManager.EzLocalisableString POOL_EMPTY = new EzLocalizationManager.EzLocalisableString("候选池已用尽", "No more beatmaps in the pool");

        public static readonly EzLocalizationManager.EzLocalisableString CONTINUE_ROTATION = new EzLocalizationManager.EzLocalisableString("继续快速轮换", "Continue Quick Rotation");
    }
}
