// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Tests.Beatmaps
{
    public class DetachedSampleLedgerTest
    {
        [Test]
        public void TestUntouchedBeatmapDetachesNothing()
        {
            var beatmap = new TestBeatmap
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

            var beatmap = new TestBeatmap
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
        public void TestTailSampleOfAConvertedHoldIsKept()
        {
            var head = new HitSampleInfo("key-head");
            var tail = new HitSampleInfo("key-tail");

            var hold = new TestHold(1000, 2000, head, tail);

            var beatmap = new TestBeatmap { HitObjects = { hold } };

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
        public void TestRewrittenHoldTakingItsSamplesOverDetachesNothing()
        {
            var head = new HitSampleInfo("key-head");
            var tail = new HitSampleInfo("key-tail");

            var hold = new TestHold(1000, 2000, head, tail);

            var beatmap = new TestBeatmap { HitObjects = { hold } };

            var ledger = DetachedSampleLedger.Capture(beatmap);

            // A mod rebuilds the hold at new times, carrying both node samples over.
            beatmap.HitObjects[0] = new TestHold(1500, 2500, new HitSampleInfo("key-head"), new HitSampleInfo("key-tail"));

            ledger.Settle(beatmap);

            Assert.That(beatmap.DetachedSamples, Is.Empty);
        }

        [Test]
        public void TestSampleFollowsObjectMovedInTime()
        {
            var note = new TestNote(1000, new HitSampleInfo("key-a"));

            var beatmap = new TestBeatmap { HitObjects = { note } };

            var ledger = DetachedSampleLedger.Capture(beatmap);

            note.StartTime = 3000;

            ledger.Settle(beatmap);

            Assert.That(beatmap.DetachedSamples, Is.Empty);
        }

        [Test]
        public void TestRemovedSampleIsKeptEvenWhenAnotherNoteCarriesTheSameAudio()
        {
            var removed = new TestNote(1000, new HitSampleInfo(HitSampleInfo.HIT_NORMAL));

            var beatmap = new TestBeatmap
            {
                HitObjects = { removed, new TestNote(2000, new HitSampleInfo(HitSampleInfo.HIT_NORMAL)) },
            };

            var ledger = DetachedSampleLedger.Capture(beatmap);

            beatmap.HitObjects.Remove(removed);

            ledger.Settle(beatmap);

            Assert.That(beatmap.DetachedSamples, Has.Count.EqualTo(1));
            Assert.That(beatmap.DetachedSamples[0].Time, Is.EqualTo(1000));
        }

        [Test]
        public void TestSampleOfObjectRemovedByAfterConversionModIsDetached()
        {
            var removed = new HitCircle
            {
                StartTime = 1000,
                Samples = new[] { new HitSampleInfo("key-removed") },
            };

            var beatmap = new Beatmap
            {
                HitObjects =
                {
                    removed,
                    new HitCircle { StartTime = 2000, Samples = new[] { new HitSampleInfo("key-kept") } },
                },
            };

            // The mods that rewrite hit objects are the post-conversion ones, so this also pins down where the
            // snapshot has to sit in the pipeline.
            var playable = new TestWorkingBeatmap(beatmap).GetPlayableBeatmap(new OsuRuleset().RulesetInfo, new Mod[] { new RemoveFirstObjectMod() });

            var host = (IDetachedSampleHost)playable;

            Assert.That(host.DetachedSamples, Has.Count.EqualTo(1));
            Assert.That(host.DetachedSamples[0].Time, Is.EqualTo(1000));
            Assert.That(host.DetachedSamples[0].Sample, Is.EqualTo(new HitSampleInfo("key-removed")));
        }

        private class RemoveFirstObjectMod : Mod, IApplicableAfterBeatmapConversion
        {
            public override string Name => "Remove first object";

            public override string Acronym => "RFO";

            public override LocalisableString Description => "Removes the first hit object.";

            public void ApplyToBeatmap(IBeatmap beatmap) => ((Beatmap<OsuHitObject>)beatmap).HitObjects.RemoveAt(0);
        }

        private class TestBeatmap : Beatmap<HitObject>
        {
            public TestBeatmap()
            {
                // Mirrors the mania ruleset: a hold note's segment samples sit in NodeSamples, at the head and tail.
                SampleTimelineResolver = hitObject =>
                {
                    if (hitObject is TestHold hold)
                        return hold.GetNodeTimeline();

                    return DetachedSampleLedger.GetDefaultTimeline(hitObject);
                };
            }
        }

        private class TestNote : HitObject
        {
            public TestNote(double startTime, HitSampleInfo sample)
            {
                StartTime = startTime;
                Samples = new[] { sample };
            }
        }

        private class TestHold : HitObject
        {
            private readonly double endTime;

            public IList<IList<HitSampleInfo>> NodeSamples { get; }

            public TestHold(double startTime, double endTime, HitSampleInfo head, HitSampleInfo tail)
            {
                StartTime = startTime;
                this.endTime = endTime;
                NodeSamples = new List<IList<HitSampleInfo>> { new List<HitSampleInfo> { head }, new List<HitSampleInfo> { tail } };
            }

            public IEnumerable<(double Time, HitSampleInfo Sample)> GetNodeTimeline()
            {
                foreach (var sample in NodeSamples[0])
                    yield return (StartTime, sample);

                foreach (var sample in NodeSamples[1])
                    yield return (endTime, sample);
            }
        }
    }
}
