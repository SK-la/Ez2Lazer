// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Rulesets.Mania.Edit
{
    public partial class EzSkinEditorEditorDivisorNoteColouringSection : EzSkinEditorSettingsGroup
    {
        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        public EzSkinEditorEditorDivisorNoteColouringSection()
            : base(EzEditorStrings.GROUP_EDITOR_DIVISOR_NOTE_COLOURING)
        {
            Spacing = new Vector2(8);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = EzSettingsStrings.EDITOR_DIVISOR_NOTE_COLOURING,
                HintText = EzSettingsStrings.EDITOR_DIVISOR_NOTE_COLOURING_TOOLTIP,
                Current = ezConfig.GetBindable<bool>(Ez2Setting.EditorDivisorNoteColouring),
            }));
        }
    }
}
