// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.ExternalRulesets
{
    /// <summary>
    /// Publishes the user-installed rulesets discovered by the official ruleset load, including those
    /// disabled by <see cref="EzRulesetMappingConfig"/>. Consumers must not re-scan the storage directory
    /// to rediscover them: whether a ruleset can be loaded is decided once, during ruleset store construction.
    /// </summary>
    public interface IEzExternalRulesetCatalog
    {
        /// <summary>
        /// User-installed rulesets that were successfully instantiated at startup, with the OnlineID each
        /// declared before any <see cref="EzRulesetMappingConfig"/> remapping was applied.
        /// </summary>
        IReadOnlyList<DiscoveredExternalRuleset> AllDiscovered { get; }
    }
}
