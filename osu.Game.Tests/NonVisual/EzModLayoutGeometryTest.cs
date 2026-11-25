// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Mods;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzModLayoutGeometryTest
    {
        [TestCase(200, 200)]
        [TestCase(570, 570)]
        [TestCase(572, 280)]
        [TestCase(900, 292)]
        [TestCase(0, 1)]
        public void TestCardWidth(float available, float expected) => Assert.That(EzModLayoutGeometry.CardWidth(available), Is.EqualTo(expected));

        [TestCase(572, 8.4f, 563.6f)]
        [TestCase(900, 8.4f, 283.6f)]
        [TestCase(2359, 8.4f, 318.3143f)]
        public void TestCardWidthReservesShear(float available, float shearWidth, float expected) =>
            Assert.That(EzModLayoutGeometry.CardWidth(available, shearWidth), Is.EqualTo(expected).Within(0.001f));

        [Test]
        public void TestCompactCardWidthIncreasesDensity()
        {
            float expanded = EzModLayoutGeometry.CardWidth(800);
            float compact = EzModLayoutGeometry.CardWidth(800, targetCardWidth: EzModLayoutGeometry.COMPACT_CARD_WIDTH);

            Assert.That(expanded, Is.EqualTo(394));
            Assert.That(compact, Is.EqualTo(191));
        }

        [TestCase(-10, -10, 0)]
        [TestCase(200, 20, 1)]
        [TestCase(700, 20, 2)]
        [TestCase(20, 70, 2)]
        [TestCase(200, 70, 3)]
        [TestCase(700, 140, 4)]
        public void TestWrappedInsertion(float x, float y, int expected)
        {
            var cards = new[] { (0f, 0f, 280f, 42f), (292f, 0f, 280f, 42f), (0f, 54f, 280f, 42f), (292f, 54f, 280f, 42f) };
            Assert.That(EzModLayoutGeometry.InsertionIndex(cards, new Vector2(x, y)), Is.EqualTo(expected));
        }

        [TestCase(-1, 0)]
        [TestCase(150, 1)]
        [TestCase(1000, 3)]
        public void TestGroupInsertion(float coordinate, int expected) =>
            Assert.That(EzModLayoutGeometry.GroupInsertionIndex(new[] { 100f, 200f, 300f }, coordinate), Is.EqualTo(expected));
    }
}
