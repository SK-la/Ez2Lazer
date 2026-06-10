// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Extensions;
using osu.Game.EzOsuGame.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorColumnColourSettingsSection : FillFlowContainer
    {
        private static readonly List<int> available_key_modes = new List<int> { 0, 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18 };

        private readonly EzSkinEditorPreviewState? previewState;

        private readonly Dictionary<int, List<EzSelectorColour>> columnSelectorCache = new Dictionary<int, List<EzSelectorColour>>();
        private readonly Dictionary<Ez2Setting, BindableColour4> colorBindables = new Dictionary<Ez2Setting, BindableColour4>();

        private FillFlowContainer columnsContainer = null!;
        private Bindable<int> columnTypeListSelectBindable = null!;
        private Bindable<bool> colorSettingsEnabled = null!;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        public EzSkinEditorColumnColourSettingsSection(EzSkinEditorPreviewState? previewState = null)
        {
            this.previewState = previewState;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            colorSettingsEnabled = ezSkinConfig.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);
            columnTypeListSelectBindable = ezSkinConfig.GetBindable<int>(Ez2Setting.ColumnTypeListSelect);

            colorBindables[Ez2Setting.ColumnTypeA] = createColorBindable(Ez2Setting.ColumnTypeA);
            colorBindables[Ez2Setting.ColumnTypeB] = createColorBindable(Ez2Setting.ColumnTypeB);
            colorBindables[Ez2Setting.ColumnTypeS] = createColorBindable(Ez2Setting.ColumnTypeS);
            colorBindables[Ez2Setting.ColumnTypeE] = createColorBindable(Ez2Setting.ColumnTypeE);
            colorBindables[Ez2Setting.ColumnTypeP] = createColorBindable(Ez2Setting.ColumnTypeP);

            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = EzEditorStrings.KEY_MODE_LABEL,
                    Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 14),
                }.WithUnderline(),
                new SettingsDropdown<int>
                {
                    Current = columnTypeListSelectBindable,
                    Items = available_key_modes,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 2,
                    Colour = Color4.DarkGray.Opacity(0.5f),
                },
                columnsContainer = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(2),
                },
            };

            updateKeyModeFromPreview();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            columnTypeListSelectBindable.BindValueChanged(e => updateColumnsType(e.NewValue), true);

            colorSettingsEnabled.BindValueChanged(e => onColorSettingsEnabledChanged(e.NewValue), true);

            colorBindables[Ez2Setting.ColumnTypeA].BindValueChanged(e => updateBaseColour(e.NewValue, EzConstants.COLUMN_TYPE_A));
            colorBindables[Ez2Setting.ColumnTypeB].BindValueChanged(e => updateBaseColour(e.NewValue, EzConstants.COLUMN_TYPE_B));
            colorBindables[Ez2Setting.ColumnTypeS].BindValueChanged(e => updateBaseColour(e.NewValue, EzConstants.COLUMN_TYPE_S));
            colorBindables[Ez2Setting.ColumnTypeE].BindValueChanged(e => updateBaseColour(e.NewValue, EzConstants.COLUMN_TYPE_E));
            colorBindables[Ez2Setting.ColumnTypeP].BindValueChanged(e => updateBaseColour(e.NewValue, EzConstants.COLUMN_TYPE_P));

            previewState?.SuggestedKeyMode.BindValueChanged(e => updateKeyModeFromPreview(), true);
        }

        protected override void Dispose(bool isDisposing)
        {
            columnTypeListSelectBindable.UnbindAll();
            colorSettingsEnabled.UnbindAll();

            foreach (var bindable in colorBindables.Values)
                bindable.UnbindAll();

            previewState?.SuggestedKeyMode.UnbindAll();
            base.Dispose(isDisposing);
        }

        private void onColorSettingsEnabledChanged(bool enabled)
        {
            if (enabled)
            {
                columnsContainer.Show();
                updateColumnsType(columnTypeListSelectBindable.Value);
            }
            else
            {
                columnsContainer.Hide();
            }
        }

        private BindableColour4 createColorBindable(Ez2Setting setting)
        {
            var result = new BindableColour4();
            ezSkinConfig.BindWith(setting, result);
            return result;
        }

        private void updateBaseColour(Color4 newColor, string type)
        {
            if (!colorSettingsEnabled.Value)
                return;

            foreach (var selector in columnsContainer.ChildrenOfType<EzSelectorColour>())
                selector.SetColorMapping(type, newColor);
        }

        private void updateKeyModeFromPreview()
        {
            int? suggested = previewState?.SuggestedKeyMode.Value;

            if (suggested == null || !available_key_modes.Contains(suggested.Value))
                return;

            columnTypeListSelectBindable.Value = suggested.Value;
        }

        private void updateColumnsType(int keyModeForList)
        {
            if (!IsLoaded)
                return;

            Schedule(() => updateColumnsTypeInternal(keyModeForList));
        }

        private void updateColumnsTypeInternal(int keyModeForList)
        {
            if (!IsLoaded || !colorSettingsEnabled.Value)
                return;

            if (columnSelectorCache.TryGetValue(keyModeForList, out var cachedSelectors))
            {
                columnsContainer.Clear(false);
                columnsContainer.AddRange(cachedSelectors);

                var colorMapping = createColorMapping();

                foreach (var selector in cachedSelectors)
                    selector.UpdateColorMapping(colorMapping);

                return;
            }

            columnsContainer.Clear(false);

            if (keyModeForList == 0 || !available_key_modes.Contains(keyModeForList))
            {
                columnsContainer.Add(new OsuSpriteText
                {
                    Text = EzEditorStrings.SELECT_KEY_MODE_FIRST,
                    Font = OsuFont.GetFont(weight: FontWeight.Bold),
                    Margin = new MarginPadding(5f),
                });
                return;
            }

            createColumnSelectors(keyModeForList);
        }

        private void createColumnSelectors(int keyMode)
        {
            columnsContainer.Add(new OsuSpriteText
            {
                Text = LocalisableString.Format(EzEditorStrings.COLUMN_TYPE_HEADER, keyMode),
                Font = OsuFont.GetFont(weight: FontWeight.SemiBold),
                Margin = new MarginPadding { Bottom = 5 },
            }.WithUnderline());

            var newSelectors = new List<EzSelectorColour>();
            var colorMapping = createColorMapping();
            string[] columnTypes = Enum.GetNames(typeof(EzColumnType));

            for (int i = 0; i < keyMode; i++)
            {
                var selector = createColumnSelector(keyMode, i, columnTypes, colorMapping);
                columnsContainer.Add(selector);
                newSelectors.Add(selector);
            }

            columnSelectorCache[keyMode] = newSelectors;
        }

        private Dictionary<string, Color4> createColorMapping() => new Dictionary<string, Color4>
        {
            [EzConstants.COLUMN_TYPE_A] = colorBindables[Ez2Setting.ColumnTypeA].Value,
            [EzConstants.COLUMN_TYPE_B] = colorBindables[Ez2Setting.ColumnTypeB].Value,
            [EzConstants.COLUMN_TYPE_S] = colorBindables[Ez2Setting.ColumnTypeS].Value,
            [EzConstants.COLUMN_TYPE_E] = colorBindables[Ez2Setting.ColumnTypeE].Value,
            [EzConstants.COLUMN_TYPE_P] = colorBindables[Ez2Setting.ColumnTypeP].Value,
        };

        private EzSelectorColour createColumnSelector(int keyMode, int columnIndex, string[] columnTypes, Dictionary<string, Color4> colorMapping)
        {
            EzColumnType savedType = ezSkinConfig.GetColumnTypeFast(keyMode, columnIndex);

            var selector = new EzSelectorColour($"Column {columnIndex + 1}", columnTypes, colorMapping);
            selector.Current.Value = savedType.ToString();

            selector.Current.ValueChanged += e =>
            {
                if (Enum.TryParse<EzColumnType>(e.NewValue, out var type))
                    ezSkinConfig.SetColumnType(keyMode, columnIndex, type);
            };

            return selector;
        }
    }
}
