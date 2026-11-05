// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzMinaRatingGoalTest
    {
        [Test]
        public void AccuracyBelowFloorMapsToMin()
        {
            Assert.That(EzMinaRatingGoal.ForAccuracy(0.5), Is.EqualTo(0.8f).Within(0.0001f));
        }

        [Test]
        public void AccuracyCapsAtCalcGoal()
        {
            Assert.That(EzMinaRatingGoal.ForAccuracy(1.0), Is.EqualTo(0.965f).Within(0.0001f));
        }

        [Test]
        public void NonFiniteAccuracyFallsBack()
        {
            Assert.That(EzMinaRatingGoal.ForAccuracy(double.NaN), Is.EqualTo(0.93f).Within(0.0001f));
        }

        [Test]
        public void EmptyStatisticsFallsBackToAccuracyClamp()
        {
            var score = new ScoreInfo
            {
                Accuracy = 0.99,
                IsLegacyScore = false,
                Statistics = new Dictionary<HitResult, int>(),
            };

            float? goal = EzMinaRatingGoal.ForScore(score, lnRatio: 0, od: 8);
            Assert.That(goal, Is.EqualTo(EzMinaRatingGoal.ForAccuracy(0.99)).Within(0.0001f));
        }

        [Test]
        public void AllPerfectsYieldHighWifeGoalOnLegacy()
        {
            var score = new ScoreInfo
            {
                Accuracy = 1.0,
                IsLegacyScore = true,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = 1000,
                },
            };

            float? goal = EzMinaRatingGoal.ForScore(score, lnRatio: 0, od: 8);
            Assert.That(goal, Is.Not.Null);
            Assert.That(goal!.Value, Is.GreaterThan(0.96f));
            Assert.That(goal.Value, Is.LessThanOrEqualTo((float)EzMinaRatingGoal.GOAL_CAP));
        }

        [Test]
        public void VeryLowAccuracyWithMissesReturnsNull()
        {
            var score = new ScoreInfo
            {
                Accuracy = 0.5,
                IsLegacyScore = true,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Miss] = 100,
                    [HitResult.Meh] = 10,
                },
            };

            Assert.That(EzMinaRatingGoal.ForScore(score, lnRatio: 0, od: 8), Is.Null);
        }
    }
}
