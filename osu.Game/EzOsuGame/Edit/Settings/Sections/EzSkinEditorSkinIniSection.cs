// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorSkinIniSection : FillFlowContainer
    {
        private readonly EzSkinIniSession? session;

        private bool applying;

        private readonly Bindable<string> name = new Bindable<string>(string.Empty);
        private readonly Bindable<string> author = new Bindable<string>(string.Empty);
        private readonly Bindable<string> version = new Bindable<string>(string.Empty);
        private readonly Bindable<string> columnWidth = new Bindable<string>(string.Empty);
        private readonly Bindable<string> hitPosition = new Bindable<string>(string.Empty);
        private readonly Bindable<string> widthForNoteHeightScale = new Bindable<string>(string.Empty);
        private readonly Bindable<string> stagePaddingTop = new Bindable<string>(string.Empty);
        private readonly Bindable<string> stagePaddingBottom = new Bindable<string>(string.Empty);
        private readonly Bindable<bool> keysUnderNotes = new Bindable<bool>();
        private readonly Bindable<string> noteBodyStyle = new Bindable<string>(string.Empty);

        public EzSkinEditorSkinIniSection(EzSkinIniSession? session)
        {
            this.session = session;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = "编辑当前皮肤的 skin.ini。保存前会自动备份到 Backup/。",
                    Font = OsuFont.Default.With(size: 14),
                    Colour = Color4.Gray,
                },
                createHeader("General"),
                createTextField("Name", name, EzSkinIniDocument.GENERAL_SECTION, "Name"),
                createTextField("Author", author, EzSkinIniDocument.GENERAL_SECTION, "Author"),
                createTextField("Version", version, EzSkinIniDocument.GENERAL_SECTION, "Version"),
                createHeader("Mania"),
                createTextField("ColumnWidth", columnWidth, EzSkinIniDocument.MANIA_SECTION, "ColumnWidth"),
                createTextField("HitPosition", hitPosition, EzSkinIniDocument.MANIA_SECTION, "HitPosition"),
                createTextField("WidthForNoteHeightScale", widthForNoteHeightScale, EzSkinIniDocument.MANIA_SECTION, "WidthForNoteHeightScale"),
                createTextField("StagePaddingTop", stagePaddingTop, EzSkinIniDocument.MANIA_SECTION, "StagePaddingTop"),
                createTextField("StagePaddingBottom", stagePaddingBottom, EzSkinIniDocument.MANIA_SECTION, "StagePaddingBottom"),
                new SettingsCheckbox
                {
                    LabelText = "KeysUnderNotes",
                    Current = keysUnderNotes,
                },
                createTextField("NoteBodyStyle", noteBodyStyle, EzSkinIniDocument.MANIA_SECTION, "NoteBodyStyle"),
            };

            keysUnderNotes.ValueChanged += _ => commitBoolField(EzSkinIniDocument.MANIA_SECTION, "KeysUnderNotes", keysUnderNotes.Value);
            reloadFromSession();
        }

        private Drawable createHeader(string text) =>
            new OsuSpriteText
            {
                Text = text,
                Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold),
                Colour = Color4.White,
                Margin = new MarginPadding { Top = 6 },
            };

        private Drawable createTextField(string label, Bindable<string> bindable, string section, string key)
        {
            bindable.ValueChanged += e => commitField(section, key, e.NewValue);

            return new SettingsTextBox
            {
                LabelText = label,
                Current = bindable,
            };
        }

        private void reloadFromSession()
        {
            applying = true;

            var document = session?.ParseDraftDocument();

            name.Value = document?.GetValue(EzSkinIniDocument.GENERAL_SECTION, "Name") ?? string.Empty;
            author.Value = document?.GetValue(EzSkinIniDocument.GENERAL_SECTION, "Author") ?? string.Empty;
            version.Value = document?.GetValue(EzSkinIniDocument.GENERAL_SECTION, "Version") ?? string.Empty;
            columnWidth.Value = document?.GetValue(EzSkinIniDocument.MANIA_SECTION, "ColumnWidth") ?? string.Empty;
            hitPosition.Value = document?.GetValue(EzSkinIniDocument.MANIA_SECTION, "HitPosition") ?? string.Empty;
            widthForNoteHeightScale.Value = document?.GetValue(EzSkinIniDocument.MANIA_SECTION, "WidthForNoteHeightScale") ?? string.Empty;
            stagePaddingTop.Value = document?.GetValue(EzSkinIniDocument.MANIA_SECTION, "StagePaddingTop") ?? string.Empty;
            stagePaddingBottom.Value = document?.GetValue(EzSkinIniDocument.MANIA_SECTION, "StagePaddingBottom") ?? string.Empty;
            keysUnderNotes.Value = document?.GetValue(EzSkinIniDocument.MANIA_SECTION, "KeysUnderNotes") == "1";
            noteBodyStyle.Value = document?.GetValue(EzSkinIniDocument.MANIA_SECTION, "NoteBodyStyle") ?? string.Empty;

            applying = false;
        }

        private void commitField(string section, string key, string value)
        {
            if (applying || session == null)
                return;

            var document = session.ParseDraftDocument();
            document.SetValue(section, key, value);
            session.ApplyDocument(document);
        }

        private void commitBoolField(string section, string key, bool value) =>
            commitField(section, key, value ? "1" : "0");
    }
}
