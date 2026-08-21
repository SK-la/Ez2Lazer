// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Pets
{
    public static partial class EzPetLive2DAccess
    {
        public const string PRESETS_FILE = "_official_live2d_presets.json";

        /// <summary>
        /// Loads whitelist from <c>EzResources/Pets/_official_live2d_presets.json</c> (pack → sha256).
        /// Missing file leaves the in-memory map unchanged (tests may set hashes explicitly).
        /// </summary>
        public static void LoadPresetsFromStorage(Storage petsStorage)
        {
            if (!petsStorage.Exists(PRESETS_FILE))
                return;

            try
            {
                using var stream = petsStorage.GetStream(PRESETS_FILE);
                if (stream == null)
                    return;

                using var reader = new StreamReader(stream, Encoding.UTF8);
                string json = reader.ReadToEnd();
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (parsed == null || parsed.Count == 0)
                    return;

                var cleaned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach ((string key, string value) in parsed)
                {
                    if (key.StartsWith('_'))
                        continue;

                    if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
                        continue;

                    cleaned[key] = value.Trim().ToLowerInvariant();
                }

                if (cleaned.Count == 0)
                    return;

                PresetHashes = cleaned;
                Logger.Log($"Ez pet: loaded {PresetHashes.Count} Live2D preset hash(es) from {PRESETS_FILE}.", LoggingTarget.Runtime);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Ez pet: failed reading {PRESETS_FILE}");
            }
        }
    }
}
