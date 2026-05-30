// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    public sealed class RulesetExternalLibraryConfig
    {
        public bool Enabled { get; set; }

        public List<string> Paths { get; set; } = new List<string>();
    }

    /// <summary>
    /// External library paths keyed by ruleset short name (e.g. <c>bms</c>).
    /// Standard osu rulesets do not use this until stable-folder support is added.
    /// </summary>
    public sealed class ExternalLibrarySettings
    {
        public Dictionary<string, RulesetExternalLibraryConfig> Rulesets { get; set; } = new Dictionary<string, RulesetExternalLibraryConfig>();

        public bool PendingRescan { get; set; }

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
            => Rulesets.Values.Any(r => r.Enabled && r.Paths.Any(p => !string.IsNullOrWhiteSpace(p)));
    }
}
