// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorNoteEditSettingsSection : EzSkinEditorSettingsGroup
    {
        [Resolved]
        private EzSkinEditorNoteEditSession session { get; set; } = null!;

        [Resolved]
        private EzSkinEditorSidebarActions actions { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private readonly BindableList<string> variantItems = new BindableList<string>();
        private SettingsDropdown<string> variantDropdown = null!;

        public EzSkinEditorNoteEditSettingsSection()
            : base(EzEditorStrings.GROUP_NOTE_EDIT)
        {
            Spacing = new Vector2(8);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            variantDropdown = new SettingsDropdown<string>
            {
                LabelText = EzEditorStrings.NOTE_VARIANT_LABEL,
                // ItemSource must bind before Current: binding a pre-populated source makes the dropdown
                // pick its first item while Current is still unset, which would otherwise be written
                // straight through to the shared session bindable.
                ItemSource = variantItems,
                Current = session.VariantId,
            };

            Children = new Drawable[]
            {
                variantDropdown,
                new SettingsColour
                {
                    LabelText = EzEditorStrings.NOTE_COLOUR_LABEL,
                    Current = session.NoteColour,
                },
                new SettingsCheckbox
                {
                    LabelText = EzEditorStrings.NOTE_TRUE_COLOURING_LABEL,
                    TooltipText = EzEditorStrings.NOTE_TRUE_COLOURING_TOOLTIP,
                    Current = session.TrueColouring,
                },
                new SettingsSlider<double>
                {
                    LabelText = EzEditorStrings.NOTE_WIDTH_LABEL,
                    Current = session.Width,
                    KeyboardStep = 1,
                },
                new SettingsSlider<double>
                {
                    LabelText = EzEditorStrings.NOTE_HEIGHT_LABEL,
                    Current = session.Height,
                    KeyboardStep = 1,
                },
                new SettingsTextBox
                {
                    LabelText = EzEditorStrings.NOTE_EXPORT_NAME_LABEL,
                    Current = session.ExportName,
                },
                new SettingsButton
                {
                    Text = EzEditorStrings.NOTE_CREATE_SNAPSHOT,
                    Action = () => actions.CreateNoteSnapshot?.Invoke(),
                },
                new SettingsButton
                {
                    Text = EzEditorStrings.NOTE_RESTORE_SNAPSHOT,
                    Action = () => actions.RestoreNoteSnapshot?.Invoke(),
                },
                new SettingsButton
                {
                    Text = EzEditorStrings.NOTE_EXPORT_BUTTON,
                    Action = () => actions.ExportNotePreview?.Invoke(),
                },
            };

            session.TrueColouring.Disabled = true;
            refreshVariants();

            session.Ruleset.BindValueChanged(_ => refreshVariants(), false);
            session.Part.BindValueChanged(_ => refreshVariants(), false);
        }

        private void refreshVariants()
        {
            var profile = EzSkinEditorNoteRulesetProfileRegistry.Get(session.Ruleset.Value);

            // ItemSource is bound to the shared session bindable, so rebuilding it writes through:
            // Clear() blanks the value and the first Add() selects the first item. Remember the
            // selection and restore it, otherwise every sidebar rebuild discards the session state.
            string? previous = session.VariantId.Value;

            variantItems.Clear();

            if (profile == null)
                return;

            foreach (var variant in profile.GetVariants(usesEzVariants(), session.Part.Value))
                variantItems.Add(variant.Id);

            if (variantItems.Count == 0)
                return;

            session.VariantId.Value = previous != null && variantItems.Contains(previous)
                ? previous
                : profile.GetDefaultVariantId(usesEzVariants(), session.Part.Value);
        }

        private bool usesEzVariants() => EzSkinEditorNoteVariantResolver.UsesEzVariants(skinManager.CurrentSkin.Value);
    }
}
