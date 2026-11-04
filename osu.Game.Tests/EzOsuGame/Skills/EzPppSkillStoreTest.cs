// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzPppSkillStoreTest : RealmTest
    {
        [Test]
        public void Bundle_version_read_drops_rows_on_ruleset_bump()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                const string hash = "ppp-version-hash";
                string aim = EzOsuSkillAxis.Aim.ToChartSkillId();
                string speed = EzOsuSkillAxis.Speed.ToChartSkillId();
                int staleBundle = EzPppSkillVersions.ForRuleset(0) - 1;

                store.WriteBeatmapSystemSkills(
                    hash,
                    EzSkillSystems.BEATMAP_PPPLUS,
                    new Dictionary<string, double>
                    {
                        [aim] = 1.5,
                        [speed] = 2.5,
                    },
                    skillVersions: new Dictionary<string, int>
                    {
                        [aim] = staleBundle,
                        [speed] = EzPppSkillVersions.ForRuleset(0),
                    });

                var current = store.GetBeatmapSkillsAtCurrentVersions(hash, EzSkillSystems.BEATMAP_PPPLUS);

                Assert.That(current.ContainsKey(aim), Is.False);
                Assert.That(current[speed], Is.EqualTo(2.5));
            });
        }
    }
}
