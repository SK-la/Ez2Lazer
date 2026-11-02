// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    /// <summary>
    /// 全谱预览外观的读写。独立于 <c>EzSkinSettings.ini</c> 与 skin.ini，存放在
    /// <c>EzResources/Config</c> 下与 layout 同级。
    /// </summary>
    public sealed class EzPreviewManiaSettingsStore
    {
        private static readonly JsonSerializerSettings json_settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
        };

        private readonly Storage storage;

        public EzPreviewManiaSettingsStore(Storage gameStorage)
        {
            storage = gameStorage.GetStorageForDirectory(EzModifyPath.CONFIG_ROOT_PATH);
        }

        public EzPreviewManiaSettings Load()
        {
            var settings = new EzPreviewManiaSettings();

            if (!storage.Exists(EzPreviewManiaSettings.FILENAME))
                return settings;

            try
            {
                using var stream = storage.GetStream(EzPreviewManiaSettings.FILENAME);

                if (stream == null)
                    return settings;

                using var reader = new StreamReader(stream, Encoding.UTF8);
                string json = reader.ReadToEnd();
                var document = JsonConvert.DeserializeObject<EzPreviewManiaSettingsDocument>(json, json_settings);
                document?.ApplyTo(settings);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load Ez full map preview settings {EzPreviewManiaSettings.FILENAME}");
            }

            return settings;
        }

        public void Save(EzPreviewManiaSettings settings)
        {
            string json = JsonConvert.SerializeObject(EzPreviewManiaSettingsDocument.From(settings), json_settings);

            using var stream = storage.CreateFileSafely(EzPreviewManiaSettings.FILENAME);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(json);
        }
    }
}
