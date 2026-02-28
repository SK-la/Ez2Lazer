// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.LAsEzExtensions.Localization;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public enum KeySoundPreviewMode
    {
        Off = 0,

        [LocalisableDescription(typeof(KeySoundPreviewModeStrings), nameof(KeySoundPreviewModeStrings.AUTO_PREVIEW))]
        AutoPreview = 1,

        [LocalisableDescription(typeof(KeySoundPreviewModeStrings), nameof(KeySoundPreviewModeStrings.AUTO_PLAY_PLUS))]
        AutoPlayPlus = 2,
    }

    public static class KeySoundPreviewModeStrings
    {
        public static readonly LocalisableString AUTO_PREVIEW = new EzLocalizationManager.EzLocalisableString("全K音预览", "Auto Preview");
        public static readonly LocalisableString AUTO_PLAY_PLUS = new EzLocalizationManager.EzLocalisableString("全K音预览+游戏内自动播放", "Auto Preview + In-game Auto Play");
    }
}
