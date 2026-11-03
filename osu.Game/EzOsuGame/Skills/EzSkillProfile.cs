// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Ruleset-facing skill wiring: which systems are active and which drive the default radar.
    /// Built from <see cref="EzSkillRegistry"/> — UI/HUD should query this instead of hard-coding OnlineID gates.
    /// </summary>
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
        /// Returns a profile for <paramref name="rulesetOnlineId"/>, or <see langword="null"/> when no
        /// registered system applies (taiko/catch today).
        /// </summary>
        public static EzSkillProfile? TryBuild(int rulesetOnlineId, EzSkillRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            var applicable = registry.Systems
                                     .Where(s => s.AppliesToRuleset(rulesetOnlineId))
                                     .ToList();

            if (applicable.Count == 0)
                return null;

            var chart = applicable.Where(s => s.Scope == EzSkillScope.Beatmap).ToList();
            var player = applicable.Where(s => s.Scope == EzSkillScope.Player).ToList();

            string? defaultChart = pickDefault(chart);
            string? defaultPlayer = pickDefault(player);

            return new EzSkillProfile(
                rulesetOnlineId,
                chart.Select(s => s.SystemId).ToList(),
                player.Select(s => s.SystemId).ToList(),
                defaultChart,
                defaultPlayer);
        }

        private static string? pickDefault(IReadOnlyList<IEzSkillSystem> systems)
        {
            var defaults = systems.Where(s => s.IsDefaultRadar).Select(s => s.SystemId).ToList();

            if (defaults.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple default-radar systems for scope: {string.Join(", ", defaults)}");
            }

            return defaults.Count == 1 ? defaults[0] : systems.FirstOrDefault()?.SystemId;
        }
    }
}
