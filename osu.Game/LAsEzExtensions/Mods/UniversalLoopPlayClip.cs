// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Framework.Lists;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.LAsEzExtensions.Mods
{
    public class UniversalLoopPlayClip : ModLoopPlayClip, IApplicableAfterBeatmapConversion, IApplicableToBeatmapConverter
    {
        private static readonly MethodInfo memberwise_clone_method = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly FieldInfo? start_time_bindable_field = typeof(HitObject).GetField("StartTimeBindable", BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo? samples_bindable_field = typeof(HitObject).GetField("SamplesBindable", BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo? nested_hit_objects_field = typeof(HitObject).GetField("nestedHitObjects", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? defaults_applied_field = typeof(HitObject).GetField("DefaultsApplied", BindingFlags.Instance | BindingFlags.NonPublic);

        private IBeatmap? converterBeatmap;
        private List<HitObject>? originalHitObjects;
        private SortedList<BreakPeriod>? originalBreaks;
        private bool appliedToConverter;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            if (!appliedToConverter)
            {
                var (cutTimeStart, cutTimeEnd, _) = ResolveSliceTimesForBeatmap(beatmap);
                ApplyLoopToBeatmapStatic(beatmap, LoopCount.Value, cutTimeStart, cutTimeEnd, BreakQuarter.Value, Seed.Value);
                return;
            }

            restoreConverterBeatmap();
        }

        public void ApplyToBeatmapConverter(IBeatmapConverter beatmapConverter)
        {
            var beatmap = beatmapConverter.Beatmap;

            converterBeatmap = beatmap;
            originalHitObjects = beatmap.HitObjects.ToList();
            originalBreaks = new SortedList<BreakPeriod>(Comparer<BreakPeriod>.Default);
            originalBreaks.AddRange(beatmap.Breaks);

            var (cutTimeStart, cutTimeEnd, _) = ResolveSliceTimesForBeatmap(beatmap);
            ApplyLoopToBeatmapStatic(beatmap, LoopCount.Value, cutTimeStart, cutTimeEnd, BreakQuarter.Value, Seed.Value);

            appliedToConverter = true;
        }

        public static void ApplyLoopToBeatmapStatic(IBeatmap? beatmap, int loopCount, double cutTimeStart, double cutTimeEnd, int breakQuarter, int? seed = null)
        {
            if (beatmap == null) return;

            // 禁用倒计时，LP mod 不需要倒计时
            beatmap.Countdown = CountdownType.None;
            beatmap.Breaks.Clear();

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

            var selectedPart = beatmap.HitObjects.Where(h => h.StartTime >= cutTimeStart && h.GetEndTime() <= cutTimeEnd).ToList();

            // 保留原始 HitObject 列表，后续按原始对象深克隆并应用时间偏移
            var sourceObjects = selectedPart;

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

                foreach (var note in sourceObjects)
                {
                    double baseOffset = offset - cutTimeStart;
                    var clone = createDeepClone(note, baseOffset);
                    if (clone != null) newPart.Add(clone);
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

        private static IList<HitSampleInfo>? copySamples(IList<HitSampleInfo>? samples)
        {
            if (samples == null) return null;

            var list = new List<HitSampleInfo>(samples.Count);

            foreach (var s in samples)
            {
                try
                {
                    var sType = s.GetType();
                    var cloned = (HitSampleInfo?)Activator.CreateInstance(sType, s);
                    list.Add(cloned ?? s);
                }
                catch
                {
                    list.Add(s);
                }
            }

            return list;
        }

        private static HitObject? createDeepClone(HitObject source, double baseOffset)
        {
            var clone = memberwise_clone_method.Invoke(source, null) as HitObject;
            if (clone == null)
                return null;

            resetCloneState(clone, source);

            // Apply start time offset
            try
            {
                double newStart = source.StartTime + baseOffset;
                clone.StartTime = newStart;
            }
            catch { clone.StartTime = source.StartTime + baseOffset; }

            // Deep copy samples if present
            try { clone.Samples = copySamples(source.Samples) ?? new List<HitSampleInfo>(); }
            catch { }

            // Copy end/duration where possible
            try
            {
                double srcEnd = source.GetEndTime();
                var endProp = source.GetType().GetProperty("EndTime");

                if (endProp != null && endProp.CanWrite)
                    endProp.SetValue(clone, srcEnd + baseOffset);
                else
                {
                    var durProp = source.GetType().GetProperty("Duration") ?? source.GetType().GetProperty("Length");

                    if (durProp != null && durProp.CanWrite)
                    {
                        double dur = srcEnd - source.StartTime;
                        durProp.SetValue(clone, Convert.ChangeType(dur, durProp.PropertyType));
                    }
                }
            }
            catch { }

            return clone;
        }

        private static void resetCloneState(HitObject clone, HitObject source)
        {
            var newStartBindable = new BindableDouble(source.StartTime);
            start_time_bindable_field?.SetValue(clone, newStartBindable);

            var newSamplesBindable = new BindableList<HitSampleInfo>();
            samples_bindable_field?.SetValue(clone, newSamplesBindable);

            nested_hit_objects_field?.SetValue(clone, new List<HitObject>());
            defaults_applied_field?.SetValue(clone, null);
            clone.HitWindows = null;
        }

        private void restoreConverterBeatmap()
        {
            if (converterBeatmap == null || originalHitObjects == null || originalBreaks == null)
                return;

            var beatmapType = converterBeatmap.GetType();
            var hitObjectsProp = beatmapType.GetProperty("HitObjects");
            if (hitObjectsProp != null && hitObjectsProp.CanWrite)
                hitObjectsProp.SetValue(converterBeatmap, originalHitObjects);

            converterBeatmap.Breaks.Clear();
            foreach (var breakPeriod in originalBreaks)
                converterBeatmap.Breaks.Add(breakPeriod);

            appliedToConverter = false;
            converterBeatmap = null;
            originalHitObjects = null;
            originalBreaks = null;
        }
    }
}
