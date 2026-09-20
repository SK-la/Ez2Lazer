// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;

#if DEBUG
namespace osu.Game.Rulesets.Mania.Tests.EzMania.Diagnostics
{
    [TestFixture]
    public class ManiaHoldAblationTest
    {
        [TearDown]
        public void TearDown() => ManiaHoldAblation.Reset();

        [Test]
        public void TestHoldEndsAreExcludedFromNonPositionalInputByDefault()
        {
            var hold = new DrawableHoldNote();
            var head = new DrawableHoldNoteHead();
            var tail = new DrawableHoldNoteTail();

            Assert.That(hold.HandleNonPositionalInput, Is.True);
            Assert.That(head.HandleNonPositionalInput, Is.False);
            Assert.That(tail.HandleNonPositionalInput, Is.False);
        }

        [Test]
        public void TestHoldEndsCanBeRequeuedForAblation()
        {
            ManiaHoldAblation.EnqueueHoldEnds = true;

            var head = new DrawableHoldNoteHead();
            var tail = new DrawableHoldNoteTail();

            Assert.That(head.HandleNonPositionalInput, Is.True);
            Assert.That(tail.HandleNonPositionalInput, Is.True);
        }

        [Test]
        public void TestForceTickGenerationCreatesSixteenthTicks()
        {
            ManiaHoldAblation.ForceHoldTickGeneration = true;

            var note = createHold(duration: 1000);

            Assert.That(note.Ticks, Is.Not.Null);
            Assert.That(note.Ticks.Count, Is.GreaterThan(0));
        }

        [Test]
        public void TestDisableTickGenerationSuppressesForcedTicks()
        {
            ManiaHoldAblation.ForceHoldTickGeneration = true;
            ManiaHoldAblation.DisableHoldTickGeneration = true;

            var note = createHold(duration: 1000);

            Assert.That(note.Ticks, Is.Empty);
        }

        private static HoldNote createHold(double duration)
        {
            var note = new HoldNote { StartTime = 0, Duration = duration };
            note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return note;
        }
    }
}
#endif
