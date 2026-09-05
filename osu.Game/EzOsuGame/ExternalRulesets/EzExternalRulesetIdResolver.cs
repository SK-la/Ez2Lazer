// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.ExternalRulesets
{
    public readonly record struct ExternalRulesetMappingInput(
        string ShortName,
        int InstanceOnlineId,
        bool Enabled,
        int? IniOnlineId,
        int IniOrder,
        int DiscoveryIndex);

    public readonly record struct ExternalRulesetMappingOutput(
        string ShortName,
        int ResolvedOnlineId,
        bool Enabled,
        bool WasRemapped,
        bool ParticipatesInMapping);

    public static class EzExternalRulesetIdResolver
    {
        public static List<ExternalRulesetMappingOutput> Resolve(
            IReadOnlyList<ExternalRulesetMappingInput> inputs,
            HashSet<int> usedOfficialAndMappedIds)
        {
            var results = new List<ExternalRulesetMappingOutput>();
            var used = new HashSet<int>(usedOfficialAndMappedIds);

            var ordered = inputs
                          .OrderBy(i => i.IniOrder)
                          .ThenBy(i => i.DiscoveryIndex)
                          .ToList();

            foreach (var input in ordered)
            {
                if (!input.Enabled)
                {
                    results.Add(new ExternalRulesetMappingOutput(input.ShortName, input.InstanceOnlineId, false, false, false));
                    continue;
                }

                bool hasExplicitIniId = input.IniOnlineId is int iniId && iniId >= EzExternalRulesetMapping.EXPLICIT_ONLINE_ID_MINIMUM;
                bool hasExplicitInstanceId = EzExternalRulesetMapping.HasExplicitOnlineId(input.InstanceOnlineId);

                if (!hasExplicitIniId && !hasExplicitInstanceId)
                {
                    results.Add(new ExternalRulesetMappingOutput(input.ShortName, input.InstanceOnlineId, true, false, false));
                    continue;
                }

                int candidate = hasExplicitIniId
                    ? input.IniOnlineId!.Value
                    : input.InstanceOnlineId;

                bool wasRemapped = false;

                if (used.Contains(candidate))
                {
                    candidate = nextFreeId(used);
                    wasRemapped = true;
                }

                used.Add(candidate);

                results.Add(new ExternalRulesetMappingOutput(input.ShortName, candidate, true, wasRemapped, true));
            }

            return results;
        }

        private static int nextFreeId(HashSet<int> used)
        {
            int candidate = EzExternalRulesetMapping.EXPLICIT_ONLINE_ID_MINIMUM;

            while (used.Contains(candidate))
                candidate++;

            return candidate;
        }
    }
}
