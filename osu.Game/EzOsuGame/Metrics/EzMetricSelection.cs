// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Which analysis system each ruleset reads out under, persisted as <c>ruleset=system</c> pairs.
    /// </summary>
    /// <remarks>
    /// Every ruleset owns its own choice, so switching mania to Sunny does not change osu. An absent or stale id
    /// falls back to the ruleset's first declared system, so a removed system degrades to a working default instead
    /// of an empty panel. Every supported system stays selectable; only the chosen one is stored.
    /// </remarks>
    public static class EzMetricSelection
    {
        private const char pair_separator = ';';
        private const char key_separator = '=';

        public static EzMetricVariant? Primary(int rulesetOnlineId, EzMetricVariantCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);

            string? stored = read(rulesetOnlineId);

            if (stored != null && catalog.Get(stored, rulesetOnlineId) is { } chosen)
                return chosen;

            return catalog.DefaultFor(rulesetOnlineId);
        }

        public static string? PrimaryId(int rulesetOnlineId) => read(rulesetOnlineId);

        public static void SetPrimary(int rulesetOnlineId, string variantId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(variantId);

            var config = GlobalConfigStore.EzConfig;

            if (config == null)
                return;

            config.SetValue(Ez2Setting.AnalysisSystemSelection, write(rulesetOnlineId, variantId, config.Get<string>(Ez2Setting.AnalysisSystemSelection)));
        }

        /// <summary>
        /// Moves the ruleset's primary variant by <paramref name="direction"/> through the catalog and returns
        /// the new selection. Used by the panel chip, which has no room for a full dropdown.
        /// </summary>
        public static EzMetricVariant? Cycle(int rulesetOnlineId, EzMetricVariantCatalog catalog, int direction = 1)
        {
            var variants = catalog.For(rulesetOnlineId);

            if (variants.Count == 0)
                return null;

            var current = Primary(rulesetOnlineId, catalog);
            int index = 0;

            if (current != null)
            {
                for (int i = 0; i < variants.Count; i++)
                {
                    if (string.Equals(variants[i].Id, current.Id, StringComparison.Ordinal))
                    {
                        index = i;
                        break;
                    }
                }
            }

            index = ((index + direction) % variants.Count + variants.Count) % variants.Count;
            var next = variants[index];
            SetPrimary(rulesetOnlineId, next.Id);
            return next;
        }

        private static string? read(int rulesetOnlineId)
        {
            var config = GlobalConfigStore.EzConfig;

            if (config == null)
                return null;

            string raw = config.Get<string>(Ez2Setting.AnalysisSystemSelection);

            if (string.IsNullOrEmpty(raw))
                return null;

            foreach (string pair in raw.Split(pair_separator, StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = pair.IndexOf(key_separator);

                if (separator <= 0)
                    continue;

                if (!int.TryParse(pair.AsSpan(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ruleset))
                    continue;

                if (ruleset != rulesetOnlineId)
                    continue;

                string value = pair[(separator + 1)..];
                return string.IsNullOrEmpty(value) ? null : value;
            }

            return null;
        }

        private static string write(int rulesetOnlineId, string variantId, string? existing)
        {
            string key = rulesetOnlineId.ToString(CultureInfo.InvariantCulture);
            var builder = new System.Text.StringBuilder();
            bool replaced = false;

            if (!string.IsNullOrEmpty(existing))
            {
                foreach (string pair in existing.Split(pair_separator, StringSplitOptions.RemoveEmptyEntries))
                {
                    int separator = pair.IndexOf(key_separator);

                    if (separator <= 0)
                        continue;

                    if (int.TryParse(pair.AsSpan(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ruleset)
                        && ruleset == rulesetOnlineId)
                    {
                        replaced = true;
                        continue;
                    }

                    if (builder.Length > 0)
                        builder.Append(pair_separator);

                    builder.Append(pair);
                }
            }

            if (builder.Length > 0)
                builder.Append(pair_separator);

            builder.Append(key).Append(key_separator).Append(variantId);

            return replaced || builder.Length > 0 ? builder.ToString() : string.Empty;
        }
    }
}
