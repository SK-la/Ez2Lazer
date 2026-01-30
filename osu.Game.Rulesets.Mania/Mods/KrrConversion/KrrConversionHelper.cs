// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.KrrConversion
{
    public class KrrConversionHelper
    {
        public static readonly double[] BEAT_SPEED_VALUES = { 0.125, 0.25, 0.5, 0.75, 1, 2, 3, 4, 999 };

        public static double ComputeConvertTime(int beatSpeedIndex, double bpm)
        {
            double speed = BEAT_SPEED_VALUES[Math.Clamp(beatSpeedIndex, 0, BEAT_SPEED_VALUES.Length - 1)];
            return Math.Max(1, (speed * 60000 / bpm * 4) - 10);
        }

        public static int ComputeSeedFromBeatmap(ManiaBeatmap beatmap)
        {
            try
            {
                int val = (beatmap.HitObjects.Count) ^ (beatmap.TotalColumns);
                return Math.Abs(val) + 1;
            }
            catch
            {
                return 114514;
            }
        }

        public static int InferOriginalKeys(ManiaBeatmap beatmap, int fallback)
        {
            double cs = beatmap.BeatmapInfo.Difficulty.CircleSize;
            if (cs > 0)
                return Math.Max(1, (int)Math.Round(cs));

            if (beatmap.HitObjects.Count > 0)
                return beatmap.HitObjects.Max(h => h.Column) + 1;

            return fallback;
        }

        public static ManiaHitObject CloneWithColumn(ManiaHitObject src, int col)
        {
            if (src is HoldNote hold)
            {
                return new HoldNote
                {
                    StartTime = hold.StartTime,
                    EndTime = hold.EndTime,
                    Column = col,
                    Samples = hold.Samples.ToList()
                };
            }

            return new Note
            {
                StartTime = src.StartTime,
                Column = col,
                Samples = src.Samples.ToList()
            };
        }

        // ---------------- 通用 LN/DP/N2N 方法 ----------------

        // Beta 分布随机数生成
        public static int GenerateRandom(double dBar, double uBar, double mBar, int pBar, Random r, Oscillator? osc = null)
        {
            if (pBar <= 0) return (int)mBar;

            if (pBar >= 100) pBar = 100;

            double p = pBar / 100.0;
            double d = mBar - (mBar - dBar) * p;
            double u = mBar + (uBar - mBar) * p;

            d = Math.Max(d, dBar);
            u = Math.Min(u, uBar);

            if (d >= u) return (int)mBar;

            double betaRandom = (r.NextDouble() + r.NextDouble() + r.NextDouble() + r.NextDouble() + r.NextDouble()) / 5.0;

            if (osc != null)
            {
                double bias = (osc.Next() - 0.5) * 0.04;
                betaRandom = Math.Clamp(betaRandom + bias, 0.0, 1.0);
            }

            double range = u - d;
            double mRelative = (mBar - d) / range;
            double result = betaRandom <= 0.5
                ? d + mRelative * (betaRandom / 0.5) * range
                : d + (mRelative + (1 - mRelative) * ((betaRandom - 0.5) / 0.5)) * range;

            return (int)Math.Round(result);
        }

        // 百分比筛选
        public static List<(int index, double start, int length)> MarkByPercentagePerGroup(
            List<(int index, double start, int length)> list, double percentage, Random r, Oscillator osc)
        {
            if (percentage >= 100) return list;
            if (percentage <= 0) return new List<(int, double, int)>();

            var grouped = list.GroupBy(x => Math.Round(x.start, 3)).ToList();
            var outList = new List<(int index, double start, int length)>();

            foreach (var g in grouped)
            {
                var items = g.ToList();
                int keep = (int)Math.Round(items.Count * (percentage / 100.0));
                keep = Math.Max(0, Math.Min(keep, items.Count));

                if (keep == items.Count) outList.AddRange(items);
                else if (keep > 0)
                {
                    var scored = items.Select(it => new { it.index, it.start, it.length, score = r.NextDouble() * 0.85 + osc.Next() * 0.15 })
                                      .OrderByDescending(x => x.score).Take(keep)
                                      .Select(x => (x.index, x.start, x.length));
                    outList.AddRange(scored);
                }
            }

            return outList;
        }

        // 对齐方法
        public static void ApplyAlignmentToCandidates(List<(int index, double start, int length)> list, ManiaBeatmap beatmap, int alignmentIndex)
        {
            var alignMap = new Dictionary<int, double>
            {
                { 1, 1.0 / 8 },
                { 2, 1.0 / 7 },
                { 3, 1.0 / 6 },
                { 4, 1.0 / 5 },
                { 5, 1.0 / 4 },
                { 6, 1.0 / 3 },
                { 7, 1.0 / 2 },
                { 8, 1.0 }
            };

            if (!alignMap.TryGetValue(alignmentIndex, out double alignValue)) return;

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                var ho = beatmap.HitObjects[item.index];
                double beatLen = beatmap.ControlPointInfo.TimingPointAt(ho.StartTime).BeatLength;
                double denom = beatLen * alignValue;
                if (denom <= 0.5) continue;

                int aligned = (int)(Math.Round(item.length / denom) * denom);
                if (aligned < 30) aligned = item.length;
                list[i] = (item.index, item.start, aligned);
            }
        }
    }
}
