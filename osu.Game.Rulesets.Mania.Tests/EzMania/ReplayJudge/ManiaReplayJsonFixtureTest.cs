// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaReplayJsonFixtureTest
    {
        [Test]
        public void TestEmbeddedTapFixtureRoundTripsSubMillisecondTimes()
        {
            var document = ManiaReplayJsonFixture.ReadResource("Resources/Testing/ReplayJson/Lazer-two-note-tap.json");
            var (_, _, hitObjects, frames, _) = ManiaReplayJsonFixture.ToParts(document);

            Assert.That(hitObjects[0].StartTime, Is.EqualTo(1000.25).Within(1e-9));
            Assert.That(hitObjects[1].StartTime, Is.EqualTo(2000.75).Within(1e-9));
            Assert.That(((ManiaReplayFrame)frames[0]).Time, Is.EqualTo(900.1).Within(1e-9));
            Assert.That(((ManiaReplayFrame)frames[0]).Actions, Is.EquivalentTo(new[] { ManiaAction.Key1 }));
            Assert.That(((ManiaReplayFrame)frames[1]).Actions, Is.Empty);
        }

        [Test]
        public void TestWriteThenReadPreservesHoldAndKeys()
        {
            var source = ManiaReplayJsonFixture.ReadResource("Resources/Testing/ReplayJson/Lazer-hold-break-repress-meh.json");
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "replay_json_roundtrip.json");
            ManiaReplayJsonFixture.Write(path, source);

            using var stream = File.OpenRead(path);
            var roundTrip = ManiaReplayJsonFixture.Read(stream);
            var (_, _, hitObjects, frames, _) = ManiaReplayJsonFixture.ToParts(roundTrip);

            Assert.That(hitObjects.Single(), Is.TypeOf<HoldNote>());
            var hold = (HoldNote)hitObjects.Single();
            Assert.That(hold.StartTime, Is.EqualTo(1500).Within(1e-9));
            Assert.That(hold.EndTime, Is.EqualTo(4000).Within(1e-9));
            Assert.That(frames, Has.Count.EqualTo(4));
            Assert.That(((ManiaReplayFrame)frames[2]).Time, Is.EqualTo(3900.25).Within(1e-9));
        }
    }
}
