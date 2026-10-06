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
        public static Texture? TryGetPreviewTexture(EzResourceStore provider, EzResourcePickerCategory category, string key)
        {
            switch (category)
            {
                case EzResourcePickerCategory.GameTheme:
                    return tryGameThemeJudgementPreview(provider, key);

                case EzResourcePickerCategory.NoteSet:
                    return tryNoteSetPreview(provider, key);

                case EzResourcePickerCategory.Stage:
                    return tryStagePreview(provider, key);

                default:
                    return null;
            }
        }

        // 预览缩略图走 Atlas 池（小图，与既有行为一致），帧上限 1 —— 只解一张，不为一格预览拉起整段动画。
        private static Texture? tryNoteSetPreview(EzResourceStore provider, string key)
        {
            Texture[] frames = provider.GetTextureFrames(new EzAnimationRequest
            {
                Path = $"note/{key}/whitenote",
                MaxFrames = 1,
                Usage = EzTextureUsage.Atlas,
            });

            return frames.Length > 0 ? frames[0] : null;
        }

        // 判定目录里取第一张图作预览：走层1 的图片清单，内置主题（图源在程序集里）也能取到，
        // 不再「枚举用户磁盘的首个 png 再拼名字」。
        private static Texture? tryGameThemeJudgementPreview(EzResourceStore provider, string key)
        {
            foreach (string imageKey in provider.ListImageKeys($"GameTheme/{key}/judgement"))
            {
                Texture? texture = provider.Get(imageKey, EzTextureUsage.Atlas);

                if (texture != null)
                    return texture;
            }

            return null;
        }

        private static Texture? tryStagePreview(EzResourceStore provider, string key)
        {
            const string groove_base = "GrooveLight";
            string basePath = $"Stage/{key}/Stage/{groove_base}";

            Texture[] frames = provider.GetTextureFrames(new EzAnimationRequest
            {
                Path = basePath,
                MaxFrames = 1,
                Usage = EzTextureUsage.Atlas,
                AllowSingleFallback = false,
            });

            if (frames.Length > 0)
                return frames[0];

            // 单张静态图与原实现一致走 Large：这类图可能超出图集页，进页只会被绕过并打日志。
            return provider.Get(basePath, EzTextureUsage.Large);
        }
    }
}
