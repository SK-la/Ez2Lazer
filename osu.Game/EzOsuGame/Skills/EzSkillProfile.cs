// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Metrics;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Ruleset-facing skill wiring: which systems are active and which drive the default radar.
    /// </summary>
    /// <remarks>
    /// Derived from the producer declaration, so consumers never hard-code a ruleset gate of their own.
    /// </remarks>
    public sealed class EzSkillProfile
    {
        public EzSkillProfile(
            int rulesetOnlineId,
            IReadOnlyList<string> chartSystemIds,
            IReadOnlyList<string> playerSystemIds,
            string? defaultChartSystemId,
            string? defaultPlayerSystemId)
        {
            RulesetOnlineId = rulesetOnlineId;
            ChartSystemIds = chartSystemIds;
            PlayerSystemIds = playerSystemIds;
            DefaultChartSystemId = defaultChartSystemId;
            DefaultPlayerSystemId = defaultPlayerSystemId;
        }

        public int RulesetOnlineId { get; }

        public IReadOnlyList<string> ChartSystemIds { get; }

        public IReadOnlyList<string> PlayerSystemIds { get; }

        public string? DefaultChartSystemId { get; }

        public string? DefaultPlayerSystemId { get; }

        public bool HasSkills => ChartSystemIds.Count > 0 || PlayerSystemIds.Count > 0;

        public bool ContainsSystem(string systemId)
            => ChartSystemIds.Contains(systemId, StringComparer.Ordinal)
               || PlayerSystemIds.Contains(systemId, StringComparer.Ordinal);

        /// <summary>
        /// Returns a profile for <paramref name="rulesetOnlineId"/>, or <see langword="null"/> when no producer
        /// declares a skill vector for it.
        /// </summary>
        public static EzSkillProfile? TryBuild(int rulesetOnlineId, EzMetricProducerRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            var chart = registry.VectorProducers(rulesetOnlineId, EzSkillScope.Beatmap).ToList();
            var player = registry.VectorProducers(rulesetOnlineId, EzSkillScope.Player).ToList();

            if (chart.Count == 0 && player.Count == 0)
                return null;

            return new EzSkillProfile(
                rulesetOnlineId,
                chart.Select(s => s.EffectiveSystemId).ToList(),
                player.Select(s => s.EffectiveSystemId).ToList(),
                pickDefault(chart, rulesetOnlineId),
                pickDefault(player, rulesetOnlineId));
        }

        private static string? pickDefault(IReadOnlyList<EzMetricProducer> producers, int rulesetOnlineId)
        {
            var defaults = producers.Where(s => s.IsDefaultRadarFor(rulesetOnlineId)).Select(s => s.EffectiveSystemId).ToList();

            if (defaults.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple default-radar producers for scope: {string.Join(", ", defaults)}");
            }

            return defaults.Count == 1 ? defaults[0] : producers.FirstOrDefault()?.EffectiveSystemId;
        }
    }
}
