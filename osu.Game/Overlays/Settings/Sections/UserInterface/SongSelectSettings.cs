// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Mods.Input;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class SongSelectSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.SongSelectHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, Ez2ConfigManager ezConfig)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.ShowConvertedBeatmaps,
                    Current = config.GetBindable<bool>(OsuSetting.ShowConvertedBeatmaps),
                })
                {
                    Keywords = new[] { "converts", "converted" }
                },
                new SettingsItemV2(new SortDirectionDropdown
                {
                    Caption = EzSongSelectStrings.SORT_DIRECTION,
                    Current = ezConfig.GetBindable<SortDirection>(Ez2Setting.SongSelectSortDirection),
                })
                {
                    Keywords = new[] { "sort", "ascending", "descending", "order" },
                },
                new SettingsItemV2(new FormEnumDropdown<RandomSelectAlgorithm>
                {
                    Caption = UserInterfaceStrings.RandomSelectionAlgorithm,
                    Current = config.GetBindable<RandomSelectAlgorithm>(OsuSetting.RandomSelectAlgorithm),
                }),
                new SettingsItemV2(new FormEnumDropdown<ModSelectHotkeyStyle>
                {
                    Caption = UserInterfaceStrings.ModSelectHotkeyStyle,
                    Current = config.GetBindable<ModSelectHotkeyStyle>(OsuSetting.ModSelectHotkeyStyle),
                })
                {
                    ApplyClassicDefault = c => ((IHasCurrentValue<ModSelectHotkeyStyle>)c).Current.Value = ModSelectHotkeyStyle.Classic,
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.ModSelectTextSearchStartsActive,
                    Current = config.GetBindable<bool>(OsuSetting.ModSelectTextSearchStartsActive),
                })
                {
                    ApplyClassicDefault = c => ((IHasCurrentValue<bool>)c).Current.Value = false,
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = GameplaySettingsStrings.BackgroundBlur,
                    Current = config.GetBindable<bool>(OsuSetting.SongSelectBackgroundBlur),
                }),
            };
        }

        private partial class SortDirectionDropdown : FormEnumDropdown<SortDirection>
        {
            protected override LocalisableString GenerateItemText(SortDirection item) => item switch
            {
                SortDirection.Ascending => EzSongSelectStrings.SORT_DIRECTION_ASCENDING,
                SortDirection.Descending => EzSongSelectStrings.SORT_DIRECTION_DESCENDING,
                _ => base.GenerateItemText(item),
            };
        }
    }
}
