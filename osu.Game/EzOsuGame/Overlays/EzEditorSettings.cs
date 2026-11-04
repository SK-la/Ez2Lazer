// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzEditorSettings : SettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.EDITOR_SECTION_HEADER;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = EzSettingsStrings.EDITOR_DIVISOR_NOTE_COLOURING,
                HintText = EzSettingsStrings.EDITOR_DIVISOR_NOTE_COLOURING_TOOLTIP,
                Current = ezConfig.GetBindable<bool>(Ez2Setting.EditorDivisorNoteColouring),
            })
            {
                Keywords = new[] { "editor", "divisor", "colour", "mania", "note" },
            });
        }
    }
}
