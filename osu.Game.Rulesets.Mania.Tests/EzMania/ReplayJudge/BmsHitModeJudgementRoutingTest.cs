// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;
using static osu.Game.Rulesets.Mania.EzMania.ReplayJudge.ManiaColumnSimulator;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class BmsHitModeJudgementRoutingTest
    {
        [Test]
        public void TestTryRoutePostBadKPoorAppliesKPoorWhenCloserThanUnjudged()
        {
            var judgedNote = new Note { StartTime = 1000, Column = 0 };
            var laterNote = new Note { StartTime = 5000, Column = 0 };

            var laneStates = new List<LaneTargetState>
            {
                new LaneTargetState(judgedNote),
                new LaneTargetState(laterNote),
            };

            laneStates[0].Judged = true;
            laneStates[0].Result = BmsHitModeJudgement.MapTo(BmsJudge.Bad);
            laneStates[0].BmsRoute.CanRouteToKPoor = true;

            var helper = new HitModeHelper(EzEnumHitMode.IIDX_HD)
            {
                OverallDifficulty = 5,
                BPM = 120,
            };

            HitResult? routedResult = null;

            bool routed = BmsHitModeJudgement.Instance.TryRoutePostBadKPoor(
                laneStates,
                new[] { laneStates[1] },
                inputTime: 1180,
                offsetPlusMania: 0,
                helper,
                (_, result) => routedResult = result);

            Assert.That(routed, Is.True);
            Assert.That(routedResult, Is.EqualTo(BmsHitModeJudgement.MapTo(BmsJudge.KPoor)));
            Assert.That(laneStates[0].BmsRoute.HasLateKPoor, Is.True);
            Assert.That(laneStates[0].BmsRoute.CanRouteToKPoor, Is.False);
        }
    }
}
