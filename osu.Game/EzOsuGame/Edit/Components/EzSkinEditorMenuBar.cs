// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Screens.Edit.Components.Menus;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Top menu bar for Ez skin editor, matching <see cref="SkinEditor"/> <see cref="EditorMenuBar"/> layout.
    /// </summary>
    public partial class EzSkinEditorMenuBar : EditorMenuBar
    {
        public const float HEIGHT = SkinEditor.MENU_HEIGHT;

        public Action? ApplyAction { get; set; }

        public Action? ExitAction { get; set; }

        public Action? CreateEzSkinJsonAction { get; set; }

        public Action? UpdateEzSkinJsonSnapshotAction { get; set; }

        public Action? RemoveEzSkinJsonAction { get; set; }

        public Action? ImportJsonAction { get; set; }

        public Action? ImportFromSkinJsonAction { get; set; }

        public Action? ExportJsonAction { get; set; }

        public Action? WriteColoursToSkinIniAction { get; set; }

        public Action? WriteSizesToSkinIniAction { get; set; }

        public Action? ExportOskAction { get; set; }

        public Action? CreateConfigSnapshotAction { get; set; }

        public Action? RestoreConfigSnapshotAction { get; set; }

        public Action? ExportPreviewImageAction { get; set; }

        public Func<bool>? CanCreateEzSkinJson { get; set; }

        public Func<bool>? CanUpdateEzSkinJsonSnapshot { get; set; }

        public Func<bool>? CanRemoveEzSkinJson { get; set; }

        public Func<bool>? CanImportFromSkinJson { get; set; }

        public Func<bool>? CanWriteColoursToSkinIni { get; set; }

        public Func<bool>? CanWriteSizesToSkinIni { get; set; }

        public Func<bool>? CanExportOsk { get; set; }

        public Func<bool>? CanExportPreviewImage { get; set; }

        public Func<bool>? CanUseConfigSnapshot { get; set; }

        private readonly EditorMenuItem createConfigSnapshotItem;
        private readonly EditorMenuItem restoreConfigSnapshotItem;
        private readonly EditorMenuItem exportPreviewImageItem;
        private readonly EditorMenuItem createJsonItem;
        private readonly EditorMenuItem updateSnapshotItem;
        private readonly EditorMenuItem removeJsonItem;
        private readonly EditorMenuItem importJsonItem;
        private readonly EditorMenuItem importFromSkinItem;
        private readonly EditorMenuItem exportJsonItem;
        private readonly EditorMenuItem writeColoursItem;
        private readonly EditorMenuItem writeSizesItem;
        private readonly EditorMenuItem exportOskItem;

        public EzSkinEditorMenuBar()
        {
            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;
            RelativeSizeAxes = Axes.Both;

            Items = new[]
            {
                new MenuItem(CommonStrings.MenuBarFile)
                {
                    Items = new OsuMenuItem[]
                    {
                        new EditorMenuItem(EzEditorStrings.MENU_APPLY, MenuItemType.Standard, () => ApplyAction?.Invoke()),
                        new OsuMenuItemSpacer(),
                        exportOskItem = new EditorMenuItem(EzEditorStrings.MENU_EXPORT_OSK, MenuItemType.Standard, () => ExportOskAction?.Invoke()),
                        new OsuMenuItemSpacer(),
                        new EditorMenuItem(CommonStrings.Exit, MenuItemType.Standard, () => ExitAction?.Invoke()),
                    },
                },
                new MenuItem(EzEditorStrings.MENU_CONFIG)
                {
                    Items = new OsuMenuItem[]
                    {
                        createConfigSnapshotItem = new EditorMenuItem(EzEditorStrings.MENU_CREATE_CONFIG_SNAPSHOT, MenuItemType.Standard, () => CreateConfigSnapshotAction?.Invoke()),
                        restoreConfigSnapshotItem = new EditorMenuItem(EzEditorStrings.MENU_RESTORE_CONFIG_SNAPSHOT, MenuItemType.Standard, () => RestoreConfigSnapshotAction?.Invoke()),
                        new OsuMenuItemSpacer(),
                        exportPreviewImageItem = new EditorMenuItem(EzEditorStrings.MENU_EXPORT_PREVIEW_IMAGE, MenuItemType.Standard, () => ExportPreviewImageAction?.Invoke()),
                        new OsuMenuItemSpacer(),
                        createJsonItem = new EditorMenuItem(EzEditorStrings.MENU_CREATE_EZSKIN_JSON, MenuItemType.Standard, () => CreateEzSkinJsonAction?.Invoke()),
                        updateSnapshotItem = new EditorMenuItem(EzEditorStrings.MENU_UPDATE_EZSKIN_JSON_SNAPSHOT, MenuItemType.Standard, () => UpdateEzSkinJsonSnapshotAction?.Invoke()),
                        removeJsonItem = new EditorMenuItem(EzEditorStrings.MENU_REMOVE_EZSKIN_JSON, MenuItemType.Standard, () => RemoveEzSkinJsonAction?.Invoke()),
                        new OsuMenuItemSpacer(),
                        importJsonItem = new EditorMenuItem(EzEditorStrings.MENU_IMPORT_JSON, MenuItemType.Standard, () => ImportJsonAction?.Invoke()),
                        importFromSkinItem = new EditorMenuItem(EzEditorStrings.MENU_IMPORT_FROM_SKIN_JSON, MenuItemType.Standard, () => ImportFromSkinJsonAction?.Invoke()),
                        exportJsonItem = new EditorMenuItem(EzEditorStrings.MENU_EXPORT_JSON, MenuItemType.Standard, () => ExportJsonAction?.Invoke()),
                        new OsuMenuItemSpacer(),
                        writeColoursItem = new EditorMenuItem(EzEditorStrings.MENU_WRITE_COLOURS_TO_SKIN_INI, MenuItemType.Standard, () => WriteColoursToSkinIniAction?.Invoke()),
                        writeSizesItem = new EditorMenuItem(EzEditorStrings.MENU_WRITE_SIZES_TO_SKIN_INI, MenuItemType.Standard, () => WriteSizesToSkinIniAction?.Invoke()),
                    },
                },
            };
        }

        public void RefreshMenuState()
        {
            bool canUseConfigSnapshot = CanUseConfigSnapshot?.Invoke() ?? true;
            createConfigSnapshotItem.Action.Disabled = !canUseConfigSnapshot;
            restoreConfigSnapshotItem.Action.Disabled = !canUseConfigSnapshot;
            exportPreviewImageItem.Action.Disabled = !(CanExportPreviewImage?.Invoke() ?? false);
            createJsonItem.Action.Disabled = !(CanCreateEzSkinJson?.Invoke() ?? false);
            updateSnapshotItem.Action.Disabled = !(CanUpdateEzSkinJsonSnapshot?.Invoke() ?? false);
            removeJsonItem.Action.Disabled = !(CanRemoveEzSkinJson?.Invoke() ?? false);
            importFromSkinItem.Action.Disabled = !(CanImportFromSkinJson?.Invoke() ?? false);
            writeColoursItem.Action.Disabled = !(CanWriteColoursToSkinIni?.Invoke() ?? false);
            writeSizesItem.Action.Disabled = !(CanWriteSizesToSkinIni?.Invoke() ?? false);
            exportOskItem.Action.Disabled = !(CanExportOsk?.Invoke() ?? false);
            importJsonItem.Action.Disabled = false;
            exportJsonItem.Action.Disabled = false;
        }
    }
}
