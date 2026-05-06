// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public class JudgePrecedenceRoutingRegressionTest
    {
        [TestCase(EzEnumHitMode.Classic)]
        [TestCase(EzEnumHitMode.EZ2AC)]
        [TestCase(EzEnumHitMode.O2Jam)]
        [TestCase(EzEnumHitMode.Malody_B)]
        [TestCase(EzEnumHitMode.Malody_E)]
        public void TestMissOnlyOverlapIsFilteredInNonBmsModes(EzEnumHitMode mode)
        {
            // Dense pattern: two close notes and a very late press.
            // Earlier note is already outside user-triggered hit range (Result=None).
            // Later note is still hittable; precedence must never route to the None candidate.
            var noteTimes = new[] { 1000d, 1080d };
            var helper = new HitModeHelper(mode)
            {
                OverallDifficulty = 5,
                BPM = 180
            };
            double pressTime = noteTimes[1] + helper.WindowFor(HitResult.Miss) - 5;

            double? selected = selectJudgeableByPrecedence(noteTimes, pressTime, mode, EzEnumJudgePrecedence.Duration);

            Assert.That(selected, Is.EqualTo(1080d), $"mode={mode} should pick still-judgeable note, not miss-only overlap note.");
        }

        [Test]
        public void TestRajaKeepsJudgeableCandidateInLatePressRegion()
        {
            // Raja has BMS-like extended recognition (Poor/KPoor related), so this late press remains routable.
            var noteTimes = new[] { 1000d, 1080d };
            const double pressTime = 1240d;

            double? selected = selectJudgeableByPrecedence(noteTimes, pressTime, EzEnumHitMode.Raja_NM, EzEnumJudgePrecedence.Duration);

            Assert.That(selected, Is.Not.Null);
        }

        [TestCase(EzEnumHitMode.Classic)]
        [TestCase(EzEnumHitMode.EZ2AC)]
        [TestCase(EzEnumHitMode.O2Jam)]
        [TestCase(EzEnumHitMode.Malody_B)]
        [TestCase(EzEnumHitMode.Malody_E)]
        [TestCase(EzEnumHitMode.Raja_NM)]
        public void TestSameTimestampReleaseThenPressStillRoutesToNearestJudgeable(EzEnumHitMode mode)
        {
            // Simulate short-LN boundary scenario: after releasing at tail time, a press on the same timestamp
            // should route to the nearest still-judgeable object in dense spacing.
            var noteTimes = new[] { 1000d, 1012d, 1024d };
            const double sameFramePress = 1024d;

            double? selected = selectJudgeableByPrecedence(noteTimes, sameFramePress, mode, EzEnumJudgePrecedence.Duration);

            Assert.That(selected, Is.EqualTo(1024d), $"mode={mode} should route same-frame press to nearest judgeable note.");
        }

        private static double? selectJudgeableByPrecedence(
            IEnumerable<double> noteTimes,
            double pressTime,
            EzEnumHitMode mode,
            EzEnumJudgePrecedence precedence)
        {
            var helper = new HitModeHelper(mode)
            {
                OverallDifficulty = 5,
                BPM = 180
            };

            var judgeable = noteTimes
                            .Where(t => helper.ResultFor(pressTime - t) != HitResult.None)
                            .OrderBy(t => t)
                            .ToList();

            if (judgeable.Count == 0)
                return null;

            double selected = judgeable[0];

            for (int i = 1; i < judgeable.Count; i++)
            {
                double candidate = judgeable[i];

                bool replace = precedence switch
                {
                    EzEnumJudgePrecedence.Combo => OrderedHitPolicyHelper.CompareComboByPrecedence(
                        selected,
                        candidate,
                        pressTime,
                        helper.WindowFor(HitResult.Good, true),
                        helper.WindowFor(HitResult.Good, false)),
                    EzEnumJudgePrecedence.Duration => OrderedHitPolicyHelper.CompareDurationByPrecedence(
                        selected,
                        candidate,
                        pressTime),
                    _ => false
                };

                if (replace)
                    selected = candidate;
            }

            return selected;
        }
    }
}
