// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSOrderedHitPolicyPrecedenceTest
    {
        [Test]
        public void TestComboPrecedencePrefersEarlierComboPath()
        {
            double[] notes = { 100, 200, 300 };
            double[] presses = { 160, 220 };

            var picked = selectByPrecedence(notes, presses, EzEnumJudgePrecedence.Combo, goodEarly: 112, goodLate: 112);

            Assert.That(picked, Is.EqualTo(new[] { 100d, 200d }));
        }

        [Test]
        public void TestDurationPrecedencePrefersNearestPath()
        {
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
                        EzEnumJudgePrecedence.Combo => compareComboByPrecedence(
                            winner,
                            candidate,
                            press,
                            goodEarly,
                            goodLate),
                        EzEnumJudgePrecedence.Duration => compareDurationByPrecedence(
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

        private static bool compareComboByPrecedence(double t1, double t2, double pressTime, double goodEarly, double goodLate)
            => (bool)typeof(OrderedHitPolicyHelper).GetMethod(
                "CompareComboByPrecedence",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(double), typeof(double), typeof(double), typeof(double), typeof(double) },
                null)!.Invoke(null, new object[] { t1, t2, pressTime, goodEarly, goodLate })!;

        private static bool compareDurationByPrecedence(double t1, double t2, double pressTime)
            => (bool)typeof(OrderedHitPolicyHelper).GetMethod(
                "CompareDurationByPrecedence",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(double), typeof(double), typeof(double) },
                null)!.Invoke(null, new object[] { t1, t2, pressTime })!;
    }
}
