// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Catalog of skill systems (mania Mina/SSR/pattern/dan + osu PP+ chart/player, …).
    /// Ruleset consumers should prefer <see cref="GetProfile"/> over hard-coded OnlineID checks.
    /// </summary>
    public sealed class EzSkillRegistry
    {
        private readonly Dictionary<string, IEzSkillSystem> systems = new Dictionary<string, IEzSkillSystem>(StringComparer.Ordinal);

        public EzSkillRegistry(IEnumerable<IEzSkillSystem>? systems = null)
        {
            foreach (var system in systems ?? CreateDefaultSystems())
                Register(system);
        }

        public void Register(IEzSkillSystem system)
        {
            ArgumentNullException.ThrowIfNull(system);
            systems[system.SystemId] = system;
        }

        public IEzSkillSystem? GetSystem(string systemId)
            => systems.GetValueOrDefault(systemId);

        public IReadOnlyList<IEzSkillSystem> Systems => systems.Values.ToList();

        public IEnumerable<EzSkillDefinition> AllSkills
            => systems.Values.SelectMany(s => s.Skills);

        public IEnumerable<IEzSkillSystem> GetSystemsForRuleset(int rulesetOnlineId)
            => systems.Values.Where(s => s.AppliesToRuleset(rulesetOnlineId));

        public EzSkillProfile? GetProfile(int rulesetOnlineId)
            => EzSkillProfile.TryBuild(rulesetOnlineId, this);

        public static IEnumerable<IEzSkillSystem> CreateDefaultSystems()
        {
            // Mania plugins
            yield return new BeatmapMsdSkillSystem();
            yield return new PlayerSsrSkillSystem();
            yield return new PlayerPatternSkillSystem();
            yield return new DanSkillSystem();

            // osu!standard plugins (PP+-shaped; stub engine until authorised)
            yield return new BeatmapPpPlusSkillSystem();
            yield return new PlayerPpPlusSkillSystem();
        }
    }
}
