// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Pets;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzPetSettings : SettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.DESKTOP_PET_SETTINGS_HEADER;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig, Storage storage)
        {
            var loader = new EzPetPackLoader(storage);
            var packNames = new List<string>(loader.ListPackNames());
            var packBindable = ezConfig.GetBindable<string>(Ez2Setting.DesktopPetPack);

            if (!string.IsNullOrEmpty(packBindable.Value) && !packNames.Contains(packBindable.Value))
                packNames.Insert(0, packBindable.Value);

            if (packNames.Count == 0)
                packNames.Add(EzDefaultPetPack.NAME);

            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.DESKTOP_PET_ENABLED,
                    HintText = EzSettingsStrings.DESKTOP_PET_ENABLED_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.DesktopPetEnabled),
                })
                {
                    Keywords = new[] { "pet", "desktop", "mascot", "桌宠" }
                },
                new SettingsItemV2(new FormDropdown<string>
                {
                    Caption = EzSettingsStrings.DESKTOP_PET_PACK,
                    HintText = EzSettingsStrings.DESKTOP_PET_PACK_TOOLTIP,
                    Current = packBindable,
                    Items = packNames,
                })
                {
                    Keywords = new[] { "pet", "pack", "mascot", "桌宠" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = EzSettingsStrings.DESKTOP_PET_SCALE,
                    HintText = EzSettingsStrings.DESKTOP_PET_SCALE_TOOLTIP,
                    Current = ezConfig.GetBindable<double>(Ez2Setting.DesktopPetScale),
                    KeyboardStep = 0.05f,
                })
                {
                    Keywords = new[] { "pet", "scale", "size", "桌宠" }
                },
            });
        }
    }
}
