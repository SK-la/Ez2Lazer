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
    [TestFixture]
    public class PPPSkillsPresenterTest : RealmTest
    {
        [Test]
        public void Load_fills_radar_axes_excluding_aim_total()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var store = new EzChartSkillStore(realm);
                var playerStore = new EzPlayerSkillStore(storage);
                var provider = new EzSkillProvider(store, playerStore);
                const string username = "osu-track-tester";

                var skills = new Dictionary<string, double>
                {
                    [EzPPPSkillAxis.Aim.ToPlayerSkillId()] = 9.5,
                    [EzPPPSkillAxis.JumpAim.ToPlayerSkillId()] = 8.0,
                    [EzPPPSkillAxis.FlowAim.ToPlayerSkillId()] = 7.0,
                    [EzPPPSkillAxis.Precision.ToPlayerSkillId()] = 6.0,
                    [EzPPPSkillAxis.Speed.ToPlayerSkillId()] = 5.0,
                    [EzPPPSkillAxis.Stamina.ToPlayerSkillId()] = 4.0,
                    [EzPPPSkillAxis.Accuracy.ToPlayerSkillId()] = 3.0,
                };

                playerStore.WritePlayerSystemSkills(
                    username,
                    EzAxisIds.UNSLICED_KEY,
                    EzAnalysisSystem.PPP,
                    skills,
                    analyzedPlays: 12,
                    rulesetOnlineId: EzLocalProfileConstants.OSU_RULESET_ID);

                var presenter = new PPPSkillsPresenter(provider, EzLocalProfileConstants.OSU_RULESET_ID);
                var snapshot = presenter.Load(username, EzAxisIds.UNSLICED_KEY);

                Assert.That(snapshot.RadarAxes, Has.Count.EqualTo(EzOsuSkillAxisExtensions.RadarAxes.Length));
                Assert.That(snapshot.RadarAxes.Select(a => a.SkillId), Does.Not.Contain(EzPPPSkillAxis.Aim.ToPlayerSkillId()));
                Assert.That(snapshot.RadarAxes.Select(a => a.SkillId), Is.EquivalentTo(EzOsuSkillAxisExtensions.RadarAxes.Select(a => a.ToPlayerSkillId())));
                Assert.That(snapshot.BarAxes.Select(a => a.SkillId), Does.Contain(EzPPPSkillAxis.Aim.ToPlayerSkillId()));
                Assert.That(snapshot.Overall, Is.EqualTo(9.5).Within(1e-9));
                Assert.That(snapshot.PlaysAnalyzed, Is.EqualTo(12));
                Assert.That(presenter.HasSliceChips, Is.False);
                Assert.That(presenter.HasSidePanel, Is.False);
            });
        }
    }
}
