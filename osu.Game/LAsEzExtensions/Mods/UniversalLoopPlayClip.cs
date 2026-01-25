// Universal loop play clip mod that applies to arbitrary rulesets using best-effort cloning.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.LAsEzExtensions.Mods
{
    public class UniversalLoopPlayClip : ModLoopPlayClip,
                                            IApplicableAfterBeatmapConversion
    {
        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var (cutTimeStart, cutTimeEnd, _) = ResolveSliceTimesForBeatmap(beatmap);

            ApplyLoopToBeatmapStatic(beatmap, LoopCount.Value, cutTimeStart, cutTimeEnd, BreakQuarter.Value, Seed.Value);
        }

        public static void ApplyLoopToBeatmapStatic(IBeatmap beatmap, int loopCount, double cutTimeStart, double cutTimeEnd, int breakQuarter, int? seed = null)
        {
            if (beatmap == null) return;

            try
            {
                var breaksProp = beatmap.GetType().GetProperty("Breaks");
                if (breaksProp != null && breaksProp.CanWrite)
                {
                    var breaks = breaksProp.GetValue(beatmap) as System.Collections.IList;
                    breaks?.Clear();
                }
                else
                {
                    beatmap.Breaks.Clear();
                }
            }
            catch
            {
            }

            double breakTime;
            try
            {
                var timing = beatmap.ControlPointInfo.TimingPointAt(cutTimeStart);
                double quarterMs = timing.BeatLength / 4.0;
                breakTime = quarterMs * Math.Max(1, breakQuarter);
            }
            catch
            {
                breakTime = 250 * Math.Max(1, breakQuarter);
            }

            var selectedPart = beatmap.HitObjects.Where(h => h.StartTime > cutTimeStart && h.GetEndTime() < cutTimeEnd).ToList();

            var newPart = new List<HitObject>();

            var rng = seed.HasValue ? new Random((int)seed.Value) : new Random();

            double length = cutTimeEnd - cutTimeStart;

            for (int timeIndex = 0; timeIndex < loopCount; timeIndex++)
            {
                double offset = timeIndex * (breakTime + length);

                foreach (var note in selectedPart)
                {
                    double baseOffset = offset - cutTimeStart;

                    var type = note.GetType();

                    try
                    {
                        var inst = (HitObject?)Activator.CreateInstance(type);

                        if (inst != null)
                        {
                            // StartTime
                            var startProp = type.GetProperty("StartTime");
                            if (startProp != null && startProp.CanWrite)
                                startProp.SetValue(inst, note.StartTime + baseOffset);
                            else
                                inst.StartTime = note.StartTime + baseOffset;

                            // Samples
                            var samplesProp = type.GetProperty("Samples");
                            if (samplesProp != null && samplesProp.CanWrite)
                            {
                                try
                                {
                                    var s = note.Samples?.ToList();
                                    samplesProp.SetValue(inst, s);
                                }
                                catch
                                {
                                    inst.Samples = note.Samples?.ToList();
                                }
                            }
                            else
                            {
                                inst.Samples = note.Samples?.ToList();
                            }

                            newPart.Add(inst);
                        }
                    }
                    catch
                    {
                        // skip
                    }
                }
            }

            foreach (var h in newPart)
            {
                try
                {
                    h.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                }
                catch
                {
                }
            }

            var propHitObjects = beatmap.GetType().GetProperty("HitObjects");
            if (propHitObjects != null && propHitObjects.CanWrite)
            {
                // Ensure we assign a list whose element type matches the property's generic argument
                var propType = propHitObjects.PropertyType;
                if (propType.IsGenericType)
                {
                    var elementType = propType.GetGenericArguments()[0];

                    if (elementType == typeof(HitObject))
                    {
                        propHitObjects.SetValue(beatmap, newPart);
                        return;
                    }

                    try
                    {
                        var listType = typeof(List<>).MakeGenericType(elementType);
                        var listInstance = (System.Collections.IList)Activator.CreateInstance(listType)!;

                        foreach (var h in newPart)
                        {
                            if (h == null) continue;

                            if (elementType.IsAssignableFrom(h.GetType()))
                            {
                                listInstance.Add(h);
                                continue;
                            }

                            // Attempt best-effort conversion: create instance of elementType and copy common fields.
                            try
                            {
                                var target = Activator.CreateInstance(elementType) as HitObject;
                                if (target != null)
                                {
                                    var startProp = elementType.GetProperty("StartTime");
                                    if (startProp != null && startProp.CanWrite)
                                        startProp.SetValue(target, h.StartTime);
                                    else
                                        target.StartTime = h.StartTime;

                                    var samplesProp = elementType.GetProperty("Samples");
                                    if (samplesProp != null && samplesProp.CanWrite)
                                    {
                                        try { samplesProp.SetValue(target, h.Samples?.ToList()); }
                                        catch { target.Samples = h.Samples?.ToList(); }
                                    }
                                    else
                                    {
                                        target.Samples = h.Samples?.ToList();
                                    }

                                    listInstance.Add(target);
                                    continue;
                                }
                            }
                            catch
                            {
                                // ignore conversion failure for this element
                            }
                        }

                        propHitObjects.SetValue(beatmap, listInstance);
                        return;
                    }
                    catch
                    {
                        // fallthrough to generic assignment attempt below
                    }
                }

                // Last resort: try to set directly (may fail if types mismatch)
                try
                {
                    propHitObjects.SetValue(beatmap, newPart);
                    return;
                }
                catch
                {
                    // continue to other fallbacks
                }
            }

            try
            {
                dynamic dyn = beatmap;
                dyn.HitObjects = newPart;
            }
            catch
            {
                try
                {
                    var current = beatmap.HitObjects as IList<HitObject>;
                    if (current != null)
                    {
                        current.Clear();
                        foreach (var h in newPart) current.Add(h);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
