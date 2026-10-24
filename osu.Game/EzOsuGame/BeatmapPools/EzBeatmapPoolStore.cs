// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.BeatmapPools
{
    /// <summary>
    /// Central store for external beatmap pool paths. Physical files remain owned and indexed by
    /// ruleset-specific providers; this service only stores ruleset/variant associations.
    /// </summary>
    public sealed class EzBeatmapPoolStore : IDisposable
    {
        public const int CURRENT_VERSION = 1;

        private readonly Bindable<string> persistedDocument;
        private readonly object sync = new object();
        private EzBeatmapPoolDocument document;
        private bool handlingConfigChange;

        public event Action? Changed;

        public EzBeatmapPoolStore(Ez2ConfigManager config)
        {
            persistedDocument = config.GetBindable<string>(Ez2Setting.BeatmapPoolDocument);
            document = deserialise(persistedDocument.Value);
            persistedDocument.BindValueChanged(e => onPersistedDocumentChanged(e.NewValue));
        }

        public IReadOnlyList<EzBeatmapPoolPath> GetPaths(string rulesetShortName, int variant = 0, bool enabledOnly = false)
        {
            string key = normaliseRulesetName(rulesetShortName);

            lock (sync)
            {
                return document.Paths
                               .Where(p => string.Equals(p.RulesetShortName, key, StringComparison.OrdinalIgnoreCase)
                                           && p.Variant == variant
                                           && (!enabledOnly || p.Enabled))
                               .OrderBy(p => p.Order)
                               .ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
                               .Select(p => p.Clone())
                               .ToArray();
            }
        }

        public IReadOnlyList<string> GetEnabledDirectories(string rulesetShortName, int variant = 0, bool requireExisting = true)
            => GetPaths(rulesetShortName, variant, true)
               .Select(p => p.Path)
               .Where(path => !requireExisting || Directory.Exists(path))
               .ToArray();

        public bool AddPath(string rulesetShortName, int variant, string path, bool enabled = true)
        {
            string key = normaliseRulesetName(rulesetShortName);
            string normalisedPath = NormalisePath(path);

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(normalisedPath))
                return false;

            lock (sync)
            {
                if (document.Paths.Any(p => isSameAssociation(p, key, variant, normalisedPath)))
                    return false;

                int order = document.Paths.Where(p => string.Equals(p.RulesetShortName, key, StringComparison.OrdinalIgnoreCase) && p.Variant == variant)
                                    .Select(p => p.Order)
                                    .DefaultIfEmpty(-1)
                                    .Max() + 1;

                document.Paths.Add(new EzBeatmapPoolPath
                {
                    RulesetShortName = key,
                    Variant = variant,
                    Path = normalisedPath,
                    Enabled = enabled,
                    Order = order,
                });

                persist();
                return true;
            }
        }

        public bool SetEnabled(string rulesetShortName, int variant, string path, bool enabled)
        {
            string key = normaliseRulesetName(rulesetShortName);
            string normalisedPath = NormalisePath(path);

            lock (sync)
            {
                EzBeatmapPoolPath? item = document.Paths.FirstOrDefault(p => isSameAssociation(p, key, variant, normalisedPath));

                if (item == null || item.Enabled == enabled)
                    return false;

                item.Enabled = enabled;
                persist();
                return true;
            }
        }

        public bool RemovePath(string rulesetShortName, int variant, string path)
        {
            string key = normaliseRulesetName(rulesetShortName);
            string normalisedPath = NormalisePath(path);

            lock (sync)
            {
                int removed = document.Paths.RemoveAll(p => isSameAssociation(p, key, variant, normalisedPath));

                if (removed == 0)
                    return false;

                normaliseOrders(key, variant);
                persist();
                return true;
            }
        }

        public bool Clear(string rulesetShortName, int variant = 0)
        {
            string key = normaliseRulesetName(rulesetShortName);

            lock (sync)
            {
                int removed = document.Paths.RemoveAll(p => string.Equals(p.RulesetShortName, key, StringComparison.OrdinalIgnoreCase) && p.Variant == variant);

                if (removed == 0)
                    return false;

                persist();
                return true;
            }
        }

        /// <summary>
        /// Imports paths from an older ruleset-specific setting only when the target profile is empty.
        /// Existing enable states are never overwritten.
        /// </summary>
        public bool MigrateLegacyPaths(string rulesetShortName, IEnumerable<string> paths, int variant = 0)
        {
            string key = normaliseRulesetName(rulesetShortName);

            lock (sync)
            {
                if (document.Paths.Any(p => string.Equals(p.RulesetShortName, key, StringComparison.OrdinalIgnoreCase) && p.Variant == variant))
                    return false;

                int order = 0;

                foreach (string path in paths)
                {
                    string normalisedPath = NormalisePath(path);

                    if (string.IsNullOrEmpty(normalisedPath)
                        || document.Paths.Any(p => isSameAssociation(p, key, variant, normalisedPath)))
                        continue;

                    document.Paths.Add(new EzBeatmapPoolPath
                    {
                        RulesetShortName = key,
                        Variant = variant,
                        Path = normalisedPath,
                        Enabled = true,
                        Order = order++,
                    });
                }

                if (order == 0)
                    return false;

                persist();
                return true;
            }
        }

        public static string NormalisePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string trimmed = path.Trim();

            try
            {
                string fullPath = Path.GetFullPath(trimmed);
                string root = Path.GetPathRoot(fullPath) ?? string.Empty;

                return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                    ? fullPath
                    : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        internal static EzBeatmapPoolDocument ParseDocument(string? raw) => deserialise(raw);

        private void onPersistedDocumentChanged(string raw)
        {
            if (handlingConfigChange)
                return;

            lock (sync)
                document = deserialise(raw);

            Changed?.Invoke();
        }

        private void persist()
        {
            document.Version = CURRENT_VERSION;
            document.Paths = normaliseDocumentPaths(document.Paths);
            string serialised = JsonSerializer.Serialize(document);

            handlingConfigChange = true;

            try
            {
                persistedDocument.Value = serialised;
            }
            finally
            {
                handlingConfigChange = false;
            }

            Changed?.Invoke();
        }

        private static EzBeatmapPoolDocument deserialise(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new EzBeatmapPoolDocument { Version = CURRENT_VERSION };

            try
            {
                EzBeatmapPoolDocument? parsed = JsonSerializer.Deserialize<EzBeatmapPoolDocument>(raw);

                if (parsed == null)
                    return new EzBeatmapPoolDocument { Version = CURRENT_VERSION };

                parsed.Version = CURRENT_VERSION;
                parsed.Paths = normaliseDocumentPaths(parsed.Paths);
                return parsed;
            }
            catch (JsonException ex)
            {
                Logger.Error(ex, "Failed to parse the unified beatmap pool document; keeping an empty in-memory document.");
                return new EzBeatmapPoolDocument { Version = CURRENT_VERSION };
            }
        }

        private static List<EzBeatmapPoolPath> normaliseDocumentPaths(IEnumerable<EzBeatmapPoolPath> paths)
        {
            var result = new List<EzBeatmapPoolPath>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (EzBeatmapPoolPath source in paths.OrderBy(p => p.Order))
            {
                string ruleset = normaliseRulesetName(source.RulesetShortName);
                string path = NormalisePath(source.Path);

                if (string.IsNullOrEmpty(ruleset) || string.IsNullOrEmpty(path))
                    continue;

                string key = $"{ruleset}\0{source.Variant}\0{path}";

                if (!seen.Add(key))
                    continue;

                result.Add(new EzBeatmapPoolPath
                {
                    RulesetShortName = ruleset,
                    Variant = source.Variant,
                    Path = path,
                    Enabled = source.Enabled,
                    Order = source.Order,
                });
            }

            foreach (IGrouping<(string Ruleset, int Variant), EzBeatmapPoolPath> group in result.GroupBy(p => (p.RulesetShortName.ToUpperInvariant(), p.Variant)))
            {
                int order = 0;

                foreach (EzBeatmapPoolPath item in group.OrderBy(p => p.Order))
                    item.Order = order++;
            }

            return result;
        }

        private void normaliseOrders(string rulesetShortName, int variant)
        {
            int order = 0;

            foreach (EzBeatmapPoolPath item in document.Paths.Where(p => string.Equals(p.RulesetShortName, rulesetShortName, StringComparison.OrdinalIgnoreCase) && p.Variant == variant)
                                                              .OrderBy(p => p.Order))
                item.Order = order++;
        }

        private static bool isSameAssociation(EzBeatmapPoolPath path, string rulesetShortName, int variant, string normalisedPath)
            => path.Variant == variant
               && string.Equals(path.RulesetShortName, rulesetShortName, StringComparison.OrdinalIgnoreCase)
               && string.Equals(NormalisePath(path.Path), normalisedPath, StringComparison.OrdinalIgnoreCase);

        private static string normaliseRulesetName(string? rulesetShortName) => rulesetShortName?.Trim().ToLowerInvariant() ?? string.Empty;

        public void Dispose() => persistedDocument.UnbindAll();
    }
}
