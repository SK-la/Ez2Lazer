// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.ExternalRulesets
{
    public static class EzExternalRulesetScanner
    {
        private const string ruleset_library_prefix = @"osu.Game.Rulesets";

        public static List<DiscoveredExternalRuleset> Scan(Storage storage)
        {
            var results = new List<DiscoveredExternalRuleset>();
            var rulesetStorage = storage.GetStorageForDirectory(@"rulesets");

            foreach (string file in rulesetStorage.GetFiles(@".", @$"{ruleset_library_prefix}.*.dll").Where(f => !f.Contains(@"Tests")))
            {
                try
                {
                    string fullPath = rulesetStorage.GetFullPath(file);
                    var assembly = Assembly.LoadFrom(fullPath);
                    Type rulesetType = assembly.GetTypes().First(t => t.IsPublic && t.IsSubclassOf(typeof(Ruleset)));
                    var instance = (Ruleset)Activator.CreateInstance(rulesetType)!;

                    results.Add(new DiscoveredExternalRuleset(instance.ShortName, instance.RulesetInfo.Name, instance.RulesetInfo.OnlineID));
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to inspect external ruleset '{Path.GetFileName(file)}': {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
                }
            }

            return results
                   .OrderBy(r => r.ShortName, StringComparer.Ordinal)
                   .ToList();
        }
    }
}
