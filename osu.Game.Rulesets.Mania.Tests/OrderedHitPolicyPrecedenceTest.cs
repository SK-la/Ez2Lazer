// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public class OrderedHitPolicyPrecedenceTest
    {
        [Test]
        public void TestComboPrecedencePrefersEarlierComboPath()
        {
            // note times: 100 / 200 / 300
            // press times: 160 / 220
            // expected combo path: 100 then 200
            double[] notes = { 100, 200, 300 };
            double[] presses = { 160, 220 };

            var picked = selectByPrecedence(notes, presses, EzEnumJudgePrecedence.Combo, goodEarly: 112, goodLate: 112);

            Assert.That(picked, Is.EqualTo(new[] { 100d, 200d }));
        }

        [Test]
        public void TestDurationPrecedencePrefersNearestPath()
        {
            // note times: 100 / 200 / 300
            // press times: 160 / 220
            // expected duration path: 200 then 300
            double[] notes = { 100, 200, 300 };
            double[] presses = { 160, 220 };

            var picked = selectByPrecedence(notes, presses, EzEnumJudgePrecedence.Duration, goodEarly: 112, goodLate: 112);

            Assert.That(picked, Is.EqualTo(new[] { 200d, 300d }));
        }

        private static List<double> selectByPrecedence(
            IEnumerable<double> noteTimes,
            IEnumerable<double> pressTimes,
            EzEnumJudgePrecedence precedence,
            double goodEarly,
            double goodLate)
        {
            var remaining = noteTimes.OrderBy(t => t).ToList();
            var selected = new List<double>();

            foreach (double press in pressTimes)
            {
                if (remaining.Count == 0)
                    break;

                double winner = remaining[0];

                for (int i = 1; i < remaining.Count; i++)
                {
                    double candidate = remaining[i];
                    bool shouldReplace = precedence switch
                    {
                        EzEnumJudgePrecedence.Combo => OrderedHitPolicyHelper.CompareComboByPrecedence(
                            winner,
                            candidate,
                            press,
                            goodEarly,
                            goodLate),
                        EzEnumJudgePrecedence.Duration => OrderedHitPolicyHelper.CompareDurationByPrecedence(
                            winner,
                            candidate,
                            press),
                        _ => false
                    };

                    if (shouldReplace)
                        winner = candidate;
                }

                selected.Add(winner);
                remaining.Remove(winner);
            }

            return selected;
        }
    }
}
