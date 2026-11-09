// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace osu.Game.Database
{
    /// <summary>
    /// 选歌 carousel：限制 <see cref="RealmDetachedBeatmapStore"/> 每帧从 pending 队列取出的操作数。
    /// </summary>
    public static class DetachedBeatmapStoreFrameBudget
    {
        /// <summary>
        /// Legacy count-only cap; prefer <see cref="DrainForTimeBudget{T}"/> with <see cref="DetachedBeatmapStoreSyncPolicy"/>.
        /// </summary>
        public const int MAX_OPS_PER_FRAME = 24;

        /// <summary>
        /// 至多处理 <paramref name="maxOps"/> 项；队列剩余留到后续帧。
        /// </summary>
        public static int Drain<T>(Queue<T> queue, int maxOps, Action<T> process)
        {
            ArgumentNullException.ThrowIfNull(queue);
            ArgumentNullException.ThrowIfNull(process);
            ArgumentOutOfRangeException.ThrowIfNegative(maxOps);

            int processed = 0;

            while (processed < maxOps && queue.TryDequeue(out var item))
            {
                process(item);
                processed++;
            }

            return processed;
        }

        /// <summary>
        /// Drains until <paramref name="maxOps"/> is reached or elapsed time meets <paramref name="timeBudgetMs"/>.
        /// The first item always runs; subsequent items start only while under the budget.
        /// </summary>
        public static int DrainForTimeBudget<T>(Queue<T> queue, int maxOps, double timeBudgetMs, Action<T> process)
        {
            ArgumentNullException.ThrowIfNull(queue);
            ArgumentNullException.ThrowIfNull(process);
            ArgumentOutOfRangeException.ThrowIfNegative(maxOps);

            if (timeBudgetMs <= 0)
                return Drain(queue, maxOps, process);

            var stopwatch = Stopwatch.StartNew();
            int processed = 0;

            while (processed < maxOps && queue.TryDequeue(out var item))
            {
                if (processed > 0 && stopwatch.Elapsed.TotalMilliseconds >= timeBudgetMs)
                {
                    // Re-queue the item we dequeued but did not apply so ordering is preserved.
                    requeueFront(queue, item);
                    break;
                }

                process(item);
                processed++;
            }

            return processed;
        }

        private static void requeueFront<T>(Queue<T> queue, T item)
        {
            int count = queue.Count;
            var buffer = new T[count + 1];
            buffer[0] = item;

            int index = 1;

            while (queue.TryDequeue(out var existing))
            {
                buffer[index++] = existing;
            }

            foreach (var entry in buffer)
                queue.Enqueue(entry);
        }
    }
}
