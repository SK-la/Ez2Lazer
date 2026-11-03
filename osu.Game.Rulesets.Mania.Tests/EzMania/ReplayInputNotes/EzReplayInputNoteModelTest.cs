// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.EzMania.ReplayInputNotes;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayInputNotes
{
    [TestFixture]
    public class EzReplayInputNoteModelTest
    {
        [Test]
        public void TestSpansFollowPressAndRelease()
        {
            var model = load(createBeatmap(), new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(1300),
                    new ManiaReplayFrame(2000, ManiaAction.Key1),
                    new ManiaReplayFrame(2004),
                },
            });

            var notes = model.NotesFor(0);

            Assert.That(notes, Has.Count.EqualTo(2));
            Assert.That(notes[0].StartTime, Is.EqualTo(1000));
            Assert.That(notes[0].EndTime, Is.EqualTo(1300));
            Assert.That(notes[0].Kind, Is.EqualTo(EzReplayInputNoteKind.None));

            // No minimum length: a four millisecond press keeps its exact duration.
            Assert.That(notes[1].StartTime, Is.EqualTo(2000));
            Assert.That(notes[1].Duration, Is.EqualTo(4));
        }

        [Test]
        public void TestUnreleasedPressEndsAtLastFrame()
        {
            var model = load(createBeatmap(), new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(1500),
                },
            });

            var notes = model.NotesFor(0);

            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].EndTime, Is.EqualTo(1500));
        }

        [Test]
        public void TestHoldPressIsSplitIntoHeadAndTail()
        {
            var (beatmap, hold) = createBeatmapWithHold();

            var model = load(beatmap, new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(2000),
                },
            });

            model.ApplyResults(new[]
            {
                new HitEvent(0, null, HitResult.Great, hold.Head, null, null),
                new HitEvent(0, null, HitResult.Meh, hold.Tail, null, null),
            });

            var notes = model.NotesFor(0);

            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].Kind, Is.EqualTo(EzReplayInputNoteKind.Hold));
            Assert.That(notes[0].HeadResult, Is.EqualTo(HitResult.Great));
            Assert.That(notes[0].NonHeadResult, Is.EqualTo(HitResult.Meh));
            Assert.That(notes[0].NonHeadRevealTime, Is.EqualTo(2000));
            Assert.That(notes[0].BodyBroken, Is.False);
        }

        [Test]
        public void TestBrokenHoldMarksNonHead()
        {
            var (beatmap, hold) = createBeatmapWithHold();

            var model = load(beatmap, new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(1500),
                    new ManiaReplayFrame(1800, ManiaAction.Key1),
                    new ManiaReplayFrame(2000),
                },
            });

            model.ApplyResults(new[]
            {
                new HitEvent(0, null, HitResult.Great, hold.Head, null, null),
                new HitEvent(0, null, HitResult.ComboBreak, hold.Body, null, null),
            });

            var notes = model.NotesFor(0);

            Assert.That(notes, Has.Count.EqualTo(2));
            Assert.That(notes[0].Kind, Is.EqualTo(EzReplayInputNoteKind.Hold));
            Assert.That(notes[0].BodyBroken, Is.True);
            Assert.That(notes[0].NonHeadRevealTime, Is.EqualTo(1000));
        }

        [Test]
        public void TestJudgedModeDropsStrayPressesAndUsesTapStyle()
        {
            var beatmap = createBeatmap();

            var model = load(beatmap, new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(1200),
                    new ManiaReplayFrame(1500, ManiaAction.Key1),
                    new ManiaReplayFrame(1900),
                },
            });

            model.ApplyResults(new[]
            {
                new HitEvent(0, null, HitResult.Great, beatmap.HitObjects[0], null, null),
            });

            model.OnlyJudgedNotes.Value = true;

            var notes = model.NotesFor(0);

            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].StartTime, Is.EqualTo(1000));
            Assert.That(notes[0].IsTapStyle, Is.True);
            Assert.That(notes[0].EndTime, Is.EqualTo(1000));
        }

        [Test]
        public void TestJudgedModeKeepsHoldSpanAndDropsIgnoredHeads()
        {
            var (beatmap, hold) = createBeatmapWithHold();

            var model = load(beatmap, new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(2000),
                },
            });

            model.ApplyResults(new[]
            {
                new HitEvent(0, null, HitResult.IgnoreMiss, hold.Head, null, null),
                new HitEvent(0, null, HitResult.Great, hold.Tail, null, null),
            });

            model.OnlyJudgedNotes.Value = true;

            Assert.That(model.NotesFor(0), Is.Empty);

            model.ApplyResults(new[]
            {
                new HitEvent(0, null, HitResult.Great, hold.Head, null, null),
                new HitEvent(0, null, HitResult.Great, hold.Tail, null, null),
            });

            var notes = model.NotesFor(0);

            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].Kind, Is.EqualTo(EzReplayInputNoteKind.Hold));
            Assert.That(notes[0].IsTapStyle, Is.False);
            Assert.That(notes[0].StartTime, Is.EqualTo(1000));
            Assert.That(notes[0].EndTime, Is.EqualTo(2000));
        }

        private static EzReplayInputNoteModel load(IBeatmap beatmap, Replay replay)
        {
            var model = new EzReplayInputNoteModel();
            model.Load(replay, beatmap);
            return model;
        }

        private static Beatmap createBeatmap()
        {
            var beatmap = new TestBeatmap(new ManiaRuleset().RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            return beatmap;
        }

        private static (Beatmap beatmap, HoldNote hold) createBeatmapWithHold()
        {
            var hold = new HoldNote { StartTime = 1000, Duration = 1000, Column = 0 };

            var beatmap = new TestBeatmap(new ManiaRuleset().RulesetInfo)
            {
                HitObjects = new List<HitObject> { hold },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            Assert.That(hold.NestedHitObjects.OfType<HoldNoteBody>().Any(), Is.True);

            return (beatmap, hold);
        }
    }
}
