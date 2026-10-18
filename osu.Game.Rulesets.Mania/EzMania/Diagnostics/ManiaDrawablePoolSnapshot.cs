// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using osu.Framework.Statistics;

namespace osu.Game.Rulesets.Mania.EzMania.Diagnostics
{
    /// <summary>
    /// 从 <see cref="GlobalStatistics"/> 读取 <c>DrawablePool&lt;T&gt;</c> 的占用快照。
    /// <para>
    /// 框架把池统计注册为 <c>DrawablePool&lt;&gt;</c> 组下的一条记录，展示值为
    /// <c>{CountInUse}/{CurrentPoolSize} ({CountExcessConstructed} excess)</c>。
    /// 池实例本身由 <c>Playfield</c> 私有持有，这里按展示值汇总，避免为了诊断扩大生产 API。
    /// </para>
    /// </summary>
    public static class ManiaDrawablePoolSnapshot
    {
        private const string pool_group = "DrawablePool<>";
        private const string legacy_pool_group = "DrawablePool";

        /// <summary>按 drawable 类型名汇总的池占用。</summary>
        public readonly struct Entry
        {
            public string Type { get; }
            public int InUse { get; }
            public int PoolSize { get; }
            public int Excess { get; }

            public Entry(string type, int inUse, int poolSize, int excess)
            {
                Type = type;
                InUse = inUse;
                PoolSize = poolSize;
                Excess = excess;
            }

            public override string ToString() => $"{Type}={InUse}/{PoolSize}+{Excess}";
        }

        /// <summary>读取当前所有 <c>DrawablePool&lt;T&gt;</c> 统计并按类型名排序返回。</summary>
        public static List<Entry> Capture()
        {
            var accumulators = new Dictionary<string, (int inUse, int poolSize, int excess)>(StringComparer.Ordinal);

            foreach (var statistic in GlobalStatistics.GetStatistics())
            {
                if (statistic.Group != pool_group && statistic.Group != legacy_pool_group)
                    continue;

                if (!tryParse(statistic.DisplayValue, out int inUse, out int poolSize, out int excess))
                    continue;

                string type = stripInstanceSuffix(statistic.Name);

                if (accumulators.TryGetValue(type, out var current))
                    accumulators[type] = (current.inUse + inUse, current.poolSize + poolSize, current.excess + excess);
                else
                    accumulators[type] = (inUse, poolSize, excess);
            }

            var entries = new List<Entry>(accumulators.Count);

            foreach (var (type, value) in accumulators)
                entries.Add(new Entry(type, value.inUse, value.poolSize, value.excess));

            entries.Sort(static (a, b) => string.CompareOrdinal(a.Type, b.Type));
            return entries;
        }

        /// <summary>压成单行日志：<c>DrawableNote=140/160+0 …</c>。</summary>
        public static string FormatSummary()
        {
            var entries = Capture();

            if (entries.Count == 0)
                return "pools=none";

            var builder = new StringBuilder("pools=");

            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                    builder.Append(' ');

                builder.Append(entries[i]);
            }

            return builder.ToString();
        }

        /// <summary>所有类型合计的 excess 数。</summary>
        public static int TotalExcess()
        {
            int total = 0;

            foreach (var entry in Capture())
                total += entry.Excess;

            return total;
        }

        /// <summary>所有类型合计的在用数与池容量。</summary>
        public static (int inUse, int poolSize) TotalUsage()
        {
            int inUse = 0;
            int poolSize = 0;

            foreach (var entry in Capture())
            {
                inUse += entry.InUse;
                poolSize += entry.PoolSize;
            }

            return (inUse, poolSize);
        }

        private static string stripInstanceSuffix(string name)
        {
            // DrawablePool<T> 的统计名是 "{TypeName}`{instanceId}"。
            int separator = name.LastIndexOf('`');
            return separator > 0 ? name.Substring(0, separator) : name;
        }

        private static bool tryParse(string displayValue, out int inUse, out int poolSize, out int excess)
        {
            inUse = 0;
            poolSize = 0;
            excess = 0;

            if (string.IsNullOrEmpty(displayValue))
                return false;

            string[] parts = displayValue.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                return false;

            string[] usage = parts[0].Split('/');

            if (usage.Length != 2)
                return false;

            string excessText = parts[1].TrimStart('(');

            return int.TryParse(usage[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out inUse)
                   && int.TryParse(usage[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out poolSize)
                   && int.TryParse(excessText, NumberStyles.Integer, CultureInfo.InvariantCulture, out excess);
        }
    }
}
