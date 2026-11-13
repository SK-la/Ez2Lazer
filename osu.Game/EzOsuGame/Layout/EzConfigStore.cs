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
using osu.Game.EzOsuGame.Edit.Settings;
using osu.Game.EzOsuGame.Mods;
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
        private int lastPreviewSave;
        private int lastBeatmapEditorSave;
        private bool suppressSave;

        public EzPreviewManiaSettings PreviewManiaSettings { get; }

        public BeatmapEditorSettings BeatmapEditor { get; }

        private readonly EzModLayoutDocument modLayout = new EzModLayoutDocument();

        public EzConfigStore(Storage gameStorage)
        {
            storage = gameStorage.GetStorageForDirectory(EzModifyPath.CONFIG_ROOT_PATH);
            PreviewManiaSettings = new EzPreviewManiaSettings();
            BeatmapEditor = new BeatmapEditorSettings();
            reloadPreview();
            reloadBeatmapEditor();
            reloadModLayout();

            PreviewManiaSettings.Style.BindValueChanged(_ => queuePreviewSave(), false);
            PreviewManiaSettings.NoteColour.BindValueChanged(_ => queuePreviewSave(), false);
            PreviewManiaSettings.HoldHeadColour.BindValueChanged(_ => queuePreviewSave(), false);
            PreviewManiaSettings.HoldBodyColour.BindValueChanged(_ => queuePreviewSave(), false);
            PreviewManiaSettings.LaneBackgroundColour.BindValueChanged(_ => queuePreviewSave(), false);
            PreviewManiaSettings.ThicknessScale.BindValueChanged(_ => queuePreviewSave(), false);

            foreach (var bindable in BeatmapEditor.Colouring.BeatLines.AllBindables)
                bindable.BindValueChanged(_ => queueBeatmapEditorSave(), false);
        }

        public void Save()
        {
            lock (saveLock)
            {
                if (suppressSave)
                    return;

                writePreviewUnlocked();
            }
        }

        internal void SaveBeatmapEditor()
        {
            lock (saveLock)
            {
                if (suppressSave)
                    return;

                writeBeatmapEditorUnlocked();
            }
        }

        public EzRulesetModLayout? GetModLayout(string ruleset)
        {
            if (string.IsNullOrEmpty(ruleset))
                return null;

            lock (saveLock)
                return modLayout.Rulesets.TryGetValue(ruleset, out var layout) ? layout : null;
        }

        public void SaveModLayout(string ruleset, EzRulesetModLayout layout)
        {
            if (string.IsNullOrEmpty(ruleset))
                return;

            lock (saveLock)
            {
                modLayout.Rulesets[ruleset] = layout;
                writeText(EzModLayoutDocument.FILENAME, JsonConvert.SerializeObject(modLayout, json_settings));
            }
        }

        private void reloadPreview()
        {
            lock (saveLock)
            {
                suppressSave = true;

                try
                {
                    string? json = readTextUnlocked(EzPreviewManiaSettings.FILENAME);

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
        }

        private void reloadModLayout()
        {
            lock (saveLock)
            {
                try
                {
                    string? json = readTextUnlocked(EzModLayoutDocument.FILENAME);

                    if (string.IsNullOrEmpty(json))
                        return;

                    var loaded = JsonConvert.DeserializeObject<EzModLayoutDocument>(json, json_settings);

                    if (loaded?.Rulesets == null)
                        return;

                    modLayout.Rulesets = loaded.Rulesets;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to load Ez mod layout {EzModLayoutDocument.FILENAME}");
                }
            }
        }

        private void reloadBeatmapEditor()
        {
            lock (saveLock)
            {
                suppressSave = true;

                try
                {
                    string? json = readTextUnlocked(BeatmapEditorSettings.FILENAME);

                    if (string.IsNullOrEmpty(json))
                        return;

                    JsonConvert.DeserializeObject<BeatmapEditorSettingsDocument>(json, json_settings)?.ApplyTo(BeatmapEditor);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to load Ez beatmap editor settings {BeatmapEditorSettings.FILENAME}");
                }
                finally
                {
                    suppressSave = false;
                }
            }
        }

        private void queuePreviewSave()
        {
            lock (saveLock)
            {
                if (suppressSave)
                    return;
            }

            int current = Interlocked.Increment(ref lastPreviewSave);

            Task.Delay(save_debounce_ms).ContinueWith(_ =>
            {
                if (current != lastPreviewSave)
                    return;

                lock (saveLock)
                {
                    if (suppressSave)
                        return;

                    writePreviewUnlocked();
                }
            });
        }

        private void queueBeatmapEditorSave()
        {
            lock (saveLock)
            {
                if (suppressSave)
                    return;
            }

            int current = Interlocked.Increment(ref lastBeatmapEditorSave);

            Task.Delay(save_debounce_ms).ContinueWith(_ =>
            {
                if (current != lastBeatmapEditorSave)
                    return;

                lock (saveLock)
                {
                    if (suppressSave)
                        return;

                    writeBeatmapEditorUnlocked();
                }
            });
        }

        private void writePreviewUnlocked()
        {
            writeText(EzPreviewManiaSettings.FILENAME, JsonConvert.SerializeObject(EzPreviewManiaSettingsDocument.From(PreviewManiaSettings), json_settings));
        }

        private void writeBeatmapEditorUnlocked()
        {
            writeText(BeatmapEditorSettings.FILENAME, JsonConvert.SerializeObject(BeatmapEditorSettingsDocument.From(BeatmapEditor), json_settings));
        }

        private string? readTextUnlocked(string filename)
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
