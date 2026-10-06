// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod
{
    /// <summary>
    /// 「按列 + 按开始时间升序」的重叠查询视图，用来替代对整份 <see cref="ManiaHitObject"/> 列表的全表扫描。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 判定与 <see cref="ManiaModYuModHelper.FindOverlapInList(List{ManiaHitObject}, int, double, double)"/> 完全一致，
    /// 包括它的怪癖：只关心「同列上有对象的开始时间等于查询时刻」或「同列上有正长条覆盖该时刻」，查询传入的结束时间不参与判定。
    /// 重构时不要把结束时间顺手纳入判定——那会改变转换结果。
    /// </para>
    /// <para>
    /// 查询 O(log n + k)：每列保存按开始时间升序的 (start, end)，外加「到该位置为止、正长条结束时间的最大值」。
    /// 命中条件 = 二分出最后一个 start &lt;= t 的位置，再取该位置的前缀最大值判断是否 &gt;= t。
    /// 前缀最大值能代表「正长条覆盖 t」：结束时间不大于开始时间的对象（note 与负长条）只会把前缀最大值压到不超过自身 start，
    /// 覆盖不了更晚的时刻，因此既不会误报也不会漏报；note 的 start == t 另由等值分支命中。
    /// </para>
    /// <para>
    /// 维护成本：按开始时间插入。调用点都按开始时间递增地添加对象，常态是 O(1) 追加；乱序插入是 O(该列长度)
    /// （一次数组搬移 + 重算该位置之后的前缀），但无论哪种，重叠查询本身都不再扫全表。
    /// </para>
    /// </remarks>
    public sealed class ManiaObjectColumnIndex
    {
        private readonly List<Entry>[] entries;
        private readonly List<double>[] prefixMaxEnd;

        public ManiaObjectColumnIndex(int columnCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(columnCount);

            entries = new List<Entry>[columnCount];
            prefixMaxEnd = new List<double>[columnCount];

            for (int i = 0; i < columnCount; i++)
            {
                entries[i] = new List<Entry>();
                prefixMaxEnd[i] = new List<double>();
            }
        }

        /// <summary>
        /// 清空所有列以便复用。轮次之间重建索引时用它替掉重新分配。
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i].Clear();
                prefixMaxEnd[i].Clear();
            }
        }

        public void Add(ManiaHitObject hitObject)
        {
            ArgumentNullException.ThrowIfNull(hitObject);

            Add(hitObject.Column, hitObject.StartTime, hitObject.GetEndTime());
        }

        public void Add(int column, double startTime, double endTime)
        {
            // 越界列与 NaN 开始时间在原判定里永远不成立，直接不入索引即可（入不了也就查不到）。
            if (!isTrackable(column, startTime))
                return;

            List<Entry> columnEntries = entries[column];
            int position = upperBound(columnEntries, startTime);

            columnEntries.Insert(position, new Entry(startTime, endTime));
            repairPrefix(column, position);
        }

        /// <summary>
        /// 同列上是否存在与该时刻重叠的对象。语义同
        /// <see cref="ManiaModYuModHelper.FindOverlapInList(List{ManiaHitObject}, int, double, double)"/>。
        /// </summary>
        public bool Overlaps(int column, double startTime)
        {
            if ((uint)column >= (uint)entries.Length)
                return false;

            List<Entry> columnEntries = entries[column];

            if (columnEntries.Count == 0)
                return false;

            int index = upperBound(columnEntries, startTime);

            // 开始时间相等的对象就在上界前一位（重复时是最后一个）。
            if (index > 0 && columnEntries[index - 1].Start == startTime)
                return true;

            int candidate = index - 1;

            if (candidate < 0)
                return false;

            double covered = prefixMaxEnd[column][candidate];

            return !double.IsNaN(covered) && covered >= startTime;
        }

        private static bool isTrackable(int column, double startTime) => column >= 0 && !double.IsNaN(startTime);

        private static int upperBound(List<Entry> columnEntries, double startTime)
        {
            int low = 0;
            int high = columnEntries.Count;

            while (low < high)
            {
                int mid = (low + high) >> 1;

                if (columnEntries[mid].Start <= startTime)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }

        /// <summary>
        /// 从 <paramref name="fromIndex"/> 起重算该列的前缀最大值：升序追加时它等于列尾，摊到 O(1)。
        /// </summary>
        private void repairPrefix(int column, int fromIndex)
        {
            List<Entry> columnEntries = entries[column];
            List<double> prefix = prefixMaxEnd[column];

            if (prefix.Count > columnEntries.Count)
                prefix.RemoveRange(columnEntries.Count, prefix.Count - columnEntries.Count);

            double running = fromIndex > 0 ? prefix[fromIndex - 1] : double.NegativeInfinity;

            for (int i = fromIndex; i < columnEntries.Count; i++)
            {
                Entry entry = columnEntries[i];

                // 只有结束时间晚于开始时间的对象能覆盖更晚的时刻；NaN 结束时间不参与（原判定里也永远不成立）。
                if (entry.End > entry.Start)
                    running = maxIgnoringNaN(running, entry.End);

                if (i < prefix.Count)
                    prefix[i] = running;
                else
                    prefix.Add(running);
            }
        }

        private static double maxIgnoringNaN(double a, double b)
        {
            if (double.IsNaN(a)) return b;
            if (double.IsNaN(b)) return a;

            return Math.Max(a, b);
        }

        private readonly struct Entry
        {
            public readonly double Start;
            public readonly double End;

            public Entry(double start, double end)
            {
                Start = start;
                End = end;
            }
        }
    }
}
