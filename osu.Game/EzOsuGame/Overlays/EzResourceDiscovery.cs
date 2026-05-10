// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.HUD;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// EzResources 下列目录与预览纹理键解析。
    /// </summary>
    public static class EzResourceDiscovery
    {
        public static IReadOnlyList<string> ListGameThemeCandidates(Storage storage)
        {
            var list = new List<string>();
            string basePath = storage.GetFullPath(EzModifyPath.GAME_THEME_PATH);

            if (!Directory.Exists(basePath))
                return list;

            foreach (EzEnumGameThemeName v in Enum.GetValues(typeof(EzEnumGameThemeName)))
            {
                string name = v.ToString();
                if (Directory.Exists(Path.Combine(basePath, name)))
                    list.Add(name);
            }

            return list;
        }

        public static IReadOnlyList<string> ListNoteSetCandidates(Storage storage)
            => listSubdirectories(storage, EzModifyPath.NOTE_PATH);

        public static IReadOnlyList<string> ListStageCandidates(Storage storage)
            => listSubdirectories(storage, EzModifyPath.STAGE_PATH);

        private static List<string> listSubdirectories(Storage storage, string relativePath)
        {
            var list = new List<string>();

            try
            {
                string path = storage.GetFullPath(relativePath);

                if (!Directory.Exists(path))
                    return list;

                list.AddRange(
                    Directory.GetDirectories(path)
                             .Select(Path.GetFileName)
                             .Where(n => !string.IsNullOrEmpty(n))!);
            }
            catch
            {
            }

            return list;
        }

        /// <summary>
        /// 获取缩略图纹理（可能为 null，调用方使用占位）。
        /// </summary>
        public static Texture? TryGetPreviewTexture(EzResourceStore provider, Storage storage, EzResourcePickerCategory category, string key)
        {
            switch (category)
            {
                case EzResourcePickerCategory.GameTheme:
                    return tryGameThemeJudgementPreview(provider, storage, key);

                case EzResourcePickerCategory.NoteSet:
                    return provider.Get($"note/{key}/whitenote/000") ?? provider.Get($"note/{key}/whitenote/001");

                case EzResourcePickerCategory.Stage:
                    return tryStagePreview(provider, key);

                default:
                    return null;
            }
        }

        private static Texture? tryGameThemeJudgementPreview(EzResourceStore provider, Storage storage, string key)
        {
            string judgementDir = Path.Combine(storage.GetFullPath(EzModifyPath.GAME_THEME_PATH), key, "judgement");

            if (!Directory.Exists(judgementDir))
                return null;

            string? first = Directory.EnumerateFiles(judgementDir, "*.png", SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (string.IsNullOrEmpty(first))
                return null;

            string fileName = Path.GetFileNameWithoutExtension(first);
            return provider.Get($@"GameTheme/{key}/judgement/{fileName}");
        }

        private static Texture? tryStagePreview(EzResourceStore provider, string key)
        {
            const string groove_base = "GrooveLight";

            for (int i = 0; i < 8; i++)
            {
                var t = provider.Get($"Stage/{key}/Stage/{groove_base}_{i}");
                if (t != null)
                    return t;
            }

            return provider.Get($"Stage/{key}/Stage/{groove_base}", useLargeStore: true);
        }
    }
}
