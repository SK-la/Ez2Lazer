// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Logging;
using Realms;

namespace osu.Game.Database
{
    internal static class EzRealmMigrationContributorRegistry
    {
        private const string ruleset_assembly_prefix = "osu.Game.Rulesets";

        private static IEzRealmMigrationContributor[]? cached;

        public static void ApplyContributors(Migration migration, int targetEzVersion)
        {
            foreach (var contributor in getContributors())
            {
                if (!contributor.TargetEzVersions.Contains(targetEzVersion))
                    continue;

                contributor.Apply(migration, targetEzVersion);
            }
        }

        private static IEzRealmMigrationContributor[] getContributors()
        {
            if (cached != null)
                return cached;

            var contributors = new List<IEzRealmMigrationContributor>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string? name = assembly.GetName().Name;

                if (name == null || !name.StartsWith(ruleset_assembly_prefix, StringComparison.Ordinal) || name.Contains("Tests", StringComparison.Ordinal))
                    continue;

                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).Cast<Type>().ToArray();
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface || !typeof(IEzRealmMigrationContributor).IsAssignableFrom(type))
                        continue;

                    try
                    {
                        if (Activator.CreateInstance(type) is IEzRealmMigrationContributor contributor)
                            contributors.Add(contributor);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Failed to create {nameof(IEzRealmMigrationContributor)} '{type.FullName}'.");
                    }
                }
            }

            return cached = contributors.ToArray();
        }
    }
}
