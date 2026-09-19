// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Mania.EzMania.Input
{
    internal sealed class RingBufferPressTimes : IReadOnlyList<double>
    {
        private readonly double[] buffer;
        private int head;

        public int Count { get; private set; }

        public RingBufferPressTimes(int capacity)
        {
            buffer = new double[capacity];
        }

        public double this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException();

                return buffer[(head + index) % buffer.Length];
            }
        }

        public void Add(double time)
        {
            int idx = (head + Count) % buffer.Length;
            buffer[idx] = time;

            if (Count < buffer.Length)
                Count++;
            else
                head = (head + 1) % buffer.Length;
        }

        public void Trim(double cutoff)
        {
            while (Count > 0 && buffer[head] < cutoff)
            {
                head = (head + 1) % buffer.Length;
                Count--;
            }
        }

        public IEnumerator<double> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return buffer[(head + i) % buffer.Length];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
