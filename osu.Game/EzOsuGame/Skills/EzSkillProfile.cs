// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Metrics;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One side's skill wiring: which systems carry a vector for a ruleset, and which of them is the default radar.
    /// </summary>
    /// <remarks>
    /// Derived from the producer declaration, so consumers never hard-code a ruleset gate of their own. There is one
    /// profile per side, because the chart and player catalogs are separate tables.
    /// </remarks>
    public sealed class EzSkillProfile
    {
        public EzSkillProfile(int rulesetOnlineId, IReadOnlyList<string> systemIds, string? defaultSystemId)
        {
            RulesetOnlineId = rulesetOnlineId;
            SystemIds = systemIds;
            DefaultSystemId = defaultSystemId;
        }

        public int RulesetOnlineId { get; }

        public IReadOnlyList<string> SystemIds { get; }

        public string? DefaultSystemId { get; }

        public bool HasSkills => SystemIds.Count > 0;

        public bool ContainsSystem(string systemId)
            => SystemIds.Contains(systemId, StringComparer.Ordinal);

        /// <summary>
        /// Returns a profile for <paramref name="rulesetOnlineId"/>, or <see langword="null"/> when the catalog
        /// declares no vector for it.
        /// </summary>
        public static EzSkillProfile? TryBuild(int rulesetOnlineId, EzMetricProducerRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            var producers = registry.VectorProducers(rulesetOnlineId).ToList();

            if (producers.Count == 0)
                return null;

            var defaults = producers.Where(s => s.IsDefaultRadarFor(rulesetOnlineId)).Select(s => s.EffectiveSystemId).ToList();

            if (defaults.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple default-radar producers for ruleset {rulesetOnlineId}: {string.Join(", ", defaults)}");
            }

            return new EzSkillProfile(
                rulesetOnlineId,
                producers.Select(s => s.EffectiveSystemId).ToList(),
                defaults.Count == 1 ? defaults[0] : producers[0].EffectiveSystemId);
        }
    }
}
