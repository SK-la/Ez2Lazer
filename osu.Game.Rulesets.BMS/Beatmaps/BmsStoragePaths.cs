// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Canonical on-disk layout for BMS ruleset data under the osu storage root.
    /// </summary>
    public static class BmsStoragePaths
    {
        public const string StorageRoot = "EzBMS";
        public const string IndexDatabaseFile = "index.sqlite";
        public const string LampDatabaseFile = "lamps.sqlite";

        public static string GetStorageRootPath(Storage storage) => storage.GetFullPath(StorageRoot);

        public static string GetIndexDatabasePath(Storage storage) => Path.Combine(GetStorageRootPath(storage), IndexDatabaseFile);

        public static string GetLampDatabasePath(Storage storage) => Path.Combine(GetStorageRootPath(storage), LampDatabaseFile);

        /// <summary>
        /// Ensures <see cref="StorageRoot"/> exists and migrates legacy <c>bms_cache</c> / <c>bms</c> data when needed.
        /// </summary>
        public static string EnsureInitialized(Storage storage)
        {
            string root = GetStorageRootPath(storage);
            Directory.CreateDirectory(root);

            BmsLibraryMigration.MigrateIfNeeded(storage, root);
            return root;
        }
    }

    internal static class BmsLibraryMigration
    {
        private const string legacy_cache_directory = "bms_cache";
        private const string legacy_bms_directory = "bms";
        private const string legacy_library_cache = "bms_library_cache.json";

        public static void MigrateIfNeeded(Storage storage, string ezbmsRoot)
        {
            string indexPath = Path.Combine(ezbmsRoot, BmsStoragePaths.IndexDatabaseFile);
            bool indexExists = File.Exists(indexPath);

            if (!indexExists)
                tryImportLegacyJson(storage, ezbmsRoot, indexPath);

            migrateLampDatabase(storage, ezbmsRoot);
        }

        private static void tryImportLegacyJson(Storage storage, string ezbmsRoot, string indexPath)
        {
            foreach (string legacyDir in new[] { legacy_cache_directory, legacy_bms_directory })
            {
                string legacyRoot = storage.GetFullPath(legacyDir);
                string jsonPath = Path.Combine(legacyRoot, legacy_library_cache);

                if (!File.Exists(jsonPath))
                    continue;

                var cache = BMSLibraryCache.Load(jsonPath);

                if (cache == null)
                    continue;

                var repository = new Persistence.BmsLibraryIndexRepository(indexPath);
                repository.ImportFromLibraryCache(cache);
                Logger.Log($"[BMS] Migrated library cache from '{legacyDir}' into EzBMS index.", LoggingTarget.Database);
                break;
            }
        }

        private static void migrateLampDatabase(Storage storage, string ezbmsRoot)
        {
            string target = Path.Combine(ezbmsRoot, BmsStoragePaths.LampDatabaseFile);

            if (File.Exists(target))
                return;

            foreach (string legacyDir in new[] { legacy_cache_directory, legacy_bms_directory, BmsStoragePaths.StorageRoot })
            {
                string candidate = Path.Combine(storage.GetFullPath(legacyDir), BmsStoragePaths.LampDatabaseFile);

                if (!File.Exists(candidate) || string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    Directory.CreateDirectory(ezbmsRoot);
                    File.Move(candidate, target);
                    Logger.Log($"[BMS] Moved lamp database from '{legacyDir}' to EzBMS.", LoggingTarget.Database);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[BMS] Lamp database migration failed: {ex.Message}", LoggingTarget.Database, LogLevel.Important);
                }

                return;
            }
        }
    }
}
