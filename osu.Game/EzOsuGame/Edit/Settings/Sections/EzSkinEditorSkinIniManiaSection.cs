// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorSkinIniManiaSection : EzSkinEditorSkinIniSectionBase
    {
        private const int default_keys = 4;

        private readonly Bindable<int> selectedKeys = new Bindable<int>(default_keys);
        private readonly Dictionary<string, Bindable<string>> textFields = new Dictionary<string, Bindable<string>>();
        private readonly Dictionary<string, Bindable<bool>> boolFields = new Dictionary<string, Bindable<bool>>();
        private readonly Dictionary<string, Bindable<Colour4>> colourFields = new Dictionary<string, Bindable<Colour4>>();

        private SettingsDropdown<int> keysDropdown = null!;
        private FillFlowContainer staticFields = null!;
        private FillFlowContainer perKeyFields = null!;

        public EzSkinEditorSkinIniManiaSection(EzSkinIniSession? session, EzSkinEditorComparisonSnapshot? comparisonSnapshot = null)
            : base(session, comparisonSnapshot)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Add(new OsuSpriteText
            {
                Text = EzEditorStrings.SKIN_INI_MANIA_HINT,
                Font = OsuFont.Default.With(size: 14),
                Colour = Color4.Gray,
            });

            Add(keysDropdown = new SettingsDropdown<int>
            {
                LabelText = EzEditorStrings.SKIN_INI_MANIA_KEYS_LABEL,
                Current = selectedKeys,
                Items = new[] { default_keys },
            });

            Add(staticFields = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(8),
            });

            Add(perKeyFields = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(8),
            });

            addFieldGroup(staticFields, EzEditorStrings.SKIN_INI_FIELD_GROUP_LAYOUT, EzSkinIniFieldCatalog.ManiaLayoutFields);
            addFieldGroup(staticFields, EzEditorStrings.SKIN_INI_FIELD_GROUP_POSITION, EzSkinIniFieldCatalog.ManiaPositionFields);
            addFieldGroup(staticFields, EzEditorStrings.SKIN_INI_FIELD_GROUP_DISPLAY, EzSkinIniFieldCatalog.ManiaDisplayFields);
            addFieldGroup(staticFields, EzEditorStrings.SKIN_INI_FIELD_GROUP_EXPLOSION, EzSkinIniFieldCatalog.ManiaExplosionFields);

            selectedKeys.ValueChanged += _ =>
            {
                if (!Applying)
                    reloadFromSession();
            };

            reloadFromSession();
        }

        private void addFieldGroup(FillFlowContainer container, LocalisableString title, IReadOnlyList<EzSkinIniFieldDefinition> fields)
        {
            container.Add(CreateSubheader(title));

            foreach (var field in fields)
            {
                switch (field.Kind)
                {
                    case EzSkinIniFieldKind.Text:
                        addTextField(container, field.Key, field.Label);
                        break;

                    case EzSkinIniFieldKind.Bool:
                        addBoolField(container, field.Key, field.Label);
                        break;

                    case EzSkinIniFieldKind.Colour:
                        addColourField(container, field.Key, field.Label);
                        break;
                }
            }
        }

        private void reloadFromSession()
        {
            WithApplying(() =>
            {
                var document = ParseDocument();
                var snapshotDocument = ParseSnapshotDocument();
                var keysList = document?.GetManiaKeys().ToList() ?? new List<int>();

                if (keysList.Count == 0)
                {
                    document?.EnsureManiaBlock(default_keys);
                    keysList.Add(default_keys);
                }

                keysDropdown.Items = keysList.OrderBy(k => k);

                if (!keysList.Contains(selectedKeys.Value))
                    selectedKeys.Value = keysList.Contains(default_keys) ? default_keys : keysList[0];

                int keys = selectedKeys.Value;
                rebuildPerKeyFields(keys);

                foreach (var (key, bindable) in textFields)
                {
                    string value = document?.GetManiaValue(keys, key) ?? string.Empty;
                    bindable.Value = value;
                    EzSkinIniBridge.SyncTextDefault(bindable, snapshotDocument?.GetManiaValue(keys, key), value);
                }

                foreach (var (key, bindable) in boolFields)
                {
                    bool value = document?.GetManiaValue(keys, key) == "1";
                    bindable.Value = value;
                    EzSkinIniBridge.SyncBoolDefault(bindable, snapshotDocument?.GetManiaValue(keys, key), value);
                }

                foreach (var (key, bindable) in colourFields)
                {
                    string? raw = document?.GetManiaValue(keys, key);
                    Colour4 value = raw != null && EzSkinIniColourFormat.TryParse(raw, out var colour) ? colour : Colour4.White;
                    bindable.Value = value;

                    string? snapshotRaw = snapshotDocument?.GetManiaValue(keys, key);
                    Colour4? snapshotColour = snapshotRaw != null && EzSkinIniColourFormat.TryParse(snapshotRaw, out var snapshotParsedColour)
                        ? snapshotParsedColour
                        : null;

                    EzSkinIniBridge.SyncColourDefault(bindable, snapshotColour, value);
                }
            });
        }

        private void rebuildPerKeyFields(int keys)
        {
            perKeyFields.Clear();
            perKeyFields.Add(CreateSubheader(EzEditorStrings.SKIN_INI_PER_KEY_COLOURS));

            foreach (string key in EzSkinIniFieldCatalog.GetManiaPerKeyColourKeys(keys))
                addColourField(perKeyFields, key, key);
        }

        private void addTextField(FillFlowContainer container, string key, string label)
        {
            if (!textFields.TryGetValue(key, out var bindable))
            {
                bindable = new Bindable<string>(string.Empty);
                textFields[key] = bindable;
                bindable.ValueChanged += e => CommitManiaField(selectedKeys.Value, key, e.NewValue);
            }

            container.Add(CreateTextField(label, bindable));
        }

        private void addBoolField(FillFlowContainer container, string key, string label)
        {
            if (!boolFields.TryGetValue(key, out var bindable))
            {
                bindable = new Bindable<bool>();
                boolFields[key] = bindable;
                bindable.ValueChanged += e => CommitManiaBoolField(selectedKeys.Value, key, e.NewValue);
            }

            container.Add(CreateBoolField(label, bindable));
        }

        private void addColourField(FillFlowContainer container, string key, string label)
        {
            if (!colourFields.TryGetValue(key, out var bindable))
            {
                bindable = new Bindable<Colour4>(Colour4.White);
                colourFields[key] = bindable;
                bindable.ValueChanged += e => CommitManiaColourField(selectedKeys.Value, key, e.NewValue);
            }

            container.Add(CreateColourField(label, bindable));
        }
    }
}
