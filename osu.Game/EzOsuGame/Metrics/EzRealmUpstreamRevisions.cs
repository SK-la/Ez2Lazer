// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Official difficulty and Sunny revisions as recorded on the ruleset row.
    /// </summary>
    /// <remarks>
    /// These are the values a stored baseline was stamped with, not the calculator's current version:
    /// a ruleset that still owes a difficulty upgrade keeps its previous number until the backfill
    /// resets the columns and advances the row, so the number here always describes what is on disk.
    /// </remarks>
    public sealed class EzRealmUpstreamRevisions : IEzMetricUpstreamRevisions
    {
        private readonly IRulesetStore rulesetStore;

        public EzRealmUpstreamRevisions(IRulesetStore rulesetStore)
        {
            this.rulesetStore = rulesetStore;
        }

        public int OfficialDifficulty(int rulesetOnlineId)
            => rulesetStore.GetRuleset(rulesetOnlineId) is RulesetInfo ruleset ? ruleset.LastAppliedDifficultyVersion : 0;

        public int Ssr(int rulesetOnlineId)
            => rulesetStore.GetRuleset(rulesetOnlineId) is RulesetInfo ruleset ? ruleset.LastAppliedSunnySRVersion : 0;
    }

    /// <summary>
    /// Upstream revisions that are always zero. Used as the default before a real store is installed,
    /// so unit tests and static initialisers never depend on a ruleset database.
    /// </summary>
    public sealed class EzNullUpstreamRevisions : IEzMetricUpstreamRevisions
    {
        public static readonly EzNullUpstreamRevisions INSTANCE = new EzNullUpstreamRevisions();

        private EzNullUpstreamRevisions()
        {
        }

        public int OfficialDifficulty(int rulesetOnlineId) => 0;

        public int Ssr(int rulesetOnlineId) => 0;
    }
}
