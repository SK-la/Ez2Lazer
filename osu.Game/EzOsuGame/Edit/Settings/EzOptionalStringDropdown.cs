// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Edit.Settings
{
    /// <summary>
    /// 字符串下拉。空字符串显示为 <see cref="EmptyLabel"/>。
    /// </summary>
    public partial class EzOptionalStringDropdown : SettingsDropdown<string>
    {
        public LocalisableString EmptyLabel { get; init; }

        protected override OsuDropdown<string> CreateDropdown() => new NoneLabelDropdown { EmptyLabel = EmptyLabel };

        private partial class NoneLabelDropdown : DropdownControl
        {
            public LocalisableString EmptyLabel { get; init; }

            protected override LocalisableString GenerateItemText(string item)
                => string.IsNullOrEmpty(item) ? EmptyLabel : item;
        }
    }
}
