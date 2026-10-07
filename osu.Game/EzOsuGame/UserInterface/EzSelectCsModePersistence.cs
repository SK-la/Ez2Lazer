// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// Serialises song-select CS / keys filter selections per ruleset in <see cref="Configuration.Ez2Setting.EzSelectCsMode"/>.
    /// Format: <c>rulesetId=commaSeparatedModes|...</c> (e.g. <c>0=4,5|3=7,8</c>).
    /// Legacy: a single comma-separated list without <c>=</c> applies to whichever ruleset is loaded first.
    /// </summary>
    internal static class EzSelectCsModePersistence
    {
        internal const int legacy_ruleset_key = -1;

        public static Dictionary<int, HashSet<string>> Parse(string? raw)
        {
            var result = new Dictionary<int, HashSet<string>>();

            if (string.IsNullOrEmpty(raw))
                return result;

            if (!raw.Contains('='))
            {
                var legacy = parseModeTokens(raw);

                if (legacy.Count > 0)
                    result[legacy_ruleset_key] = legacy;

                return result;
            }

            foreach (string segment in raw.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = segment.IndexOf('=');

                if (separator <= 0)
                    continue;

                if (!int.TryParse(segment.AsSpan(0, separator), out int rulesetId))
                    continue;

                result[rulesetId] = parseModeTokens(segment[(separator + 1)..]);
            }

            return result;
        }

        public static string Serialize(IReadOnlyDictionary<int, HashSet<string>> selections)
        {
            return string.Join("|", selections
                .Where(kvp => kvp.Key >= 0 && kvp.Value.Count > 0)
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{kvp.Key}={string.Join(",", kvp.Value.OrderBy(x => x, StringComparer.Ordinal))}"));
        }

        public static HashSet<string> GetSelection(IReadOnlyDictionary<int, HashSet<string>> store, int rulesetId)
        {
            if (store.TryGetValue(rulesetId, out var direct))
                return new HashSet<string>(direct);

            if (store.TryGetValue(legacy_ruleset_key, out var legacy))
                return new HashSet<string>(legacy);

            return new HashSet<string>();
        }

        private static HashSet<string> parseModeTokens(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new HashSet<string>();

            return value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        }
    }
}
