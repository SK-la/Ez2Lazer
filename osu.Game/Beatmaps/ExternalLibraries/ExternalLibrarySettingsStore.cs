// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Platform;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    public sealed class ExternalLibrarySettingsStore
    {
        private const string settings_filename = "external_beatmap_libraries.json";

        /// <summary>
        /// Set by <see cref="ExternalBeatmapLibraryIndexer"/> for ruleset assemblies (e.g. BMS settings) to read/write config.
        /// </summary>
        public static ExternalLibrarySettingsStore? Instance { get; set; }

        private readonly Storage storage;

        public ExternalLibrarySettingsStore(Storage storage)
        {
            this.storage = storage;
        }

        public ExternalLibrarySettings Load()
        {
            try
            {
                using var stream = storage.GetStream(settings_filename);

                if (stream == null)
                    return new ExternalLibrarySettings();

                using var reader = new StreamReader(stream);
                var settings = JsonConvert.DeserializeObject<ExternalLibrarySettings>(reader.ReadToEnd()) ?? new ExternalLibrarySettings();
                settings.Normalise();
                return settings;
            }
            catch (Exception)
            {
                return new ExternalLibrarySettings();
            }
        }

        public void Save(ExternalLibrarySettings settings)
        {
            using var stream = storage.CreateFileSafely(settings_filename);
            using var writer = new StreamWriter(stream);
            writer.Write(JsonConvert.SerializeObject(settings, Formatting.Indented));
        }

        public void MarkPendingRescan()
        {
            var settings = Load();
            settings.PendingRescan = true;
            Save(settings);
        }
    }
}
