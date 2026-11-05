// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Per-ruleset revisions owned by the upstream difficulty layer, which Ez does not compute.
    /// </summary>
    public interface IEzMetricUpstreamRevisions
    {
        /// <summary>Applied official difficulty revision for a ruleset, or 0 when unset.</summary>
        int OfficialDifficulty(int rulesetOnlineId);

        /// <summary>Applied Sunny star rating revision for a ruleset, or 0 when unset.</summary>
        int Ssr(int rulesetOnlineId);
    }
}
