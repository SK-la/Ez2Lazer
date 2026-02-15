// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.LAsEzExtensions.Mods
{
    public class UniversalLoopPlayClip : ModLoopPlayClip, IApplicableAfterBeatmapConversion
    {
        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var (cutTimeStart, cutTimeEnd, _) = ResolveSliceTimesForBeatmap(beatmap);

            ApplyLoopToBeatmapStatic(beatmap, LoopCount.Value, cutTimeStart, cutTimeEnd, BreakQuarter.Value, Seed.Value);
        }

        public static void ApplyLoopToBeatmapStatic(IBeatmap? beatmap, int loopCount, double cutTimeStart, double cutTimeEnd, int breakQuarter, int? seed = null)
        {
            if (beatmap == null) return;

            try
            {
                var breaksProp = beatmap.GetType().GetProperty("Breaks");

                if (breaksProp != null && breaksProp.CanWrite)
                {
                    var breaks = breaksProp.GetValue(beatmap) as IList;
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

            double length = cutTimeEnd - cutTimeStart;

            // 防护：避免在 selectedPart 较大时通过 loopCount 复制产生过多 HitObject 导致内存暴涨。
            const int max_total_hitobjects = 200_000;

            if (selectedPart.Count > 0)
            {
                long total = (long)selectedPart.Count * loopCount;

                if (total > max_total_hitobjects)
                {
                    int adjustedLoopCount = Math.Max(1, max_total_hitobjects / selectedPart.Count);
                    loopCount = adjustedLoopCount;
                }
            }

            for (int timeIndex = 0; timeIndex < loopCount; timeIndex++)
            {
                double offset = timeIndex * (breakTime + length);

                foreach (var note in selectedPart)
                {
                    double baseOffset = offset - cutTimeStart;

                    var type = note.GetType();

                    try
                    {
                        HitObject? inst;

                        // 先尝试公共构造器，然后尝试调用非公共的无参构造器（若存在）。
                        try
                        {
                            inst = (HitObject?)Activator.CreateInstance(type);
                        }
                        catch
                        {
                            try
                            {
                                inst = (HitObject?)Activator.CreateInstance(type, nonPublic: true);
                            }
                            catch
                            {
                                inst = null;
                            }
                        }

                        if (inst != null)
                        {
                            // 尽力复制字段以保留对象内部状态
                            try
                            {
                                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                                foreach (var f in fields)
                                {
                                    try { f.SetValue(inst, f.GetValue(note)); }
                                    catch { }
                                }
                            }
                            catch { }

                            // 确保时间与样本被正确设置为偏移后的值
                            try
                            {
                                var startProp = type.GetProperty("StartTime");
                                if (startProp != null && startProp.CanWrite)
                                    startProp.SetValue(inst, note.StartTime + baseOffset);
                                else
                                    inst.StartTime = note.StartTime + baseOffset;
                            }
                            catch
                            {
                                inst.StartTime = note.StartTime + baseOffset;
                            }

                            try { inst.Samples = note.Samples?.ToList(); }
                            catch { }

                            newPart.Add(inst);
                        }
                    }
                    catch
                    {
                        // skip on unexpected errors
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
                        var listInstance = (IList)Activator.CreateInstance(listType)!;

                        foreach (var h in newPart)
                        {
                            if (elementType.IsInstanceOfType(h))
                            {
                                listInstance.Add(h);
                                continue;
                            }

                            // Attempt best-effort conversion: create instance of elementType and copy common fields.
                            try
                            {
                                if (Activator.CreateInstance(elementType) is HitObject target)
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
                    if (beatmap.HitObjects is IList<HitObject> current)
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
