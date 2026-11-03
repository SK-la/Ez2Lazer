// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.ExternalRulesets
{
    public sealed class EzRulesetMappingEntry
    {
        public bool Enabled { get; set; } = true;

        public int? OnlineID { get; set; }

        public int? Order { get; set; }
    }

    public readonly record struct DiscoveredExternalRuleset(string ShortName, string Name, int InstanceOnlineId);

    public sealed class EzRulesetMappingConfig
    {
        public const string FILENAME = "EzRulesetMapping.ini";

        public const string HEADER_LINE1 = "# OnlineID must avoid 0~3 (official modes). Use -1 or >= 4. Restart required.";
        public const string HEADER_LINE2 = "# Only rulesets with explicit OnlineID (>=4) need OnlineID/Order entries.";

        public Dictionary<string, EzRulesetMappingEntry> Entries { get; } = new Dictionary<string, EzRulesetMappingEntry>(StringComparer.Ordinal);

        public static EzRulesetMappingConfig Load(Storage storage)
        {
            var config = new EzRulesetMappingConfig();

            using var stream = storage.GetStream(FILENAME);

            if (stream == null)
                return config;

            string? currentSection = null;

            using var reader = new StreamReader(stream);

            while (reader.ReadLine() is { } line)
            {
                line = line.Trim();

                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    config.Entries.TryAdd(currentSection, new EzRulesetMappingEntry());
                    continue;
                }

                int equalsIndex = line.IndexOf('=');

                if (equalsIndex < 0 || currentSection == null)
                    continue;

                string key = line.Substring(0, equalsIndex).Trim();
                string value = line.Substring(equalsIndex + 1).Trim();
                var entry = config.Entries[currentSection];

                switch (key)
                {
                    case "Enabled":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int enabled))
                            entry.Enabled = enabled != 0;
                        break;

                    case "OnlineID":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int onlineId))
                            entry.OnlineID = onlineId;
                        break;

                    case "Order":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int order))
                            entry.Order = order;
                        break;
                }
            }

            return config;
        }

        public void Save(Storage storage)
        {
            using var stream = storage.GetStream(FILENAME, FileAccess.Write, FileMode.Create);
            using var writer = new StreamWriter(stream, Encoding.UTF8);

            writer.WriteLine(HEADER_LINE1);
            writer.WriteLine(HEADER_LINE2);
            writer.WriteLine();

            var orderedSections = Entries
                                  .OrderBy(kv => kv.Value.Order ?? int.MaxValue)
                                  .ThenBy(kv => kv.Key, StringComparer.Ordinal);

            foreach (var (shortName, entry) in orderedSections)
            {
                writer.WriteLine($"[{shortName}]");
                writer.WriteLine($"Enabled={(entry.Enabled ? 1 : 0)}");

                if (entry.OnlineID.HasValue)
                    writer.WriteLine($"OnlineID={entry.OnlineID.Value}");

                if (entry.Order.HasValue)
                    writer.WriteLine($"Order={entry.Order.Value}");

                writer.WriteLine();
            }
        }

        public EzRulesetMappingEntry GetOrAdd(string shortName)
        {
            if (!Entries.TryGetValue(shortName, out var entry))
            {
                entry = new EzRulesetMappingEntry();
                Entries[shortName] = entry;
            }

            return entry;
        }

        public void EnsureDefaults(IEnumerable<DiscoveredExternalRuleset> discovered)
        {
            int nextOrder = Entries.Values.Where(e => e.Order.HasValue).Select(e => e.Order!.Value).DefaultIfEmpty(-1).Max() + 1;

            foreach (var ruleset in discovered)
            {
                var entry = GetOrAdd(ruleset.ShortName);

                if (EzExternalRulesetMapping.HasExplicitOnlineId(ruleset.InstanceOnlineId))
                {
                    entry.OnlineID ??= ruleset.InstanceOnlineId;
                    entry.Order ??= nextOrder++;
                }
            }
        }
    }
}
