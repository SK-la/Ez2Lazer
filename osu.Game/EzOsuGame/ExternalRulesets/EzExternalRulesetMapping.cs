// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.ExternalRulesets
{
    /// <summary>
    /// Helpers for third-party rulesets in <c>rulesets/</c> that optionally declare an explicit <see cref="RulesetInfo.OnlineID"/>.
    /// </summary>
    public static class EzExternalRulesetMapping
    {
        public const int EXPLICIT_ONLINE_ID_MINIMUM = 4;

        public static bool HasExplicitOnlineId(Ruleset instance)
            => instance is not ILegacyRuleset && instance.RulesetInfo.OnlineID >= EXPLICIT_ONLINE_ID_MINIMUM;

        public static bool HasExplicitOnlineId(int instanceOnlineId)
            => instanceOnlineId >= EXPLICIT_ONLINE_ID_MINIMUM;

        public static bool IsOfficialOnlineId(int onlineId)
            => onlineId >= 0 && onlineId <= ILegacyRuleset.MAX_LEGACY_RULESET_ID;
    }
}
