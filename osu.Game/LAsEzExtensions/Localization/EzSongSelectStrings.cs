// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.LAsEzExtensions.Localization
{
    public static class EzSongSelectStrings
    {
        public static readonly LocalisableString KEY_SOUND_PREVIEW_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "按键音预览：\n0 关闭; \n1 蓝灯开启 (全量音效预览); \n2 黄灯开启 (全量音效预览, 游戏中自动播放 note 音效, 按键不再触发样本播放) ",
            "Key sound preview: \n0 Off; \n1 BlueLight (keypress triggers samples); \n2 GoldLight (preserve preview in song select; in gameplay auto-play note samples, keypresses no longer trigger sample playback)");
    }
}
