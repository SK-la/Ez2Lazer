// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorSkinIniGeneralSection : EzSkinEditorSkinIniSectionBase
    {
        private readonly Dictionary<string, Bindable<string>> textFields = new Dictionary<string, Bindable<string>>();
        private readonly Dictionary<string, Bindable<bool>> boolFields = new Dictionary<string, Bindable<bool>>();

        public EzSkinEditorSkinIniGeneralSection(EzSkinIniSession? session)
            : base(session)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Add(new OsuSpriteText
            {
                Text = EzEditorStrings.SKIN_INI_GENERAL_HINT,
                Font = OsuFont.Default.With(size: 14),
                Colour = Color4.Gray,
            });

#if DEBUG
            Add(new RawEditorPlaceholderButton
            {
                RelativeSizeAxes = Axes.X,
                Height = 40,
            });
#endif

            foreach (var field in EzSkinIniFieldCatalog.GeneralFields)
            {
                switch (field.Kind)
                {
                    case EzSkinIniFieldKind.Text:
                        var textBindable = new Bindable<string>(string.Empty);
                        textFields[field.Key] = textBindable;
                        textBindable.ValueChanged += e => CommitGeneralField(field.Key, e.NewValue);
                        Add(CreateTextField(field.Label, textBindable));
                        break;

                    case EzSkinIniFieldKind.Bool:
                        var boolBindable = new Bindable<bool>();
                        boolFields[field.Key] = boolBindable;
                        boolBindable.ValueChanged += e => CommitGeneralBoolField(field.Key, e.NewValue);
                        Add(CreateBoolField(field.Label, boolBindable));
                        break;
                }
            }

            reloadFromSession();
        }

        private void reloadFromSession()
        {
            WithApplying(() =>
            {
                var document = ParseDocument();

                foreach (var (key, bindable) in textFields)
                    bindable.Value = document?.GetValue(EzSkinIniDocument.GENERAL_SECTION, key) ?? string.Empty;

                foreach (var (key, bindable) in boolFields)
                    bindable.Value = document?.GetValue(EzSkinIniDocument.GENERAL_SECTION, key) == "1";
            });
        }

#if DEBUG
        private partial class RawEditorPlaceholderButton : OsuButton
        {
            public RawEditorPlaceholderButton()
            {
                Text = EzEditorStrings.SKIN_INI_RAW_EDITOR_PLACEHOLDER;
                Enabled.Value = false;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                BackgroundColour = colours.Blue3;
            }
        }
#endif
    }
}
