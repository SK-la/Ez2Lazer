// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Wiki
{
    internal static class EzWikiLocalePagesCache
    {
        private const string cache_relative_path = "EzData/ez-wiki-locale-pages.json";

        public static bool TryRead(Storage storage, out string json)
        {
            json = string.Empty;

            try
            {
                string path = storage.GetFullPath(cache_relative_path, createIfNotExisting: false);
                if (!File.Exists(path))
                    return false;

                json = File.ReadAllText(path);
                return !string.IsNullOrWhiteSpace(json);
            }
            catch (Exception ex)
            {
                Logger.Log($"Ez wiki locale-pages cache read failed: {ex.Message}", level: LogLevel.Debug);
                return false;
            }
        }

        public static void Write(Storage storage, string json)
        {
            try
            {
                string path = storage.GetFullPath(cache_relative_path, createIfNotExisting: true);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Logger.Log($"Ez wiki locale-pages cache write failed: {ex.Message}", level: LogLevel.Debug);
            }
        }
    }
}
