// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    public sealed class RulesetExternalLibraryConfig
    {
        public bool Enabled { get; set; }

        public List<string> Paths { get; set; } = new List<string>();
    }

    public sealed class ExternalLibrarySettings
    {
        private static readonly string[] standard_ruleset_short_names = { "osu", "mania", "taiko", "catch" };

        /// <summary>
        /// Shared osu!-style external folders (stable <c>Songs</c> or set directories) for all standard rulesets.
        /// </summary>
        public bool Enabled { get; set; }

        public List<string> Paths { get; set; } = new List<string>();

        /// <summary>
        /// Per-ruleset paths for non-standard layouts (e.g. BMS).
        /// </summary>
        public Dictionary<string, RulesetExternalLibraryConfig> Rulesets { get; set; } = new Dictionary<string, RulesetExternalLibraryConfig>();

        public bool PendingRescan { get; set; }

        public void Normalise()
        {
            foreach (string shortName in standard_ruleset_short_names)
            {
                if (!Rulesets.TryGetValue(shortName, out var legacy))
                    continue;

                if (legacy.Enabled)
                    Enabled = true;

                foreach (string path in legacy.Paths)
                {
                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    if (!Paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                        Paths.Add(path);
                }

                Rulesets.Remove(shortName);
            }
        }

        public RulesetExternalLibraryConfig GetOrCreateRulesetLibrary(string rulesetShortName)
        {
            if (!Rulesets.TryGetValue(rulesetShortName, out var config))
            {
                config = new RulesetExternalLibraryConfig();
                Rulesets[rulesetShortName] = config;
            }

            return config;
        }

        public bool HasAnyConfiguredPath()
            => (Enabled && Paths.Any(p => !string.IsNullOrWhiteSpace(p)))
               || Rulesets.Values.Any(r => r.Enabled && r.Paths.Any(p => !string.IsNullOrWhiteSpace(p)));
    }
}
