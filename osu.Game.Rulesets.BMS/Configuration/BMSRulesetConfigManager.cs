// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using osu.Game.Configuration;
using osu.Game.Rulesets.Configuration;

namespace osu.Game.Rulesets.BMS.Configuration
{
    public class BMSRulesetConfigManager : RulesetConfigManager<BMSRulesetSetting>
    {
        public BMSRulesetConfigManager(SettingsStore? settings, RulesetInfo ruleset, int? variant = null)
            : base(settings, ruleset, variant)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(BMSRulesetSetting.BmsRootPath, string.Empty);
            SetDefault(BMSRulesetSetting.BmsLibraryPaths, "[]");
            SetDefault(BMSRulesetSetting.ScrollSpeed, 25.0, 1.0, 40.0, 1.0);
            SetDefault(BMSRulesetSetting.AutoPreloadKeysounds, true);
            SetDefault(BMSRulesetSetting.KeysoundVolume, 1.0, 0.0, 1.0, 0.01);
        }

        public static IReadOnlyList<string> ParseLibraryPaths(string? rawPaths, string? legacyRootPath = null)
        {
            List<string> paths = new List<string>();

            if (!string.IsNullOrWhiteSpace(rawPaths))
            {
                string trimmed = rawPaths.Trim();

                try
                {
                    if (trimmed.StartsWith("[", StringComparison.Ordinal))
                    {
                        List<string>? deserialised = JsonSerializer.Deserialize<List<string>>(trimmed);

                        if (deserialised != null)
                            paths.AddRange(deserialised);
                    }
                    else
                    {
                        paths.Add(trimmed);
                    }
                }
                catch (JsonException)
                {
                    paths.Add(trimmed);
                }
            }

            if (paths.Count == 0 && !string.IsNullOrWhiteSpace(legacyRootPath))
                paths.Add(legacyRootPath);

            return normalisePaths(paths);
        }

        public static string SerialiseLibraryPaths(IEnumerable<string> paths)
            => JsonSerializer.Serialize(normalisePaths(paths));

        private static List<string> normalisePaths(IEnumerable<string> paths)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> result = new List<string>();

            foreach (string path in paths)
            {
                string trimmed = path?.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(trimmed) || !seen.Add(trimmed))
                    continue;

                result.Add(trimmed);
            }

            return result;
        }
    }

    public enum BMSRulesetSetting
    {
        BmsRootPath,
        BmsLibraryPaths,
        ScrollSpeed,
        AutoPreloadKeysounds,
        KeysoundVolume,
    }
}
