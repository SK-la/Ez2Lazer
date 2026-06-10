// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public abstract partial class EzSkinEditorSkinIniSectionBase : FillFlowContainer
    {
        protected readonly EzSkinIniSession? Session;

        protected bool Applying { get; private set; }

        protected EzSkinEditorSkinIniSectionBase(EzSkinIniSession? session)
        {
            Session = session;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);
        }

        protected Drawable CreateSubheader(LocalisableString text) =>
            new OsuSpriteText
            {
                Text = text,
                Font = OsuFont.Default.With(size: 15, weight: FontWeight.Bold),
                Colour = Color4.White,
                Margin = new MarginPadding { Top = 4 },
            };

        protected SettingsTextBox CreateTextField(string label, Bindable<string> bindable) =>
            new SettingsTextBox
            {
                LabelText = label,
                Current = bindable,
            };

        protected SettingsCheckbox CreateBoolField(string label, Bindable<bool> bindable) =>
            new SettingsCheckbox
            {
                LabelText = label,
                Current = bindable,
            };

        protected SettingsColour CreateColourField(string label, Bindable<Colour4> bindable) =>
            new SettingsColour
            {
                LabelText = label,
                Current = bindable,
            };

        protected void WithApplying(System.Action action)
        {
            Applying = true;

            try
            {
                action();
            }
            finally
            {
                Applying = false;
            }
        }

        protected void CommitGeneralField(string key, string value)
        {
            if (Applying || Session == null)
                return;

            var document = Session.ParseDraftDocument();
            document.SetValue(EzSkinIniDocument.GENERAL_SECTION, key, value);
            Session.ApplyDocument(document);
        }

        protected void CommitGeneralBoolField(string key, bool value) =>
            CommitGeneralField(key, value ? "1" : "0");

        protected void CommitColourField(string key, Colour4 colour)
        {
            if (Applying || Session == null)
                return;

            var document = Session.ParseDraftDocument();
            document.SetColourValue(key, colour);
            Session.ApplyDocument(document);
        }

        protected void CommitManiaField(int keys, string key, string value)
        {
            if (Applying || Session == null)
                return;

            var document = Session.ParseDraftDocument();
            document.EnsureManiaBlock(keys);
            document.SetManiaValue(keys, key, value);
            Session.ApplyDocument(document);
        }

        protected void CommitManiaBoolField(int keys, string key, bool value) =>
            CommitManiaField(keys, key, value ? "1" : "0");

        protected void CommitManiaColourField(int keys, string key, Colour4 colour)
        {
            if (Applying || Session == null)
                return;

            var document = Session.ParseDraftDocument();
            document.EnsureManiaBlock(keys);
            document.SetManiaValue(keys, key, EzSkinIniColourFormat.ToIniString(colour, includeAlpha: true));
            Session.ApplyDocument(document);
        }

        protected EzSkinIniDocument? ParseDocument() => Session?.ParseDraftDocument();
    }
}
