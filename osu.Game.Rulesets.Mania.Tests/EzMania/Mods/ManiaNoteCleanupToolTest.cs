// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Mods
{
    [TestFixture]
    public class ManiaNoteCleanupToolTest
    {
        [Test]
        public void TestSameTimeTapsAlwaysDropTheLaterOne()
        {
            ManiaBeatmap beatmap = createBeatmap(
                new Note { StartTime = 1000, Column = 0 },
                new Note { StartTime = 1000, Column = 0 });

            NoteCleanupReport report = ManiaNoteCleanupTool.CleanupBeatmap(beatmap);

            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(1));
            Assert.That(beatmap.HitObjects[0], Is.TypeOf<Note>());
            Assert.That(report.Dropped, Is.EqualTo(1));
        }

        [Test]
        public void TestContinueSplitsHoldAroundTap()
        {
            ManiaBeatmap beatmap = createBeatmap(
                new HoldNote { StartTime = 1000, Duration = 9000, Column = 0 },
                new Note { StartTime = 5000, Column = 0 },
                new Note { StartTime = 7000, Column = 0 });

            ManiaNoteCleanupTool.CleanupBeatmap(beatmap, new NoteCleanupOptions { CleanDenseNotes = false });

            Assert.That(serialise(beatmap), Is.EqualTo("H0@1000+3984 H0@5000+1984 H0@7000+3000"));
        }

        [Test]
        public void TestDropTapLeavesTheHold()
        {
            ManiaBeatmap beatmap = createBeatmap(
                new HoldNote { StartTime = 1000, Duration = 9000, Column = 0 },
                new Note { StartTime = 5000, Column = 0 });

            ManiaNoteCleanupTool.CleanupBeatmap(beatmap, new NoteCleanupOptions
            {
                LnBodyTapMode = LnBodyTapMode.DropTap,
                CleanDenseNotes = false,
            });

            Assert.That(serialise(beatmap), Is.EqualTo("H0@1000+9000"));
        }

        [Test]
        public void TestTruncateKeepsTheTap()
        {
            ManiaBeatmap beatmap = createBeatmap(
                new HoldNote { StartTime = 1000, Duration = 9000, Column = 0 },
                new Note { StartTime = 5000, Column = 0 });

            ManiaNoteCleanupTool.CleanupBeatmap(beatmap, new NoteCleanupOptions
            {
                LnBodyTapMode = LnBodyTapMode.Truncate,
                CleanDenseNotes = false,
            });

            Assert.That(serialise(beatmap), Is.EqualTo("H0@1000+3984 N0@5000"));
        }

        [Test]
        public void TestDenseRunDropsEvenNotesAndKeepsTheClosingNote()
        {
            ManiaBeatmap beatmap = createBeatmap(
                new Note { StartTime = 0, Column = 0 },
                new Note { StartTime = 10, Column = 0 },
                new Note { StartTime = 20, Column = 0 },
                new Note { StartTime = 30, Column = 0 },
                new Note { StartTime = 10000, Column = 0 });

            NoteCleanupReport report = ManiaNoteCleanupTool.CleanupBeatmap(beatmap);

            Assert.That(serialise(beatmap), Is.EqualTo("N0@0 N0@20 N0@10000"));
            Assert.That(report.EmptySpans, Has.Count.EqualTo(1));
            Assert.That(report.EmptySpans[0].Start, Is.EqualTo(20));
            Assert.That(report.EmptySpans[0].End, Is.EqualTo(10000));
        }

        [Test]
        public void TestDenseRunKeepsTheLastNoteWhenTheColumnEndsThere()
        {
            ManiaBeatmap beatmap = createBeatmap(
                new Note { StartTime = 0, Column = 0 },
                new Note { StartTime = 10, Column = 0 },
                new Note { StartTime = 20, Column = 0 },
                new Note { StartTime = 30, Column = 0 });

            ManiaNoteCleanupTool.CleanupBeatmap(beatmap);

            Assert.That(serialise(beatmap), Is.EqualTo("N0@0 N0@20 N0@30"));
        }

        [Test]
        public void TestGapEqualToThresholdIsNotDense()
        {
            ManiaBeatmap beatmap = createBeatmap(
                new Note { StartTime = 0, Column = 0 },
                new Note { StartTime = 16, Column = 0 },
                new Note { StartTime = 32, Column = 0 });

            ManiaNoteCleanupTool.CleanupBeatmap(beatmap);

            Assert.That(serialise(beatmap), Is.EqualTo("N0@0 N0@16 N0@32"));
        }

        [Test]
        public void TestHoldTailOpensTheSameGapWithoutTakingTheNextNote()
        {
            ManiaBeatmap beatmap = createBeatmap(
                new HoldNote { StartTime = 0, Duration = 100, Column = 0 },
                new Note { StartTime = 110, Column = 0 });

            ManiaNoteCleanupTool.CleanupBeatmap(beatmap);

            Assert.That(serialise(beatmap), Is.EqualTo("H0@0+94 N0@110"));
        }

        [Test]
        public void TestTapThenHoldDelaysTheHoldHead()
        {
            ManiaBeatmap beatmap = createBeatmap(
                new Note { StartTime = 0, Column = 0 },
                new HoldNote { StartTime = 10, Duration = 500, Column = 0 });

            ManiaNoteCleanupTool.CleanupBeatmap(beatmap);

            Assert.That(serialise(beatmap), Is.EqualTo("N0@0 H0@16+494"));
        }

        [Test]
        public void TestDenseTapsStopBeforeAFollowingHold()
        {
            ManiaBeatmap beatmap = createBeatmap(
                new Note { StartTime = 0, Column = 0 },
                new Note { StartTime = 10, Column = 0 },
                new Note { StartTime = 20, Column = 0 },
                new HoldNote { StartTime = 30, Duration = 200, Column = 0 });

            ManiaNoteCleanupTool.CleanupBeatmap(beatmap);

            Assert.That(serialise(beatmap), Is.EqualTo("N0@0 N0@20 H0@36+194"));
        }

        [Test]
        public void TestLnDensityCanBeDisabledOnItsOwn()
        {
            ManiaBeatmap beatmap = createBeatmap(
                new HoldNote { StartTime = 0, Duration = 100, Column = 0 },
                new Note { StartTime = 110, Column = 0 });

            ManiaNoteCleanupTool.CleanupBeatmap(beatmap, new NoteCleanupOptions { CleanLnDensity = false });

            Assert.That(serialise(beatmap), Is.EqualTo("H0@0+100 N0@110"));
        }

        [Test]
        public void TestBeatDivisorUsesTimingAtTheNote()
        {
            ManiaBeatmap beatmap = createBeatmap(
                new Note { StartTime = 0, Column = 0 },
                new Note { StartTime = 100, Column = 0 },
                new Note { StartTime = 200, Column = 0 },
                new Note { StartTime = 1000, Column = 0 });

            ManiaNoteCleanupTool.CleanupBeatmap(beatmap, new NoteCleanupOptions { BeatDivisor = 4 });

            Assert.That(serialise(beatmap), Is.EqualTo("N0@0 N0@200 N0@1000"));
        }

        private static ManiaBeatmap createBeatmap(params ManiaHitObject[] objects)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(4))
            {
                Difficulty = { CircleSize = 4 },
            };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            beatmap.HitObjects.AddRange(objects);
            return beatmap;
        }

        private static string serialise(ManiaBeatmap beatmap) =>
            string.Join(" ", beatmap.HitObjects.Select(hitObject =>
            {
                if (hitObject is HoldNote hold)
                    return $"H{hold.Column}@{hold.StartTime}+{hold.Duration}";

                return $"N{hitObject.Column}@{hitObject.StartTime}";
            }));
    }
}
