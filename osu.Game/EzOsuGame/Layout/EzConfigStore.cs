// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Overlays.Preview;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Global JSON on <see cref="EzModifyPath.CONFIG_ROOT_PATH"/>. Not switched by theme <c>UseSet</c>.
    /// </summary>
    public sealed class EzConfigStore
    {
        private const int save_debounce_ms = 200;

        private static readonly JsonSerializerSettings json_settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
        };

        private readonly Storage storage;
        private readonly Lock saveLock = new Lock();
        private int lastSave;
        private bool suppressSave;

        public EzPreviewManiaSettings PreviewManiaSettings { get; }

        public EzConfigStore(Storage gameStorage)
        {
            storage = gameStorage.GetStorageForDirectory(EzModifyPath.CONFIG_ROOT_PATH);
            PreviewManiaSettings = new EzPreviewManiaSettings();
            reloadPreview();

            PreviewManiaSettings.Style.BindValueChanged(_ => queueSave(), false);
            PreviewManiaSettings.NoteColour.BindValueChanged(_ => queueSave(), false);
            PreviewManiaSettings.HoldHeadColour.BindValueChanged(_ => queueSave(), false);
            PreviewManiaSettings.HoldBodyColour.BindValueChanged(_ => queueSave(), false);
            PreviewManiaSettings.LaneBackgroundColour.BindValueChanged(_ => queueSave(), false);
            PreviewManiaSettings.ThicknessScale.BindValueChanged(_ => queueSave(), false);
        }

        public void Save()
        {
            lock (saveLock)
            {
                writeText(EzPreviewManiaSettings.FILENAME, JsonConvert.SerializeObject(EzPreviewManiaSettingsDocument.From(PreviewManiaSettings), json_settings));
            }
        }

        private void reloadPreview()
        {
            suppressSave = true;

            try
            {
                string? json = readText(EzPreviewManiaSettings.FILENAME);

                if (string.IsNullOrEmpty(json))
                    return;

                PreviewManiaSettings.ApplyDocument(JsonConvert.DeserializeObject<EzPreviewManiaSettingsDocument>(json, json_settings));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load Ez full map preview settings {EzPreviewManiaSettings.FILENAME}");
            }
            finally
            {
                suppressSave = false;
            }
        }

        private void queueSave()
        {
            if (suppressSave)
                return;

            int current = Interlocked.Increment(ref lastSave);

            Task.Delay(save_debounce_ms).ContinueWith(_ =>
            {
                if (current == lastSave)
                    Save();
            });
        }

        private string? readText(string filename)
        {
            if (!storage.Exists(filename))
                return null;

            try
            {
                using (var stream = storage.GetStream(filename))
                {
                    if (stream == null)
                        return null;

                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                        return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to read Ez config {filename}");
                return null;
            }
        }

        private void writeText(string filename, string contents)
        {
            using (var stream = storage.CreateFileSafely(filename))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
                writer.Write(contents);
        }
    }
}
