// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace osu.Game.EzOsuGame.Skills.Dan
{
    /// <summary>
    ///     Maps xxySR (Sunny star) onto community dan labels via mania-hub interval tables.
    ///     Does not run LeoBlack / sunnyAlgorithm — only table lookup.
    /// </summary>
    public static class EzSunnyDanIntervals
    {
        public readonly record struct LookupResult(string DisplayLabel, double RawDan, string IntervalName);

        private static readonly Dictionary<string, string?> tier_variants = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["low"] = "--",
            ["mid/low"] = "-",
            ["mid"] = null,
            ["mid/high"] = "+",
            ["high"] = "++"
        };

        private static readonly Dictionary<string, double> tier_offsets = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["low"] = -0.4,
            ["mid/low"] = -0.2,
            ["mid"] = 0,
            ["mid/high"] = 0.2,
            ["high"] = 0.4
        };

        /// <summary>Longest-first so <c>mid/low</c> wins over <c>low</c>.</summary>
        private static readonly string[] tier_suffixes_longest_first =
        {
            "mid/low", "mid/high", "low", "mid", "high"
        };

        private static readonly string[] greek_tails =
        {
            "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta", "iota", "kappa",
            "azimuth", "zenith", "stellium", "terra", "celestial", "mystery", "nihility", "finish"
        };

        private static readonly object level_maps_gate = new();
        private static readonly Dictionary<(int KeyCount, string Side), Dictionary<string, int>> level_maps = new();

        public static bool SupportsKeyCount(int keyCount)
        {
            return keyCount is 4 or 6 or 7;
        }

        public static bool TryLookup(int keyCount, string side, double xxySr, out LookupResult result)
        {
            result = default;

            if (!SupportsKeyCount(keyCount) || !double.IsFinite(xxySr) || xxySr <= 0)
                return false;

            var table = getTable(keyCount, side);
            if (table == null || table.Length == 0)
                return false;

            string intervalName = lookupIntervalName(xxySr, table);
            if (!tryParseInterval(intervalName, keyCount, side, table, out string displayLabel, out double rawDan))
                return false;

            result = new LookupResult(displayLabel, rawDan, intervalName);
            return true;
        }

        private static (double Lower, double Upper, string Name)[]? getTable(int keyCount, string side)
        {
            bool ln = side == DanSkillSystem.SIDE_LN;
            return keyCount switch
            {
                4 => ln ? EzSunnyDanIntervalTables.Ln4K : EzSunnyDanIntervalTables.Rc4K,
                6 => ln ? EzSunnyDanIntervalTables.Ln6K : EzSunnyDanIntervalTables.Rc6K,
                7 => ln ? EzSunnyDanIntervalTables.Ln7K : EzSunnyDanIntervalTables.Rc7K,
                _ => null
            };
        }

        private static string lookupIntervalName(double sr, (double Lower, double Upper, string Name)[] table)
        {
            foreach (var row in table)
            {
                if (row.Lower <= sr && sr <= row.Upper)
                    return row.Name;
            }

            if (sr < table[0].Lower)
                return $"< {table[0].Name}";

            if (sr > table[^1].Upper)
                return $"> {table[^1].Name}";

            return table[0].Name;
        }

        private static bool tryParseInterval(
            string text,
            int keyCount,
            string side,
            (double Lower, double Upper, string Name)[] table,
            out string displayLabel,
            out double rawDan)
        {
            displayLabel = string.Empty;
            rawDan = 0;

            string body = text.Trim();
            bool below = false;
            bool above = false;

            if (body.StartsWith("< ", StringComparison.Ordinal))
            {
                below = true;
                body = body[2..].Trim();
            }
            else if (body.StartsWith("> ", StringComparison.Ordinal))
            {
                above = true;
                body = body[2..].Trim();
            }

            if (!trySplitBaseAndTier(body, out string baseName, out string tier))
                return false;

            if (!tryResolveLevel(keyCount, side, table, baseName, out int level))
                return false;

            string bare = toBareLabel(baseName);
            string? variant = below ? "--" : above ? "++" : tier_variants.GetValueOrDefault(tier);
            displayLabel = $"{bare}{variant ?? string.Empty}";

            if (below)
                rawDan = level - 0.5;
            else if (above)
                rawDan = level + 0.5;
            else
                rawDan = level + tier_offsets.GetValueOrDefault(tier);

            return !string.IsNullOrEmpty(bare);
        }

        /// <summary>
        /// Split <c>Intro 1 mid/low</c> → base + tier without Regex.
        /// Avoids <see cref="System.Text.RegularExpressions.RegexMatchTimeoutException"/> on the live ChartDan path
        /// (table scans previously ran <c>tier_pattern.Match</c> per row).
        /// </summary>
        private static bool trySplitBaseAndTier(string body, out string baseName, out string tier)
        {
            baseName = string.Empty;
            tier = string.Empty;

            foreach (string suffix in tier_suffixes_longest_first)
            {
                string needle = " " + suffix;
                if (!body.EndsWith(needle, StringComparison.Ordinal))
                    continue;

                baseName = body[..^needle.Length];
                if (string.IsNullOrWhiteSpace(baseName))
                    return false;

                tier = suffix;
                return true;
            }

            return false;
        }

        private static bool tryResolveLevel(
            int keyCount,
            string side,
            (double Lower, double Upper, string Name)[] table,
            string baseName,
            out int level)
        {
            return getOrBuildLevelMap(keyCount, side, table).TryGetValue(baseName, out level);
        }

        private static Dictionary<string, int> getOrBuildLevelMap(
            int keyCount,
            string side,
            (double Lower, double Upper, string Name)[] table)
        {
            var key = (keyCount, side);

            lock (level_maps_gate)
            {
                if (level_maps.TryGetValue(key, out var existing))
                    return existing;
            }

            var built = new Dictionary<string, int>(StringComparer.Ordinal);
            int lastNumeric = 0;

            foreach (var row in table)
            {
                if (!trySplitBaseAndTier(row.Name, out string baseOfRow, out _))
                    baseOfRow = row.Name;

                if (built.ContainsKey(baseOfRow))
                    continue;

                if (tryParseTrailingNumber(baseOfRow, out int n))
                    lastNumeric = n;
                else
                    lastNumeric += 1;

                built[baseOfRow] = lastNumeric;
            }

            lock (level_maps_gate)
            {
                level_maps[key] = built;
                return built;
            }
        }

        private static bool tryParseTrailingNumber(string text, out int number)
        {
            number = 0;
            int i = text.Length - 1;

            if (i < 0 || !char.IsDigit(text[i]))
                return false;

            while (i >= 0 && char.IsDigit(text[i]))
                i--;

            // Require a boundary before digits when more text precedes (e.g. "Reform 10", not "Alpha10").
            if (i >= 0 && text[i] != ' ')
                return false;

            return int.TryParse(text.AsSpan(i + 1), NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        /// <summary>Maps interval base names onto Ez ladder / texture bare ids.</summary>
        private static string toBareLabel(string baseName)
        {
            string s = baseName.Trim();

            if (s.StartsWith("Regular ", StringComparison.OrdinalIgnoreCase))
                s = s[8..].TrimStart();
            else if (s.StartsWith("LN ", StringComparison.OrdinalIgnoreCase))
                s = s[3..].TrimStart();

            // e.g. "Something LN 3" → keep trailing portion after " LN ".
            int firstSpace = s.IndexOf(' ');
            if (firstSpace > 0)
            {
                int lnIdx = s.IndexOf(" LN ", StringComparison.OrdinalIgnoreCase);
                if (lnIdx == firstSpace)
                    s = s[(lnIdx + 4)..].TrimStart();
            }

            if (s.StartsWith("Reform ", StringComparison.OrdinalIgnoreCase))
            {
                string rest = s[7..].TrimStart();
                if (int.TryParse(rest, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    return rest;
            }

            if (s.StartsWith("Intro ", StringComparison.OrdinalIgnoreCase))
            {
                string rest = s[6..].TrimStart();
                if (int.TryParse(rest, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    return $"intro{rest}";
            }

            if (int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                return s;

            string lower = s.ToLowerInvariant();

            foreach (string tail in greek_tails)
            {
                if (lower == tail || lower.EndsWith(" " + tail, StringComparison.Ordinal))
                    return tail;
            }

            return lower;
        }
    }
}
