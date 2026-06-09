// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorTextureSettingsSection : FillFlowContainer
    {
        private readonly List<string> availableNoteSets = new List<string>();
        private readonly List<string> availableStageSets = new List<string>();

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
            loadFolderSets("note", availableNoteSets);
            loadFolderSets("Stage", availableStageSets);

            var nameOfNote = ezSkinConfig.GetBindable<string>(Ez2Setting.NoteSetName);
            var nameOfStage = ezSkinConfig.GetBindable<string>(Ez2Setting.StageName);

            ensureDropdownItems(nameOfNote, availableNoteSets);
            ensureDropdownItems(nameOfStage, availableStageSets);
            var nameOfGameTheme = ezSkinConfig.GetBindable<EzEnumGameThemeName>(Ez2Setting.GameThemeName);

            Children = new Drawable[]
            {
                new SettingsEnumDropdown<EzEnumGameThemeName>
                {
                    LabelText = EzSkinStrings.GLOBAL_TEXTURE_NAME,
                    TooltipText = EzSkinStrings.GLOBAL_TEXTURE_NAME_TOOLTIP,
                    Current = nameOfGameTheme,
                },
                new SettingsDropdown<string>
                {
                    LabelText = EzSkinStrings.STAGE_SET,
                    TooltipText = EzSkinStrings.STAGE_SET_TOOLTIP,
                    Current = nameOfStage,
                    Items = availableStageSets,
                },
                new SettingsDropdown<string>
                {
                    LabelText = EzSkinStrings.NOTE_SET,
                    TooltipText = EzSkinStrings.NOTE_SET_TOOLTIP,
                    Current = nameOfNote,
                    Items = availableNoteSets,
                },
            };
        }

        private static void ensureDropdownItems(Bindable<string> current, List<string> items)
        {
            if (!string.IsNullOrEmpty(current.Value) && !items.Contains(current.Value))
                items.Insert(0, current.Value);

            if (items.Count == 0)
                items.Add(string.Empty);

            if (string.IsNullOrEmpty(current.Value))
                current.Value = items[0];
        }

        private void loadFolderSets(string type, List<string> targetList)
        {
            targetList.Clear();

            string? relativePath = type.Equals("note", StringComparison.OrdinalIgnoreCase)
                ? EzModifyPath.NOTE_PATH
                : type.Equals("Stage", StringComparison.OrdinalIgnoreCase)
                    ? EzModifyPath.STAGE_PATH
                    : null;

            if (relativePath == null)
                return;

            try
            {
                string dataFolderPath = storage.GetFullPath(relativePath);

                if (!Directory.Exists(dataFolderPath))
                    Directory.CreateDirectory(dataFolderPath);

                targetList.AddRange(Directory.GetDirectories(dataFolderPath).Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name))!);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"EzSkinEditorTextureSettingsSection load {type} failed");
            }
        }
    }
}
