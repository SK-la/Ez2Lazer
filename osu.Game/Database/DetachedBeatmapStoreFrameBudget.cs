// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Database
{
    /// <summary>
    /// 选歌 carousel：限制 <see cref="RealmDetachedBeatmapStore"/> 每帧从 pending 队列取出的操作数。
    /// </summary>
    public static class DetachedBeatmapStoreFrameBudget
    {
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
    }
}
