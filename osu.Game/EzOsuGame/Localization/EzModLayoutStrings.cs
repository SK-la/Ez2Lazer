// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Localization
{
    public static class EzModLayoutStrings
    {
        static EzModLayoutStrings() => EzLocalizationManager.AutoFillEnglish(typeof(EzModLayoutStrings));

        public static readonly EzLocalizationManager.EzLocalisableString EDIT = new EzLocalizationManager.EzLocalisableString("编辑");

        public static readonly EzLocalizationManager.EzLocalisableString DONE = new EzLocalizationManager.EzLocalisableString("完成");

        public static readonly EzLocalizationManager.EzLocalisableString NEW_GROUP = new EzLocalizationManager.EzLocalisableString("新分组");

        public static readonly EzLocalizationManager.EzLocalisableString HIDDEN_MODS = new EzLocalizationManager.EzLocalisableString("隐藏的 Mod");
    }
}
