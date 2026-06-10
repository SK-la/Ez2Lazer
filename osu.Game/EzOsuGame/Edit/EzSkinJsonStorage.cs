// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Text;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    public static class EzSkinJsonStorage
    {
        public static string? TryReadJson(SkinManager skinManager, SkinInfo skin)
        {
            var existingFile = skin.GetFile(EzSkinJsonDocument.FILENAME);

            if (existingFile == null)
                return null;

            var skinFiles = ((IStorageResourceProvider)skinManager).Files;

            using (var stream = skinFiles.GetStream(existingFile.File.GetStoragePath()))
            {
                if (stream == null)
                    return null;

                using (var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true))
                    return reader.ReadToEnd();
            }
        }

        public static string ExportToStorage(Storage exportStorage, Ez2ConfigManager config, string skinName)
        {
            string filename = $"{sanitizeFileName(skinName)}-EzSkin.json";
            var document = EzSkinJsonBridge.Capture(config);

            using (var stream = exportStorage.CreateFileSafely(filename))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
                writer.Write(document.Serialize());

            return exportStorage.GetFullPath(filename);
        }

        private static string sanitizeFileName(string skinName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                skinName = skinName.Replace(c, '_');

            return string.IsNullOrWhiteSpace(skinName) ? "skin" : skinName;
        }
    }
}
