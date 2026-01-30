using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.KrrConversion
{
    public static class KrrLNConverter
    {
        private const double min_gap = 20.0;
        private const double min_len = 30.0;

        public static void Transform(ManiaBeatmap beatmap, KrrLNOptions options)
        {
            int seedValue = options.Seed ?? KrrConversionHelper.ComputeSeedFromBeatmap(beatmap);
            var rg = new Random(seedValue);
            var osc = new Oscillator(seedValue, 1.0 / 16.0);

            int cs = Math.Max(1, beatmap.TotalColumns);

            // 按列收集音符索引
            var columns = new List<List<int>>();
            for (int i = 0; i < cs; i++) columns.Add(new List<int>());

            for (int i = 0; i < beatmap.HitObjects.Count; i++)
            {
                var ho = beatmap.HitObjects[i];
                if (!options.ProcessOriginalIsChecked && ho is HoldNote) continue;

                int col = Math.Clamp(ho.Column, 0, cs - 1);
                columns[col].Add(i);
            }

            var borderGen = new BeatNumberGenerator(64, 1.0 / 4);
            var shortGen = new BeatNumberGenerator(256, 1.0 / 16);

            int borderKey = options.LengthThreshold;
            double borderValue = borderGen.GetValue(borderKey);

            double longLevel = options.LongPercentage;
            double shortLevel = shortGen.GetValue(Math.Max(0, options.Level));

            var longCandidates = new List<(int index, double start, int length)>();
            var shortCandidates = new List<(int index, double start, int length)>();

            // 按列计算可用时间
            for (int c = 0; c < cs; c++)
            {
                var list = columns[c];
                list.Sort((a, b) => beatmap.HitObjects[a].StartTime.CompareTo(beatmap.HitObjects[b].StartTime));

                for (int j = 0; j < list.Count; j++)
                {
                    int idx = list[j];
                    var ho = beatmap.HitObjects[idx];
                    double start = ho.StartTime;

                    double available;
                    if (j + 1 < list.Count)
                        available = beatmap.HitObjects[list[j + 1]].StartTime - start;
                    else
                        available = beatmap.HitObjects.Last().StartTime + 2000 - start;

                    double beatLen = beatmap.ControlPointInfo.TimingPointAt(ho.StartTime).BeatLength;
                    double borderTime = borderValue * beatLen;

                    bool candidateLong = available > borderTime;

                    if (candidateLong)
                    {
                        double mean = available * longLevel / 100.0;
                        double di = borderTime;
                        int newLength;
                        if (mean < di)
                            newLength = KrrConversionHelper.GenerateRandom(0, di, shortLevel * beatLen, options.LongRandom, rg, osc);
                        else
                            newLength = KrrConversionHelper.GenerateRandom(di, available, mean, options.LongRandom, rg, osc);

                        if (newLength > available - 34) newLength = (int)Math.Max(0, available - 34);
                        if (newLength > 0) longCandidates.Add((idx, start, newLength));
                    }
                    else
                    {
                        int newLength = KrrConversionHelper.GenerateRandom(0, borderTime, shortLevel * beatLen, options.ShortRandom, rg, osc);
                        if (newLength > available - 34) newLength = (int)Math.Max(0, available - 34);
                        if (newLength > 0) shortCandidates.Add((idx, start, newLength));
                    }
                }
            }

            // 百分比筛选
            longCandidates = KrrConversionHelper.MarkByPercentagePerGroup(longCandidates, options.LongPercentage, rg, osc);
            shortCandidates = KrrConversionHelper.MarkByPercentagePerGroup(shortCandidates, options.ShortPercentage, rg, osc);

            // 行限制
            longCandidates = enforceLimit(longCandidates, options.LongLimit, rg);
            shortCandidates = enforceLimit(shortCandidates, options.ShortLimit, rg);

            // 合并，长优先
            var reserved = new HashSet<int>(longCandidates.Select(x => x.index));
            var merged = new List<(int index, double start, int length)>();
            merged.AddRange(longCandidates);
            merged.AddRange(shortCandidates.Where(x => !reserved.Contains(x.index)));

            // 对齐
            if (options.Alignment > 0)
                KrrConversionHelper.ApplyAlignmentToCandidates(merged, beatmap, options.Alignment);

            // 应用到谱面对象
            foreach (var c in merged)
            {
                var original = beatmap.HitObjects[c.index];
                double newEnd = original.StartTime + c.length;
                if (newEnd - original.StartTime < min_len) continue;

                if (original is HoldNote hn)
                    hn.EndTime = newEnd;
                else
                {
                    var newHold = new HoldNote
                    {
                        StartTime = original.StartTime,
                        Column = original.Column,
                        Samples = original.Samples?.ToList(),
                        EndTime = newEnd
                    };
                    beatmap.HitObjects[c.index] = newHold;
                }
            }
        }

        private static List<(int index, double start, int length)> enforceLimit(List<(int index, double start, int length)> list, int limit, Random rg)
        {
            if (limit <= 0) return new List<(int, double, int)>();

            var grouped = list.GroupBy(x => Math.Round(x.start, 3)).ToList();
            var outList = new List<(int, double, int)>();

            foreach (var g in grouped)
            {
                var items = g.ToList();

                if (items.Count <= limit) outList.AddRange(items);
                else
                {
                    items = items.OrderBy(x => rg.Next()).Take(limit).ToList();
                    outList.AddRange(items);
                }
            }

            return outList;
        }
    }

    internal class BeatNumberGenerator
    {
        private readonly double[] values;
        private readonly int middleIndex;
        private readonly double coefficient;
        private readonly double lastValue;

        public BeatNumberGenerator(int middleIndex, double coefficient, double lastValue = 999.0)
        {
            this.middleIndex = middleIndex;
            this.coefficient = coefficient;
            this.lastValue = lastValue;
            values = new double[middleIndex + 2];
            values[0] = 0.0;
            for (int i = 1; i <= middleIndex; i++) values[i] = i * coefficient;
            values[middleIndex + 1] = lastValue;
        }

        public double GetValue(int index)
        {
            if (index <= 0) return 0;
            if (index >= values.Length - 1) return lastValue;

            return values[index];
        }
    }
}
