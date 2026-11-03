// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.EzOsuGame.Screens.Rotation;

namespace osu.Game.Tests.EzOsuGame.Rotation
{
    [TestFixture]
    public class TestEzQuickRotationDifficultyBalancer
    {
        [Test]
        public void TestIterativeStepConvergesWithinTolerance()
        {
            const double target = 4.0;
            const double tolerance = 0.5;

            double accumulatedStep = 0;
            double speed = 1.0;
            double measured = measureAtSpeed(1.0);

            for (int i = 0; i < EzQuickRotationDifficultyBalancer.MAX_ITERATIONS; i++)
            {
                if (Math.Abs(measured - target) <= tolerance)
                    break;

                double step = (measured - target) / 10.0;
                accumulatedStep += step;
                speed = Math.Clamp(1.0 - accumulatedStep, EzQuickRotationDifficultyHelper.MIN_SPEED, EzQuickRotationDifficultyHelper.MAX_SPEED);
                measured = measureAtSpeed(speed);
            }

            Assert.That(speed, Is.EqualTo(0.64).Within(0.001));
            Assert.That(measured, Is.EqualTo(4.3).Within(0.001));
            Assert.That(Math.Abs(measured - target), Is.LessThanOrEqualTo(tolerance));
        }

        private static double measureAtSpeed(double speed)
        {
            if (speed >= 0.99)
                return 6.7;

            if (speed >= 0.7)
                return 4.9;

            return 4.3;
        }
    }
}
