// Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.KrrConverters
{
    public static class KRRLNConverter
    {
        // Full port using matrix-based pipeline adapted from krrTools
        public static void Transform(ManiaBeatmap beatmap, object optionsObj)
        {
            if (beatmap == null) return;

            var options = new KrrLNOptions();
            if (optionsObj is KrrLNOptions ko) options = ko;
            else if (optionsObj != null)
            {
                var t = optionsObj.GetType();
                var pLevel = t.GetProperty("Level");
                var pSeed = t.GetProperty("Seed");
                try { if (pLevel != null) options.Level = Convert.ToInt32(pLevel.GetValue(optionsObj)); } catch { }
                try { if (pSeed != null) options.Seed = pSeed.GetValue(optionsObj) == null ? (int?)null : Convert.ToInt32(pSeed.GetValue(optionsObj)); } catch { }
            }

            int seedValue = options.Seed ?? ComputeSeedFromBeatmap(beatmap);
            Random RG = new Random(seedValue);
            var osc = new OscillatorGenerator(seedValue, frequency: 1.0 / 16.0, phase: 0.0, step: 1.0);

            int cs = Math.Max(1, beatmap.TotalColumns);

            // precompute sorted global start times for efficient next-global lookup
            var allStartTimes = beatmap.HitObjects.Select(h => h.StartTime).OrderBy(t => t).ToArray();

            double GetNextGlobalStart(double s)
            {
                int idx = Array.BinarySearch(allStartTimes, s);
                if (idx >= 0) // exact match -> next one
                    idx++;
                else
                    idx = ~idx; // first greater
                if (idx >= 0 && idx < allStartTimes.Length) return allStartTimes[idx];
                return s + 2000.0;
            }

            // Prepare per-column index lists (only consider ManiaHitObject-derived objects)
            var columns = new List<List<int>>();
            for (int i = 0; i < cs; i++) columns.Add(new List<int>());

            for (int i = 0; i < beatmap.HitObjects.Count; i++)
            {
                var ho = beatmap.HitObjects[i] as ManiaHitObject;
                if (ho == null) continue;
                // Skip existing LN if user chose not to process originals
                if (!options.ProcessOriginalIsChecked && ho is HoldNote) continue;
                int col = Math.Clamp(ho.Column, 0, cs - 1);
                columns[col].Add(i);
            }

            int borderKey = options.LengthThreshold;

            // Simplified replacements for BeatNumberGenerator
            const double borderCoefficient = 1.0 / 4.0;
            const int borderMiddleIndex = 64;
            double GetBorderValue(int idx)
            {
                if (idx <= 0) return 0.0;
                if (idx >= borderMiddleIndex + 1) return 999.0;
                return idx * borderCoefficient;
            }

            const double shortCoefficient = 1.0 / 16.0;
            const int shortMiddleIndex = 256;
            double GetShortValue(int idx)
            {
                if (idx <= 0) return 0.0;
                if (idx >= shortMiddleIndex + 1) return 999.0;
                return idx * shortCoefficient;
            }

            double LongLevel = Math.Clamp(options.Level * 10.0, 0, 100);
            double ShortLevel = GetShortValue(Math.Max(0, options.Level));

            // Collect candidate LN modifications (index -> newLength)
            var longCandidates = new List<(int index, double start, int length)>();
            var shortCandidates = new List<(int index, double start, int length)>();

            for (int c = 0; c < cs; c++)
            {
                var list = columns[c];
                list.Sort((a, b) => beatmap.HitObjects[a].StartTime.CompareTo(beatmap.HitObjects[b].StartTime));
                for (int j = 0; j < list.Count; j++)
                {
                    int idx = list[j];
                    var ho = beatmap.HitObjects[idx];
                    double start = ho.StartTime;
                    // compute available time conservatively: consider next in same column, next global start, and any overlapping hold end
                    double nextSameColStart = (j + 1 < list.Count) ? beatmap.HitObjects[list[j + 1]].StartTime : double.MaxValue;
                    double nextGlobalStart = GetNextGlobalStart(start);
                    double nextHoldEnd = double.MaxValue;
                    // nearest end time of any following HoldNote in any column (conservative)
                    double nextAnyHoldEnd = double.MaxValue;
                    var laterHolds = beatmap.HitObjects.Skip(j + 1).OfType<HoldNote>().Select(h => h.EndTime);
                    if (laterHolds.Any()) nextAnyHoldEnd = laterHolds.Min();
                    // if next object in column is a hold, consider its end
                    if (j + 1 < list.Count)
                    {
                        var nextHo = beatmap.HitObjects[list[j + 1]] as HoldNote;
                        if (nextHo != null) nextHoldEnd = nextHo.EndTime;
                    }
                    int available = (int)Math.Max(0, Math.Min(nextSameColStart, Math.Min(nextGlobalStart, Math.Min(nextHoldEnd, nextAnyHoldEnd))) - start);

                    double beatLen = beatmap.ControlPointInfo.TimingPointAt(ho.StartTime).BeatLength;
                    double borderValue = GetBorderValue(borderKey);
                    double borderTime = borderValue * beatLen;

                    bool candidateLong = available > borderTime;

                    if (candidateLong)
                    {
                        double mean = available * LongLevel / 100.0;
                        double di = borderTime;
                        int newLength;
                        if (mean < di)
                            newLength = GenerateRandom(0, di, (int)(ShortLevel * beatLen), options.LongRandom, RG, osc);
                        else
                            newLength = GenerateRandom(di, available, mean, options.LongRandom, RG, osc);
                        if (newLength > available - 34) newLength = Math.Max(0, available - 34);
                        if (newLength > 0) longCandidates.Add((idx, start, newLength));
                    }
                    else
                    {
                        int newLength = GenerateRandom(0, (int)(borderTime), (int)(ShortLevel * beatLen), options.ShortRandom, RG, osc);
                        if (newLength > available - 34) newLength = Math.Max(0, available - 34);
                        if (newLength > 0) shortCandidates.Add((idx, start, newLength));
                    }
                }
            }

            // Apply percentage filters (deterministic scoring using RNG + oscillator)
            longCandidates = MarkByPercentagePerGroup(longCandidates, options.LongPercentage, RG, osc);
            shortCandidates = MarkByPercentagePerGroup(shortCandidates, options.ShortPercentage, RG, osc);

            // Enforce per-starttime limits
            Func<List<(int index, double start, int length)>, int, List<(int index, double start, int length)>> enforceLimit = (list, limit) =>
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
                        // deterministic random selection
                        items = items.OrderBy(x => RG.Next()).Take(limit).ToList();
                        outList.AddRange(items);
                    }
                }
                return outList;
            };

            longCandidates = enforceLimit(longCandidates, Math.Max(0, options.LongLimit));
            shortCandidates = enforceLimit(shortCandidates, Math.Max(0, options.ShortLimit));

            // Merge long first so longs take precedence if conflict
            var reserved = new HashSet<int>(longCandidates.Select(x => x.index));
            var merged = new List<(int index, double start, int length)>();
            merged.AddRange(longCandidates);
            merged.AddRange(shortCandidates.Where(x => !reserved.Contains(x.index)));

            // Apply alignment if requested
            if (options.Alignment.HasValue)
                ApplyAlignmentToCandidates(merged, beatmap, options.Alignment.Value);

            // Apply modifications to beatmap
            const double minGap = 20.0; // ms gap to avoid overlap
            const double minLen = 30.0;
            foreach (var c in merged)
            {
                int idx = c.index;
                if (idx < 0 || idx >= beatmap.HitObjects.Count) continue;
                var original = beatmap.HitObjects[idx] as ManiaHitObject;
                if (original == null) continue;

                // determine next event start in same column or global to avoid overlap
                double nextSameCol = double.MaxValue;
                for (int k = idx + 1; k < beatmap.HitObjects.Count; k++) if (beatmap.HitObjects[k].Column == original.Column) { nextSameCol = beatmap.HitObjects[k].StartTime; break; }
                double nextGlobal = (beatmap.HitObjects.FirstOrDefault(h => h.StartTime > original.StartTime)?.StartTime) ?? double.MaxValue;
                double limit = Math.Min(nextSameCol, nextGlobal) - minGap;

                double desiredEnd = original.StartTime + c.length;
                double newEnd = Math.Min(desiredEnd, limit);
                if (newEnd - original.StartTime < minLen) continue; // too short, skip

                if (original is HoldNote hn)
                {
                    hn.EndTime = newEnd;
                }
                else
                {
                    var newHold = new HoldNote
                    {
                        StartTime = original.StartTime,
                        Column = original.Column,
                        Samples = original.Samples?.ToList()
                    };
                    newHold.EndTime = newEnd;
                    beatmap.HitObjects[idx] = newHold;
                }
            }

            // metadata handling intentionally omitted (framework will manage metadata/setting UI)
        }

        private static int GenerateRandom(double D, double U, double M, int P, Random r, OscillatorGenerator osc)
        {
            if (P <= 0) return (int)M;
            if (P >= 100) P = 100;
            double p = P / 100.0;
            double d = M - ((M - D) * p);
            double u = M + ((U - M) * p);
            d = Math.Max(d, D);
            u = Math.Min(u, U);
            if (d >= u) return (int)M;

            // use a more peaked beta-like distribution by averaging five uniforms
            double betaRandom = (r.NextDouble() + r.NextDouble() + r.NextDouble() + r.NextDouble() + r.NextDouble()) / 5.0;
            // small periodic bias from oscillator to reproduce subtle periodic patterns
            if (osc != null)
            {
                double bias = (osc.Next() - 0.5) * 0.04; // ±0.02 bias
                betaRandom = Math.Clamp(betaRandom + bias, 0.0, 1.0);
            }

            double range = u - d;
            double mRelative = (M - d) / range;
            double result;
            if (betaRandom <= 0.5)
                result = d + (mRelative * (betaRandom / 0.5) * range);
            else
                result = d + ((mRelative + ((1 - mRelative) * ((betaRandom - 0.5) / 0.5))) * range);

            var rounded = (int)Math.Round(result);
            rounded = Math.Max((int)D, Math.Min((int)U, rounded));
            return rounded;
        }

        private static List<(int index, double start, int length)> MarkByPercentagePerGroup(List<(int index, double start, int length)> list, double percentage, Random r, OscillatorGenerator osc)
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
                    // Score each candidate deterministically using RNG and oscillator to bias selection
                    var scored = items.Select(it => new { it.index, it.start, it.length, score = (r.NextDouble() * 0.85) + (osc?.Next() * 0.15) }).OrderByDescending(x => x.score).Take(keep).Select(x => (x.index, x.start, x.length));
                    outList.AddRange(scored);
                }
            }
            return outList;
        }

        private static void ApplyAlignmentToCandidates(List<(int index, double start, int length)> list, ManiaBeatmap beatmap, int alignmentIndex)
        {
            // alignmentIndex: 1..8 roughly mapping to 1/8..1/1 fractions
            var alignMap = new Dictionary<int, double> { {1,1.0/8},{2,1.0/7},{3,1.0/6},{4,1.0/5},{5,1.0/4},{6,1.0/3},{7,1.0/2},{8,1.0} };
            if (!alignMap.TryGetValue(alignmentIndex, out double alignValue)) return;
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                var ho = beatmap.HitObjects[item.index];
                double beatLen = beatmap.ControlPointInfo.TimingPointAt(ho.StartTime).BeatLength;
                double denom = beatLen * alignValue;
                if (denom <= 0.5) continue;
                int aligned = (int)(Math.Round(item.length / denom) * denom);
                if (aligned < 30) aligned = item.length; // keep if too small after rounding
                list[i] = (item.index, item.start, aligned);
            }
        }

        private static int ComputeSeedFromBeatmap(ManiaBeatmap beatmap)
        {
            try
            {
                var s = beatmap?.BeatmapInfo?.Hash ?? string.Empty;
                if (string.IsNullOrEmpty(s))
                {
                    // fallback to simple deterministic derivation
                    int val = (beatmap?.HitObjects.Count ?? 0) ^ (beatmap?.TotalColumns ?? 0);
                    return Math.Abs(val) + 1;
                }

                // DJB2
                unchecked
                {
                    int hash = 5381;
                    foreach (var ch in s)
                        hash = ((hash << 5) + hash) + ch;
                    return Math.Abs(hash) + 1;
                }
            }
            catch
            {
                return 1337;
            }
        }

    }
}
