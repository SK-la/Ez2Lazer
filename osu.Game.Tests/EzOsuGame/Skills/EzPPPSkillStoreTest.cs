// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.Rulesets;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    [Ignore("[EZ-REALTIME] Realm v12 写入已屏蔽（实时验证期）；恢复时取消注释。")]
    public class EzPPPSkillStoreTest : RealmTest
    {
        [Test]
        public void Bundle_version_read_drops_rows_on_ruleset_bump()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzChartSkillStore(realm);
                const string hash = "ppp-version-hash";
                string aim = EzPPPSkillAxis.Aim.ToChartSkillId();
                string speed = EzPPPSkillAxis.Speed.ToChartSkillId();
                int staleBundle = EzAnalysis.VERSION - 1;

                store.WriteBeatmapSystemSkills(
                    hash,
                    EzAnalysisSystem.PPP,
                    new Dictionary<string, double>
                    {
                        [aim] = 1.5,
                        [speed] = 2.5,
                    },
                    skillVersions: new Dictionary<string, int>
                    {
                        [aim] = staleBundle,
                        [speed] = EzAnalysis.VERSION,
                    });

                var beatmapInfo = new BeatmapInfo
                {
                    Hash = hash,
                    Ruleset = new RulesetInfo("osu", "osu!", "osu.Game.Rulesets.Osu.OsuRuleset, osu.Game.Rulesets.Osu", 0),
                };

                var current = store.GetBeatmapSkillsAtCurrentVersions(beatmapInfo, EzAnalysisSystem.PPP);

                Assert.That(current.ContainsKey(aim), Is.False);
                Assert.That(current[speed], Is.EqualTo(2.5));
            });
        }
    }
}
