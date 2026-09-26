// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Fonts
{
    /// <summary>
    /// Persisted font file -> family name map, fingerprinted by file length + last write time.
    /// Reading a family name costs an <c>FT_New_Face</c> plus a walk of the SFNT name table, and the
    /// startup path only needs a couple of configured families, so the scan result is kept on disk
    /// and reused while the file itself is unchanged.
    /// </summary>
    /// <remarks>
    /// Only files found by the current enumeration are looked up here, so a removed font cannot be served
    /// from the cache and a replaced one fails the fingerprint. That is why no invalidation pass is needed.
    /// </remarks>
    internal sealed class EzSystemFontNameCache
    {
        private const int format_version = 1;
        public const string FILENAME = "ez-font-name-cache.json";

        private readonly Storage storage;
        private readonly Dictionary<string, Entry> entries;
        private bool dirty;

        private EzSystemFontNameCache(Storage storage, Dictionary<string, Entry> entries)
        {
            this.storage = storage;
            this.entries = entries;
        }

        /// <summary>
        /// Loads the cache, or starts an empty one when it is missing, unreadable or written by another format
        /// version. Never throws: a broken cache must not stop the font scan.
        /// </summary>
        public static EzSystemFontNameCache Load(Storage storage)
        {
            var entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (storage.Exists(FILENAME))
                {
                    using var stream = storage.GetStream(FILENAME, FileAccess.Read, FileMode.Open);

                    var payload = JsonSerializer.Deserialize<Payload>(stream);

                    if (payload?.Version == format_version && payload.Entries != null)
                    {
                        foreach (var (path, entry) in payload.Entries)
                        {
                            if (entry?.Family != null)
                                entries[path] = entry;
                        }
                    }
                }
            }
            catch
            {
                entries.Clear();
            }

            return new EzSystemFontNameCache(storage, entries);
        }

        public bool TryGet(string path, long length, long modifiedUtcTicks, out string family)
        {
            if (entries.TryGetValue(path, out var entry)
                && entry.Family != null
                && entry.Length == length
                && entry.ModifiedUtcTicks == modifiedUtcTicks)
            {
                family = entry.Family;
                return true;
            }

            family = string.Empty;
            return false;
        }

        public void Store(string path, long length, long modifiedUtcTicks, string family)
        {
            entries[path] = new Entry { Length = length, ModifiedUtcTicks = modifiedUtcTicks, Family = family };
            dirty = true;
        }

        /// <summary>Writes the cache back if anything was resolved this run. A failed write only costs a rescan next run.</summary>
        public void Save()
        {
            if (!dirty)
                return;

            try
            {
                using var stream = storage.CreateFileSafely(FILENAME);
                JsonSerializer.Serialize(stream, new Payload { Version = format_version, Entries = entries });
                dirty = false;
            }
            catch
            {
                // Ignored on purpose.
            }
        }

        private sealed class Payload
        {
            public int Version { get; set; }
            public Dictionary<string, Entry>? Entries { get; set; }
        }

        private sealed class Entry
        {
            public long Length { get; set; }
            public long ModifiedUtcTicks { get; set; }
            public string? Family { get; set; }
        }
    }
}
