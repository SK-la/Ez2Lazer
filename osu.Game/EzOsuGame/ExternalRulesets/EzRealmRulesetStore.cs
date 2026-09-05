// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;
using Realms;

namespace osu.Game.EzOsuGame.ExternalRulesets
{
    public class EzRealmRulesetStore : RealmRulesetStore
    {
        private EzRulesetMappingConfig? mappingConfig;
        private readonly HashSet<string> disabledByConfig = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> mappedExplicitShortNames = new HashSet<string>(StringComparer.Ordinal);

        public EzRealmRulesetStore(RealmAccess realmAccess, Storage? storage = null)
            : base(realmAccess, storage)
        {
            ArgumentNullException.ThrowIfNull(storage);
        }

        protected override void OnBeforeRulesetValidation(Realm realm, IQueryable<RulesetInfo> rulesets, List<Ruleset> instances)
        {
            // GameStorage is set in RulesetStore's ctor before PrepareDetachedRulesets runs.
            var storage = GameStorage ?? throw new InvalidOperationException($"{nameof(GameStorage)} is required for external ruleset mapping.");

            mappingConfig = EzRulesetMappingConfig.Load(storage);

            var userInstances = instances
                                .Where(i => IsUserRulesetAssembly(i.GetType().Assembly))
                                .ToList();

            mappingConfig.EnsureDefaults(userInstances.Select(i =>
                new DiscoveredExternalRuleset(i.ShortName, i.RulesetInfo.Name, i.RulesetInfo.OnlineID)));

            var usedIds = new HashSet<int> { 0, 1, 2, 3 };

            var inputs = new List<ExternalRulesetMappingInput>();

            for (int i = 0; i < userInstances.Count; i++)
            {
                var instance = userInstances[i];
                var entry = mappingConfig.GetOrAdd(instance.ShortName);

                inputs.Add(new ExternalRulesetMappingInput(
                    instance.ShortName,
                    instance.RulesetInfo.OnlineID,
                    entry.Enabled,
                    entry.OnlineID,
                    entry.Order ?? int.MaxValue,
                    i));
            }

            var outputs = EzExternalRulesetIdResolver.Resolve(inputs, usedIds);

            disabledByConfig.Clear();
            mappedExplicitShortNames.Clear();

            bool configChanged = false;

            foreach (var output in outputs)
            {
                var entry = mappingConfig.GetOrAdd(output.ShortName);

                if (!output.Enabled)
                {
                    disabledByConfig.Add(output.ShortName);
                    continue;
                }

                var realmEntry = rulesets.FirstOrDefault(r => r.ShortName == output.ShortName);

                if (realmEntry == null)
                    continue;

                if (output.ParticipatesInMapping)
                {
                    mappedExplicitShortNames.Add(output.ShortName);

                    if (realmEntry.OnlineID != output.ResolvedOnlineId)
                        realmEntry.OnlineID = output.ResolvedOnlineId;

                    if (entry.OnlineID != output.ResolvedOnlineId)
                    {
                        entry.OnlineID = output.ResolvedOnlineId;
                        configChanged = true;
                    }

                    if (output.WasRemapped)
                    {
                        Logger.Log(
                            $"External ruleset '{output.ShortName}' OnlineID conflict resolved: mapped to {output.ResolvedOnlineId}. See EzRulesetMapping.ini.",
                            Ez2ConfigManager.LOGGER_NAME,
                            LogLevel.Important);
                        configChanged = true;
                    }
                }
            }

            if (configChanged)
                mappingConfig.Save(storage);
        }

        protected override bool ShouldDisableRuleset(RulesetInfo ruleset)
            => disabledByConfig.Contains(ruleset.ShortName);

        protected override bool ShouldAllowOnlineIdMismatch(RulesetInfo ruleset, Ruleset instance, int instanceOnlineId)
            => IsUserRulesetAssembly(instance.GetType().Assembly) && mappedExplicitShortNames.Contains(ruleset.ShortName);

        protected override bool ShouldSkipDuplicateOnlineIdCheck(RulesetInfo ruleset, int onlineId, IQueryable<RulesetInfo> allRulesets)
        {
            if (mappedExplicitShortNames.Contains(ruleset.ShortName))
                return true;

            if (onlineId < EzExternalRulesetMapping.EXPLICIT_ONLINE_ID_MINIMUM)
                return true;

            return false;
        }
    }
}
