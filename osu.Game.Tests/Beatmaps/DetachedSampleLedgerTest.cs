// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Tests.Beatmaps
{
    public class DetachedSampleLedgerTest
    {
        [Test]
        public void TestUntouchedBeatmapDetachesNothing()
        {
            var beatmap = new Beatmap<HitObject>
            {
                HitObjects =
                {
                    new TestNote(1000, new HitSampleInfo("key-a")),
                    new TestNote(2000, new HitSampleInfo("key-b")),
                },
            };

            DetachedSampleLedger.Capture(beatmap).Settle(beatmap);

            Assert.That(beatmap.DetachedSamples, Is.Empty);
        }

        [Test]
        public void TestRemovedObjectKeepsItsSampleAtItsOriginalTimeAndPosition()
        {
            var sample = new HitSampleInfo("key-a");
            var removed = new TestNote(1000, sample);

            var beatmap = new Beatmap<HitObject>
            {
                HitObjects = { removed, new TestNote(2000, new HitSampleInfo("key-b")) },
                SamplePositionResolver = _ => 0.25f,
            };

            var ledger = DetachedSampleLedger.Capture(beatmap);

            beatmap.HitObjects.Remove(removed);

            ledger.Settle(beatmap);

            Assert.That(beatmap.DetachedSamples, Has.Count.EqualTo(1));
            Assert.That(beatmap.DetachedSamples[0].Sample, Is.SameAs(sample));
            Assert.That(beatmap.DetachedSamples[0].Time, Is.EqualTo(1000));
            Assert.That(beatmap.DetachedSamples[0].Position, Is.EqualTo(0.25f));
        }

        [Test]
        public void TestSampleTakenOverByReplacementIsNotDetached()
        {
            var head = new HitSampleInfo("key-head");
            var tail = new HitSampleInfo("key-tail");

            var hold = new TestHold(1000, 2000, head, tail);

            var beatmap = new Beatmap<HitObject> { HitObjects = { hold } };
            applyDefaults(beatmap);

            var ledger = DetachedSampleLedger.Capture(beatmap);

            // A mod turns the hold into a tap which takes the head sample over.
            beatmap.HitObjects.Clear();
            beatmap.HitObjects.Add(new TestNote(1000, head));

            ledger.Settle(beatmap);

            Assert.That(beatmap.DetachedSamples, Has.Count.EqualTo(1));
            Assert.That(beatmap.DetachedSamples[0].Sample, Is.SameAs(tail));
            Assert.That(beatmap.DetachedSamples[0].Time, Is.EqualTo(2000));
        }

        [Test]
        public void TestSampleFollowsObjectMovedInTime()
        {
            var note = new TestNote(1000, new HitSampleInfo("key-a"));

            var beatmap = new Beatmap<HitObject> { HitObjects = { note } };

            var ledger = DetachedSampleLedger.Capture(beatmap);

            note.StartTime = 3000;

            ledger.Settle(beatmap);

            Assert.That(beatmap.DetachedSamples, Is.Empty);
        }

        [Test]
        public void TestRemovedSampleIsKeptEvenWhenAnotherNoteCarriesTheSameAudio()
        {
            var removed = new TestNote(1000, new HitSampleInfo(HitSampleInfo.HIT_NORMAL));

            var beatmap = new Beatmap<HitObject>
            {
                HitObjects = { removed, new TestNote(2000, new HitSampleInfo(HitSampleInfo.HIT_NORMAL)) },
            };

            var ledger = DetachedSampleLedger.Capture(beatmap);

            beatmap.HitObjects.Remove(removed);

            ledger.Settle(beatmap);

            Assert.That(beatmap.DetachedSamples, Has.Count.EqualTo(1));
            Assert.That(beatmap.DetachedSamples[0].Time, Is.EqualTo(1000));
        }

        private static void applyDefaults(IBeatmap beatmap)
        {
            foreach (var hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty, CancellationToken.None);
        }

        private class TestNote : HitObject
        {
            public TestNote(double startTime, HitSampleInfo sample)
            {
                StartTime = startTime;
                Samples = new[] { sample };
            }
        }

        /// <summary>
        /// A hold whose head and tail each carry their own sample, as converted keysound charts do.
        /// </summary>
        private class TestHold : HitObject
        {
            private readonly double endTime;
            private readonly HitSampleInfo head;
            private readonly HitSampleInfo tail;

            public TestHold(double startTime, double endTime, HitSampleInfo head, HitSampleInfo tail)
            {
                StartTime = startTime;
                this.endTime = endTime;
                this.head = head;
                this.tail = tail;
            }

            protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
            {
                base.CreateNestedHitObjects(cancellationToken);

                AddNested(new TestNote(StartTime, head));
                AddNested(new TestNote(endTime, tail));
            }
        }
    }
}
