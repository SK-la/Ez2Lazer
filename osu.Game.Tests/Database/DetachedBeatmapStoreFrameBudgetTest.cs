// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class DetachedBeatmapStoreFrameBudgetTest
    {
        [Test]
        public void MaxOpsPerFrameIsBounded()
        {
            Assert.That(DetachedBeatmapStoreFrameBudget.MAX_OPS_PER_FRAME, Is.EqualTo(24));
            Assert.That(DetachedBeatmapStoreFrameBudget.MAX_OPS_PER_FRAME, Is.LessThanOrEqualTo(64));
        }

        [Test]
        public void DrainProcessesAtMostMaxOpsAndLeavesRemainder()
        {
            var queue = new Queue<int>();
            for (int i = 0; i < 100; i++)
                queue.Enqueue(i);

            var seen = new List<int>();
            int processed = DetachedBeatmapStoreFrameBudget.Drain(queue, DetachedBeatmapStoreFrameBudget.MAX_OPS_PER_FRAME, seen.Add);

            Assert.That(processed, Is.EqualTo(DetachedBeatmapStoreFrameBudget.MAX_OPS_PER_FRAME));
            Assert.That(seen, Has.Count.EqualTo(DetachedBeatmapStoreFrameBudget.MAX_OPS_PER_FRAME));
            Assert.That(queue.Count, Is.EqualTo(100 - DetachedBeatmapStoreFrameBudget.MAX_OPS_PER_FRAME));

            processed = DetachedBeatmapStoreFrameBudget.Drain(queue, DetachedBeatmapStoreFrameBudget.MAX_OPS_PER_FRAME, seen.Add);
            Assert.That(processed, Is.EqualTo(DetachedBeatmapStoreFrameBudget.MAX_OPS_PER_FRAME));
            Assert.That(queue.Count, Is.EqualTo(100 - 2 * DetachedBeatmapStoreFrameBudget.MAX_OPS_PER_FRAME));
        }

        [Test]
        public void DrainDoesNotExceedQueue()
        {
            var queue = new Queue<int>(new[] { 1, 2, 3 });
            int n = DetachedBeatmapStoreFrameBudget.Drain(queue, DetachedBeatmapStoreFrameBudget.MAX_OPS_PER_FRAME, _ => { });
            Assert.That(n, Is.EqualTo(3));
            Assert.That(queue.Count, Is.EqualTo(0));
        }

        [Test]
        public void DrainRejectsNegativeBudget()
        {
            var queue = new Queue<int>();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DetachedBeatmapStoreFrameBudget.Drain(queue, -1, _ => { }));
        }

        [Test]
        public void Time_budget_stops_after_first_item_when_subsequent_would_exceed_budget()
        {
            var queue = new Queue<int>(new[] { 1, 2, 3 });

            int processed = DetachedBeatmapStoreFrameBudget.DrainForTimeBudget(queue, 8, 0.001, i =>
            {
                if (i >= 2)
                    System.Threading.Thread.Sleep(2);
            });

            Assert.That(processed, Is.EqualTo(1));
            Assert.That(queue.Count, Is.EqualTo(2));
            Assert.That(queue.Peek(), Is.EqualTo(2));
        }
    }
}
