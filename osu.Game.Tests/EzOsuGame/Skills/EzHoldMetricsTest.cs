// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// The hold ratio is derived from the beatmap object counts, not stored as an analysis result.
    /// </summary>
    [TestFixture]
    public class EzHoldMetricsTest
    {
        [Test]
        public void It_is_the_ratio_of_end_time_objects_to_total_objects()
        {
            var beatmapInfo = new BeatmapInfo { TotalObjectCount = 200, EndTimeObjectCount = 50 };

            Assert.That(EzHoldMetrics.HoldRatio(beatmapInfo), Is.EqualTo(0.25).Within(1e-9));
        }

        [Test]
        public void It_is_unknown_when_the_counts_are_not_stamped()
        {
            Assert.That(EzHoldMetrics.HoldRatio(new BeatmapInfo()), Is.NaN);
            Assert.That(EzHoldMetrics.HoldRatio(new BeatmapInfo { TotalObjectCount = 0, EndTimeObjectCount = 0 }), Is.NaN);
        }

        [Test]
        public void It_falls_back_to_the_playable_when_the_counts_are_missing()
        {
            var beatmap = new Beatmap { Difficulty = { CircleSize = 4 } };
            beatmap.HitObjects.Add(new HoldObject(0, 0));
            beatmap.HitObjects.Add(new NoteObject(100, 0));
            beatmap.HitObjects.Add(new NoteObject(200, 1));
            beatmap.HitObjects.Add(new NoteObject(300, 2));

            var beatmapInfo = new BeatmapInfo();
            double fromPlayable = EzHoldMetrics.ComputeHoldRatio(beatmap);

            Assert.That(fromPlayable, Is.EqualTo(0.25).Within(1e-9));
            Assert.That(EzHoldMetrics.HoldRatioOr(beatmap, beatmapInfo), Is.EqualTo(fromPlayable).Within(1e-9));
        }

        [Test]
        public void Stored_counts_win_over_the_playable()
        {
            var beatmap = new Beatmap { Difficulty = { CircleSize = 4 } };
            beatmap.HitObjects.Add(new NoteObject(0, 0));

            var beatmapInfo = new BeatmapInfo { TotalObjectCount = 200, EndTimeObjectCount = 50 };

            Assert.That(EzHoldMetrics.HoldRatioOr(beatmap, beatmapInfo), Is.EqualTo(0.25).Within(1e-9));
        }

        private class NoteObject : HitObject, IHasColumn
        {
            public int Column { get; }

            public NoteObject(double startTime, int column)
            {
                StartTime = startTime;
                Column = column;
            }
        }

        private class HoldObject : NoteObject, IHasDuration
        {
            public double EndTime { get; set; }

            // Expressed through the object span so the held-duration check sees a positive value.
            public double Duration
            {
                get => EndTime - StartTime;
                set => EndTime = StartTime + value;
            }

            public HoldObject(double startTime, int column)
                : base(startTime, column)
            {
                EndTime = startTime + 100;
            }
        }
    }
}
