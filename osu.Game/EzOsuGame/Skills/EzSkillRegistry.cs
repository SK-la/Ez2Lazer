// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Catalog of skill systems. Default systems cover current Mina MSD/SSR + dan tracks for keys 4–9.
    /// Reserved: register additional <see cref="IEzSkillSystem"/> implementations for keymode-specific axis sets
    /// (do not replace this with a hard-coded Mina-only list when that lands).
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

        public static IEnumerable<IEzSkillSystem> CreateDefaultSystems()
        {
            yield return new BeatmapMsdSkillSystem();
            yield return new PlayerSsrSkillSystem();
            yield return new DanSkillSystem();
        }
    }
}
