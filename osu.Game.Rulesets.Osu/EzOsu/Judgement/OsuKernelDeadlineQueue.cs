// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Osu.EzOsu.Judgement
{
    /// <summary>
    /// deadline 最小堆。每个 owner 至多保留一条有效记录；状态推进后旧记录按
    /// 「记录时刻 != 当前 NextDeadline」判为过期并在取堆顶时丢弃。
    /// </summary>
    internal sealed class OsuKernelDeadlineQueue
    {
        private readonly struct Entry
        {
            public readonly double Time;
            public readonly int Order;
            public readonly IOsuKernelDeadlineOwner Owner;

            public Entry(double time, int order, IOsuKernelDeadlineOwner owner)
            {
                Time = time;
                Order = order;
                Owner = owner;
            }
        }

        private readonly List<Entry> heap = new List<Entry>();

        public int Count => heap.Count;

        public double? Peek()
        {
            dropStale();
            return heap.Count > 0 ? heap[0].Time : null;
        }

        public void Push(IOsuKernelDeadlineOwner owner)
        {
            double? deadline = owner.NextDeadline;
            if (deadline == null)
                return;

            heap.Add(new Entry(deadline.Value, owner.DeadlineOrder, owner));
            siftUp(heap.Count - 1);
        }

        public bool TryPop(out IOsuKernelDeadlineOwner owner, out double time)
        {
            dropStale();

            if (heap.Count == 0)
            {
                owner = null!;
                time = 0;
                return false;
            }

            Entry top = heap[0];
            owner = top.Owner;
            time = top.Time;

            removeRoot();
            return true;
        }

        public void Clear() => heap.Clear();

        private void dropStale()
        {
            while (heap.Count > 0)
            {
                Entry top = heap[0];

                if (top.Owner.NextDeadline is double deadline && deadline == top.Time)
                    return;

                removeRoot();
            }
        }

        private void removeRoot()
        {
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);

            if (heap.Count > 0)
                siftDown(0);
        }

        private void siftUp(int index)
        {
            Entry entry = heap[index];

            while (index > 0)
            {
                int parent = (index - 1) / 2;

                if (compare(heap[parent], entry) <= 0)
                    break;

                heap[index] = heap[parent];
                index = parent;
            }

            heap[index] = entry;
        }

        private void siftDown(int index)
        {
            Entry entry = heap[index];

            while (true)
            {
                int left = index * 2 + 1;

                if (left >= heap.Count)
                    break;

                int right = left + 1;
                int child = right < heap.Count && compare(heap[right], heap[left]) < 0 ? right : left;

                if (compare(entry, heap[child]) <= 0)
                    break;

                heap[index] = heap[child];
                index = child;
            }

            heap[index] = entry;
        }

        private static int compare(in Entry a, in Entry b)
        {
            int timeComparison = a.Time.CompareTo(b.Time);
            return timeComparison != 0 ? timeComparison : a.Order.CompareTo(b.Order);
        }
    }
}
