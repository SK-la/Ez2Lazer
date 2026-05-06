// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Objects;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSHoldNoteNestedTest
    {
        [Test]
        public void TestHoldNoteCreatesHeadBodyTailNestedObjects()
        {
            var hold = new BMSHoldNote
            {
                StartTime = 1000,
                Duration = 600,
                Column = 3,
            };

            typeof(BMSHoldNote).GetMethod("CreateNestedHitObjects", BindingFlags.Instance | BindingFlags.NonPublic)!
                               .Invoke(hold, new object[] { CancellationToken.None });

            Assert.That(hold.NestedHitObjects.Count, Is.EqualTo(3));
            Assert.That(hold.NestedHitObjects.OfType<BMSHoldNoteHead>().Single().StartTime, Is.EqualTo(1000).Within(0.01));
            Assert.That(hold.NestedHitObjects.OfType<BMSHoldNoteTail>().Single().StartTime, Is.EqualTo(1600).Within(0.01));
            Assert.That(hold.NestedHitObjects.OfType<BMSHoldNoteBody>().Single().Duration, Is.EqualTo(600).Within(0.01));
            Assert.That(hold.NestedHitObjects.All(n => ((BMSHitObject)n).Column == 3), Is.True);
        }
    }
}
