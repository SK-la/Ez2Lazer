// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorNoteEditSettingsSection : FillFlowContainer
    {
        private readonly EzSkinEditorNoteEditSession session;
        private readonly Action createNoteSnapshot;
        private readonly Action restoreNoteSnapshot;
        private readonly Action exportNotePreview;
        private readonly Action requestRefresh;
        private readonly ISkin editorSkin;

        private readonly BindableList<string> variantItems = new BindableList<string>();
        private SettingsDropdown<string> variantDropdown = null!;

        public EzSkinEditorNoteEditSettingsSection(
            EzSkinEditorNoteEditSession session,
            ISkin editorSkin,
            Action createNoteSnapshot,
            Action restoreNoteSnapshot,
            Action exportNotePreview,
            Action requestRefresh)
        {
            this.session = session;
            this.editorSkin = editorSkin;
            this.createNoteSnapshot = createNoteSnapshot;
            this.restoreNoteSnapshot = restoreNoteSnapshot;
            this.exportNotePreview = exportNotePreview;
            this.requestRefresh = requestRefresh;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            variantDropdown = new SettingsDropdown<string>
            {
                LabelText = EzEditorStrings.NOTE_VARIANT_LABEL,
                Current = session.VariantId,
                ItemSource = variantItems,
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
                    Action = createNoteSnapshot,
                },
                new SettingsButton
                {
                    Text = EzEditorStrings.NOTE_RESTORE_SNAPSHOT,
                    Action = restoreNoteSnapshot,
                },
                new SettingsButton
                {
                    Text = EzEditorStrings.NOTE_EXPORT_BUTTON,
                    Action = exportNotePreview,
                },
            };

            session.TrueColouring.Disabled = true;
            refreshVariants();

            session.VariantId.BindValueChanged(_ => requestRefresh());
            session.NoteColour.BindValueChanged(_ => requestRefresh());
            session.Width.BindValueChanged(_ => requestRefresh());
            session.Height.BindValueChanged(_ => requestRefresh());
        }

        private void refreshVariants()
        {
            var profile = EzSkinEditorNoteRulesetProfileRegistry.Get(session.Ruleset.Value);

            variantItems.Clear();

            if (profile == null)
                return;

            foreach (var variant in profile.GetVariants(editorSkin, session.Part.Value))
                variantItems.Add(variant.Id);

            if (variantItems.Count == 0)
                return;

            if (!variantItems.Contains(session.VariantId.Value))
                session.VariantId.Value = profile.GetDefaultVariantId(editorSkin, session.Part.Value);
        }
    }
}
