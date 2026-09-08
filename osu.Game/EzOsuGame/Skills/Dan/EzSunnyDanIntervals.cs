// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace osu.Game.EzOsuGame.Skills.Dan
{
    /// <summary>
    ///     Maps xxySR (Sunny star) onto community dan labels via mania-hub interval tables.
    ///     Does not run LeoBlack / sunnyAlgorithm — only table lookup.
    /// </summary>
    public static class EzSunnyDanIntervals
    {
        public readonly record struct LookupResult(string DisplayLabel, double RawDan, string IntervalName);

        private static readonly Regex tier_pattern = new Regex(@"^(?<base>.+?) (?<tier>low|mid/low|mid/high|mid|high)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

        private static readonly string[] greek_tails =
        {
            "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta", "iota", "kappa",
            "azimuth", "zenith", "stellium", "terra", "celestial", "mystery", "nihility", "finish"
        };

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
            if (!tryParseInterval(intervalName, table, out string displayLabel, out double rawDan))
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

        private static bool tryParseInterval(string text, (double Lower, double Upper, string Name)[] table, out string displayLabel, out double rawDan)
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

            var match = tier_pattern.Match(body);
            if (!match.Success)
                return false;

            string baseName = match.Groups["base"].Value;
            string tier = match.Groups["tier"].Value;

            if (!tryResolveLevel(baseName, table, out int level))
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

        private static bool tryResolveLevel(string baseName, (double Lower, double Upper, string Name)[] table, out int level)
        {
            level = 0;
            int lastNumeric = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in table)
            {
                var match = tier_pattern.Match(row.Name);
                string baseOfRow = match.Success ? match.Groups["base"].Value : row.Name;
                if (!seen.Add(baseOfRow))
                    continue;

                var numberMatch = Regex.Match(baseOfRow, @"(\d+)$");
                if (numberMatch.Success)
                    lastNumeric = int.Parse(numberMatch.Groups[1].Value);
                else
                    lastNumeric += 1;

                if (string.Equals(baseOfRow, baseName, StringComparison.Ordinal))
                {
                    level = lastNumeric;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Maps interval base names onto Ez ladder / texture bare ids.</summary>
        private static string toBareLabel(string baseName)
        {
            string s = Regex.Replace(baseName, @"^(Regular|LN)\s+", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"^\S+\s+LN\s+", string.Empty, RegexOptions.IgnoreCase);

            var reform = Regex.Match(s, @"^Reform\s+(\d+)$", RegexOptions.IgnoreCase);
            if (reform.Success)
                return reform.Groups[1].Value;

            var intro = Regex.Match(s, @"^Intro\s+(\d+)$", RegexOptions.IgnoreCase);
            if (intro.Success)
                return $"intro{intro.Groups[1].Value}";

            var regularNum = Regex.Match(s, @"^(\d+)$");
            if (regularNum.Success)
                return regularNum.Groups[1].Value;

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
