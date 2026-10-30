// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorTextureSettingsSection : FillFlowContainer
    {
        private readonly List<string> availableGameThemes = new List<string>();

        private SettingsDropdown<string>? gameThemeDropdown;
        private Bindable<string>? nameOfGameTheme;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        public EzSkinEditorTextureSettingsSection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new[]
            {
                createGameThemeGroup(),
                createStageDropdown(),
                createNoteDropdown(),
                createPanelDropdown(),
                createColumnDropdown(),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            EzGameThemeCandidates.Changed += onGameThemeCandidatesChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            EzGameThemeCandidates.Changed -= onGameThemeCandidatesChanged;
            base.Dispose(isDisposing);
        }

        private Drawable createGameThemeGroup()
        {
            nameOfGameTheme = ezSkinConfig.GetBindable<string>(Ez2Setting.GameThemeName);
            EzGameThemeCandidates.Refresh(storage);
            EzGameThemeCandidates.EnsureInitialSelection(nameOfGameTheme);
            refreshGameThemeDropdownItems();

            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(4),
                Children = new Drawable[]
                {
                    gameThemeDropdown = new SettingsDropdown<string>
                    {
                        LabelText = EzSkinStrings.GLOBAL_TEXTURE_NAME,
                        TooltipText = EzSkinStrings.GLOBAL_TEXTURE_NAME_TOOLTIP,
                        Current = nameOfGameTheme,
                        Items = availableGameThemes,
                    },
                    new SettingsButton
                    {
                        Text = EzSkinStrings.RESCAN_GAME_THEME_LIST,
                        Action = () => EzGameThemeCandidates.Refresh(storage),
                    },
                }
            };
        }

        private Drawable createStageDropdown()
        {
            var bindable = ezSkinConfig.GetBindable<string>(Ez2Setting.StageName);
            var items = EzResourceDiscovery.ListStageCandidates(storage).ToList();
            ensureDropdownItems(bindable, items);

            return new SettingsDropdown<string>
            {
                LabelText = EzSkinStrings.STAGE_SET,
                TooltipText = EzSkinStrings.STAGE_SET_TOOLTIP,
                Current = bindable,
                Items = items,
            };
        }

        private Drawable createNoteDropdown()
        {
            var bindable = ezSkinConfig.GetBindable<string>(Ez2Setting.NoteSetName);
            var items = EzResourceDiscovery.ListNoteSetCandidates(storage).ToList();
            ensureDropdownItems(bindable, items);

            return new SettingsDropdown<string>
            {
                LabelText = EzSkinStrings.NOTE_SET,
                TooltipText = EzSkinStrings.NOTE_SET_TOOLTIP,
                Current = bindable,
                Items = items,
            };
        }

        private Drawable createPanelDropdown()
        {
            var bindable = ezSkinConfig.GetBindable<string>(Ez2Setting.StageBackground);

            var items = new List<string> { string.Empty };
            items.AddRange(EzResourceDiscovery.ListPanelImages(storage));

            if (!string.IsNullOrEmpty(bindable.Value) && !items.Contains(bindable.Value))
                items.Insert(1, bindable.Value);

            return new EzOptionalStringDropdown
            {
                LabelText = EzColumnStrings.STAGE_BACKGROUND,
                TooltipText = EzColumnStrings.STAGE_BACKGROUND_TOOLTIP,
                EmptyLabel = "None",
                Current = bindable,
                Items = items,
            };
        }

        private Drawable createColumnDropdown()
        {
            var bindable = ezSkinConfig.GetBindable<string>(Ez2Setting.ColumnLightName);

            var items = EzResourceDiscovery.ListColumnSets(storage).ToList();

            if (!items.Contains("EzColumnLight"))
                items.Insert(0, "EzColumnLight");

            if (!string.IsNullOrEmpty(bindable.Value) && !items.Contains(bindable.Value))
                items.Insert(0, bindable.Value);

            return new SettingsDropdown<string>
            {
                LabelText = EzColumnStrings.COLUMN_SET,
                TooltipText = EzColumnStrings.COLUMN_SET_TOOLTIP,
                Current = bindable,
                Items = items,
            };
        }

        private void onGameThemeCandidatesChanged() => refreshGameThemeDropdownItems();

        private void refreshGameThemeDropdownItems()
        {
            if (nameOfGameTheme == null)
                return;

            availableGameThemes.Clear();
            availableGameThemes.AddRange(EzGameThemeCandidates.CreateDropdownItems(nameOfGameTheme));

            gameThemeDropdown?.Items = availableGameThemes;
        }

        // 非空且缺失的当前值置顶补入；列表为空兜底 string.Empty；当前值为空时写回首项，作为新装默认值。
        private static void ensureDropdownItems(Bindable<string> current, List<string> items)
        {
            if (!string.IsNullOrEmpty(current.Value) && !items.Contains(current.Value))
                items.Insert(0, current.Value);

            if (items.Count == 0)
                items.Add(string.Empty);

            if (string.IsNullOrEmpty(current.Value))
                current.Value = items[0];
        }
    }
}
