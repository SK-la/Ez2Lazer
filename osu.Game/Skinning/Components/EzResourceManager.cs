// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Skinning.Components
{
    /// <summary>
    /// 全局EZ GameTheme 资源管理器，负责加载和管理EZ皮肤 GameTheme 资源文件夹
    /// </summary>
    [Cached]
    public static class EzResourceManager
    {
        /// <summary>
        /// 当 GameTheme 资源重新加载时触发的事件
        /// </summary>
        public static event Action? OnGameThemesReloaded;

        private static readonly string gametheme_path = @"EzResources\GameTheme";

        public static List<string> AvailableGameThemes { get; } = new List<string>();

        private static bool isLoaded;

        /// <summary>
        /// 加载 GameTheme 资源文件夹
        /// </summary>
        public static void LoadResources(Storage storage)
        {
            if (isLoaded) return;

            loadGameThemes(storage);

            isLoaded = true;
            Logger.Log("EzResourceManager: GameTheme resources loaded successfully", LoggingTarget.Runtime, LogLevel.Debug);
        }

        /// <summary>
        /// 重新加载 GameTheme 资源（用于刷新）
        /// </summary>
        public static void ReloadResources(Storage storage)
        {
            AvailableGameThemes.Clear();
            isLoaded = false;
            LoadResources(storage);
            OnGameThemesReloaded?.Invoke();
        }

        private static void loadGameThemes(Storage storage)
        {
            try
            {
                string? dataFolderPath = storage.GetFullPath(gametheme_path);

                if (!Directory.Exists(dataFolderPath))
                {
                    Directory.CreateDirectory(dataFolderPath);
                    Logger.Log($"EzResourceManager: Created GameTheme directory: {dataFolderPath}", LoggingTarget.Runtime);
                }

                string[] directories = Directory.GetDirectories(dataFolderPath);
                AvailableGameThemes.AddRange(directories.Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name))!);

                Logger.Log($"EzResourceManager: Found {AvailableGameThemes.Count} GameTheme sets in {dataFolderPath}", LoggingTarget.Runtime, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "EzResourceManager: Load GameTheme FolderSets Error");
            }
        }
    }
}
