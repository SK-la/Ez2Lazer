// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Rulesets.BMS.Beatmaps.Persistence;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Canonical on-disk layout for BMS ruleset data under the osu storage root.
    /// </summary>
    public static class BmsStoragePaths
    {
        public const string STORAGE_ROOT = "EzBMS";
        public const string INDEX_DATABASE_FILE = "index.sqlite";
        public const string LAMP_DATABASE_FILE = "lamps.sqlite";
        public const string FILTER_DATABASE_FILE = "filter.sqlite";
        public const string ANALYTICS_DATABASE_FILE = "analytics.sqlite";

        public static string GetStorageRootPath(Storage storage) => storage.GetFullPath(STORAGE_ROOT);

        public static string GetIndexDatabasePath(Storage storage) => Path.Combine(GetStorageRootPath(storage), INDEX_DATABASE_FILE);

        public static string GetLampDatabasePath(Storage storage) => Path.Combine(GetStorageRootPath(storage), LAMP_DATABASE_FILE);

        public static string GetFilterDatabasePath(Storage storage) => Path.Combine(GetStorageRootPath(storage), FILTER_DATABASE_FILE);

        public static string GetAnalyticsDatabasePath(Storage storage) => Path.Combine(GetStorageRootPath(storage), ANALYTICS_DATABASE_FILE);

        /// <summary>
        /// Ensures <see cref="STORAGE_ROOT"/> exists and migrates legacy <c>bms_cache</c> / <c>bms</c> data when needed.
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
            string indexPath = Path.Combine(ezbmsRoot, BmsStoragePaths.INDEX_DATABASE_FILE);
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

                var repository = new BmsLibraryIndexRepository(indexPath);
                repository.ImportFromLibraryCache(cache);
                Logger.Log($"[BMS] Migrated library cache from '{legacyDir}' into EzBMS index.", LoggingTarget.Database);
                break;
            }
        }

        private static void migrateLampDatabase(Storage storage, string ezbmsRoot)
        {
            string target = Path.Combine(ezbmsRoot, BmsStoragePaths.LAMP_DATABASE_FILE);

            if (File.Exists(target))
                return;

            foreach (string legacyDir in new[] { legacy_cache_directory, legacy_bms_directory, BmsStoragePaths.STORAGE_ROOT })
            {
                string candidate = Path.Combine(storage.GetFullPath(legacyDir), BmsStoragePaths.LAMP_DATABASE_FILE);

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
