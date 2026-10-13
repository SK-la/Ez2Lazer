// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.EzOsuGame.LocalProfile
{
    [TestFixture]
    public class OsuTrackSkillsPresenterTest : RealmTest
    {
        [Test]
        public void Load_fills_radar_axes_excluding_aim_total()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                var provider = new EzSkillProvider(store);
                const string username = "osu-track-tester";

                var skills = new Dictionary<string, double>
                {
                    [EzOsuSkillAxis.Aim.ToPlayerSkillId()] = 9.5,
                    [EzOsuSkillAxis.JumpAim.ToPlayerSkillId()] = 8.0,
                    [EzOsuSkillAxis.FlowAim.ToPlayerSkillId()] = 7.0,
                    [EzOsuSkillAxis.Precision.ToPlayerSkillId()] = 6.0,
                    [EzOsuSkillAxis.Speed.ToPlayerSkillId()] = 5.0,
                    [EzOsuSkillAxis.Stamina.ToPlayerSkillId()] = 4.0,
                    [EzOsuSkillAxis.Accuracy.ToPlayerSkillId()] = 3.0,
                };

                store.WritePlayerSystemSkills(
                    username,
                    EzSkillSystems.OSU_SLICE_KEY,
                    EzSkillSystems.PLAYER_PPPLUS,
                    skills,
                    analyzedPlays: 12);

                var presenter = new OsuTrackSkillsPresenter(provider);
                var snapshot = presenter.Load(username, EzSkillSystems.OSU_SLICE_KEY);

                Assert.That(snapshot.RadarAxes, Has.Count.EqualTo(EzOsuSkillAxisExtensions.RadarAxes.Length));
                Assert.That(snapshot.RadarAxes.Select(a => a.SkillId), Does.Not.Contain(EzOsuSkillAxis.Aim.ToPlayerSkillId()));
                Assert.That(snapshot.RadarAxes.Select(a => a.SkillId), Is.EquivalentTo(EzOsuSkillAxisExtensions.RadarAxes.Select(a => a.ToPlayerSkillId())));
                Assert.That(snapshot.BarAxes.Select(a => a.SkillId), Does.Contain(EzOsuSkillAxis.Aim.ToPlayerSkillId()));
                Assert.That(snapshot.Overall, Is.EqualTo(9.5).Within(1e-9));
                Assert.That(snapshot.PlaysAnalyzed, Is.EqualTo(12));
                Assert.That(presenter.HasSliceChips, Is.False);
                Assert.That(presenter.HasSidePanel, Is.False);
            });
        }
    }
}
