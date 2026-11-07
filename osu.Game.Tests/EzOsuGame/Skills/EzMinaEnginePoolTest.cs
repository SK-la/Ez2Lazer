// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Skills.Mina;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzMinaEnginePoolTest
    {
        [TearDown]
        public void TearDown() => EzMinaEnginePool.Release();

        [Test]
        public void RentReturnsTheSameEngineOnOneThread()
        {
            var first = EzMinaEnginePool.Rent();
            var second = EzMinaEnginePool.Rent();

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void RentAfterReleaseCreatesANewEngine()
        {
            var first = EzMinaEnginePool.Rent();
            EzMinaEnginePool.Release();

            var second = EzMinaEnginePool.Rent();

            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void PooledEngineStillRatesCharts()
        {
            var notes = new[]
            {
                new EzCalcNote(0b0001, 0f),
                new EzCalcNote(0b0010, 0.1f),
                new EzCalcNote(0b0100, 0.2f),
                new EzCalcNote(0b1000, 0.3f),
            };

            var first = EzMinaEnginePool.Rent().CalculateMsd(notes, 4);
            var second = EzMinaEnginePool.Rent().CalculateMsd(notes, 4);

            Assert.That(first.Overall, Is.GreaterThan(0));
            Assert.That(second.Overall, Is.EqualTo(first.Overall).Within(1e-9));
        }
    }
}
