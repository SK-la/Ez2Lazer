// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.EzOsuGame.LocalProfile
{
    /// <summary>
    /// PP+ covers every ruleset and its rows are keyed by ruleset. These pin the two consequences: a ruleset's rows
    /// live under its own id, and a pass that covers one ruleset cannot clear another's rows.
    /// </summary>
    [TestFixture]
    public class EzPlayerPppScopeTest : RealmTest
    {
        [Test]
        public void Every_supported_ruleset_keeps_its_own_rows()
        {
            RunTestWithRealm((_, storage) =>
            {
                var store = new EzPlayerSkillStore(storage);
                const string username = "ppp-scope";

                foreach (int rulesetId in EzPPPSkillCatalog.SupportedRulesets)
                {
                    store.WritePlayerSystemSkills(
                        username,
                        sliceKeyOf(rulesetId),
                        EzAnalysisSystem.PPP,
                        skillsFor(rulesetId),
                        analyzedPlays: 3,
                        rulesetOnlineId: rulesetId);
                }

                foreach (int rulesetId in EzPPPSkillCatalog.SupportedRulesets)
                {
                    var skills = store.GetPlayerSkills(username, sliceKeyOf(rulesetId), EzAnalysisSystem.PPP, rulesetOnlineId: rulesetId);

                    Assert.That(skills, Is.Not.Empty, $"ruleset {rulesetId}");
                    Assert.That(skills.Keys, Is.EquivalentTo(new[] { EzPPPSkillCatalog.STAR_RATING, summaryAxisOf(rulesetId) }));
                }
            });
        }

        [Test]
        public void Clearing_one_ruleset_leaves_the_others_alone()
        {
            RunTestWithRealm((_, storage) =>
            {
                var store = new EzPlayerSkillStore(storage);
                const string username = "ppp-scope-clear";

                foreach (int rulesetId in EzPPPSkillCatalog.SupportedRulesets)
                {
                    store.WritePlayerSystemSkills(
                        username,
                        sliceKeyOf(rulesetId),
                        EzAnalysisSystem.PPP,
                        skillsFor(rulesetId),
                        analyzedPlays: 3,
                        rulesetOnlineId: rulesetId);
                }

                // A pass over taiko that found nothing to rate must not take osu's rows with it.
                store.ClearPlayerSystemSkills(username, EzAnalysisSystem.PPP, EzLocalProfileConstants.TAIKO_RULESET_ID);

                Assert.That(store.GetPlayerSkills(username, EzAxisIds.UNSLICED_KEY, EzAnalysisSystem.PPP, rulesetOnlineId: EzLocalProfileConstants.TAIKO_RULESET_ID),
                    Is.Empty);
                Assert.That(store.GetPlayerSkills(username, EzAxisIds.UNSLICED_KEY, EzAnalysisSystem.PPP, rulesetOnlineId: EzLocalProfileConstants.OSU_RULESET_ID),
                    Is.Not.Empty);
                Assert.That(store.GetPlayerSkills(username, EzAxisIds.UNSLICED_KEY, EzAnalysisSystem.PPP, rulesetOnlineId: EzLocalProfileConstants.CATCH_RULESET_ID),
                    Is.Not.Empty);
            });
        }

        [Test]
        public void A_keymode_sliced_ruleset_reports_the_slices_it_was_written_with()
        {
            RunTestWithRealm((_, storage) =>
            {
                var store = new EzPlayerSkillStore(storage);
                const string username = "ppp-slices";

                foreach (int keyCount in new[] { 4, 7 })
                {
                    store.WritePlayerSystemSkills(
                        username,
                        keyCount,
                        EzAnalysisSystem.PPP,
                        skillsFor(EzLocalProfileConstants.MANIA_RULESET_ID),
                        analyzedPlays: 2,
                        rulesetOnlineId: EzLocalProfileConstants.MANIA_RULESET_ID);
                }

                var slices = store.GetPlayerKeyCounts(username, EzAnalysisSystem.PPP, EzLocalProfileConstants.MANIA_RULESET_ID);

                Assert.That(slices, Is.EqualTo(new[] { 4, 7 }));
            });
        }

        private static int sliceKeyOf(int rulesetOnlineId)
            => EzPPPSkillCatalog.PlayerSliceKey(rulesetOnlineId, keyCount: 4);

        private static string summaryAxisOf(int rulesetOnlineId)
        {
            // A radar axis as the second row: every ruleset's catalog declares at least one, and it keeps the test
            // independent of which metric family the ruleset happens to have.
            return EzPPPSkillCatalog.MetricsFor(rulesetOnlineId).First(spec => spec.InRadar).PlayerSkillId;
        }

        private static IReadOnlyDictionary<string, double> skillsFor(int rulesetOnlineId)
            => new Dictionary<string, double>
            {
                [EzPPPSkillCatalog.STAR_RATING] = 5,
                [summaryAxisOf(rulesetOnlineId)] = 4,
            };
    }
}
