// Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.KrrConverters
{
    public static class N2NCConverter
    {
        // Ported to operate on ManiaHitObject directly: map columns proportionally
        // and preserve hold durations. Metadata updated similarly to krrTools.
        public static void Transform(ManiaBeatmap beatmap, object? optionsObj)
        {
            if (beatmap == null) return;

            int targetKeys = beatmap.TotalColumns;
            int maxKeys = targetKeys;
            int minKeys = 1;
            int? seed = null;

            if (optionsObj != null)
            {
                var t = optionsObj.GetType();
                var pTarget = t.GetProperty("TargetKeys");
                var pMax = t.GetProperty("MaxKeys");
                var pMin = t.GetProperty("MinKeys");
                var pSeed = t.GetProperty("Seed");
                try { if (pTarget != null) targetKeys = Convert.ToInt32(pTarget.GetValue(optionsObj)); } catch { }
                try { if (pMax != null) maxKeys = Convert.ToInt32(pMax.GetValue(optionsObj)); } catch { }
                try { if (pMin != null) minKeys = Convert.ToInt32(pMin.GetValue(optionsObj)); } catch { }
                try { if (pSeed != null) seed = pSeed.GetValue(optionsObj) == null ? (int?)null : Convert.ToInt32(pSeed.GetValue(optionsObj)); } catch { }
            }

            // Infer original keycount from Difficulty.CircleSize if available, otherwise from max column seen.
            int originalKeys = 0;
            if (beatmap.BeatmapInfo?.Difficulty?.CircleSize is float cs && cs > 0)
                originalKeys = Math.Max(1, (int)Math.Round(cs));
            else if (beatmap.HitObjects.Any())
                originalKeys = beatmap.HitObjects.Max(h => h.Column) + 1;
            else
                originalKeys = targetKeys;

            // If the beatmap has already been converted to the target keys by converter, nothing to do.
            if (originalKeys == targetKeys)
                return;

            int seedValue = seed ?? ComputeSeedFromBeatmap(beatmap);
            var rng = new Random(seedValue);

            // First pass: compute mapped columns with tie-breaking via oscillator
            var osc = new OscillatorGenerator(seedValue, frequency: 1.0 / 16.0);
            var mapped = new List<(ManiaHitObject obj, int newCol)>();
            foreach (var obj in beatmap.HitObjects.OfType<ManiaHitObject>())
            {
                int origCol = Math.Clamp(obj.Column, 0, Math.Max(1, originalKeys) - 1);
                double proportion = originalKeys > 1 ? origCol / (double)(originalKeys - 1) : 0.0;
                double exact = proportion * (targetKeys - 1);
                int floor = (int)Math.Floor(exact);
                double frac = exact - floor;
                int newCol = floor;
                if (frac > 0.5) newCol = floor + 1;
                else if (Math.Abs(frac - 0.5) <= 1e-9)
                {
                    if (osc.Next() > 0.5) newCol = floor + 1;
                }
                newCol = Math.Clamp(newCol, 0, Math.Max(0, targetKeys - 1));
                mapped.Add((obj, newCol));
            }

            // Build per-target lists
            var buckets = new Dictionary<int, List<ManiaHitObject>>();
            for (int i = 0; i < targetKeys; i++) buckets[i] = new List<ManiaHitObject>();
            foreach (var kv in mapped) buckets[kv.newCol].Add(kv.obj);

            // If target has empty buckets (targetKeys > originalKeys), insert clones from nearest source columns
            if (targetKeys > originalKeys)
            {
                for (int t = 0; t < targetKeys; t++)
                {
                    if (buckets[t].Count == 0)
                    {
                        int approxSource = (int)Math.Round(t * (originalKeys > 1 ? (originalKeys - 1) / (double)(targetKeys - 1) : 0.0));
                        approxSource = Math.Clamp(approxSource, 0, Math.Max(0, originalKeys - 1));
                        var candidates = beatmap.HitObjects.Where(h => Math.Clamp(h.Column, 0, originalKeys - 1) == approxSource).Cast<ManiaHitObject>().ToList();
                        if (candidates.Count > 0)
                        {
                            var pick = candidates[rng.Next(candidates.Count)];
                            if (pick is HoldNote hn)
                                buckets[t].Add(new HoldNote { Column = t, StartTime = hn.StartTime, EndTime = hn.EndTime, Samples = hn.Samples?.ToList() });
                            else
                                buckets[t].Add(new Note { Column = t, StartTime = pick.StartTime, Samples = pick.Samples });
                        }
                    }
                }
            }

            // Density control: enforce max/min per-column if provided
            int globalMax = Math.Max(1, maxKeys);
            int globalMin = Math.Max(0, minKeys);
            var finalObjects = new List<ManiaHitObject>();
            foreach (var kv in buckets)
            {
                var list = kv.Value.OrderBy(h => h.StartTime).ToList();
                // mild reduction when compressing many source columns into fewer target columns
                int expectedPerBucket = (int)Math.Ceiling(beatmap.HitObjects.Count / (double)targetKeys);
                if (originalKeys > targetKeys && list.Count > expectedPerBucket * 2)
                {
                    list = list.OrderBy(x => rng.Next()).Take(expectedPerBucket).OrderBy(x => x.StartTime).ToList();
                }
                if (list.Count > globalMax)
                {
                    list = list.OrderBy(x => rng.Next()).Take(globalMax).OrderBy(x => x.StartTime).ToList();
                }
                else if (list.Count < globalMin && list.Count > 0)
                {
                    int idx = 0;
                    while (list.Count < globalMin)
                    {
                        var src = list[idx % list.Count];
                        if (src is HoldNote hh)
                            list.Add(new HoldNote { Column = kv.Key, StartTime = hh.StartTime, EndTime = hh.EndTime, Samples = hh.Samples?.ToList() });
                        else
                            list.Add(new Note { Column = kv.Key, StartTime = src.StartTime, Samples = src.Samples });
                        idx++;
                    }
                    list = list.OrderBy(x => x.StartTime).ToList();
                }

                foreach (var h in list)
                {
                    h.Column = kv.Key;
                    finalObjects.Add(h);
                }
            }

            beatmap.HitObjects.Clear();
            beatmap.HitObjects.AddRange(finalObjects.OrderBy(h => h.StartTime).ThenBy(h => h.Column));

            // metadata handling intentionally omitted (framework will manage metadata/setting UI)
        }

        private static int ComputeSeedFromBeatmap(ManiaBeatmap beatmap)
        {
            try
            {
                var s = beatmap?.BeatmapInfo?.Hash ?? string.Empty;
                if (string.IsNullOrEmpty(s))
                {
                    int val = (beatmap?.HitObjects.Count ?? 0) ^ (beatmap?.TotalColumns ?? 0);
                    return Math.Abs(val) + 1;
                }

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
