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
            if (beatmap == null || beatmap.HitObjects.Count == 0)
                return;

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

            // 保留，可以防止开启mod并切换规则集时出现 beatmapConverter.Beatmap 变为 null 导致的崩溃。
            if (beatmap == null || beatmap.HitObjects.Count == 0)
                return;

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
            // beatmap.Countdown = CountdownType.None;
            beatmap.Breaks.Clear();

            double breakTime;

            try
            {
                var timing = beatmap.ControlPointInfo.TimingPointAt(cutTimeStart);
                double halfBeatMs = timing.BeatLength / 2.0;
                breakTime = halfBeatMs * Math.Max(1, breakQuarter);
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
                h.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
            }

            // 在每次循环之间仅在间隙足够且不会与任何 HitObject 重叠时插入休息段（breaks），
            // 以避免出现：已经开始打 note，但主时钟仍显示休息提示的情况。

            beatmap.Breaks.Clear();

            if (loopCount > 1 && breakTime > 0 && newPart.Count > 0)
            {
                for (int i = 0; i < loopCount - 1; i++)
                {
                    double loopStart = cutTimeStart + i * (length + breakTime);
                    double loopEnd = loopStart + length;
                    double nextLoopStart = cutTimeStart + (i + 1) * (length + breakTime);

                    double bStart = Math.Max(0, loopEnd);
                    double bEnd = Math.Max(bStart, nextLoopStart);

                    // 如果间隙长度小于 breakTime，跳过（谨慎判断，避免在 note 存在情况下显示 break）。
                    if (bEnd - bStart < 1.0) // 1 ms 最小阈值
                        continue;

                    // 检查 newPart 中是否有任何 HitObject 与该 break 区间相交（[bStart, bEnd)）
                    bool overlaps = false;

                    foreach (var ho in newPart)
                    {
                        try
                        {
                            double hs = ho.StartTime;
                            double he = ho.GetEndTime();

                            if (hs < bEnd && he > bStart)
                            {
                                overlaps = true;
                                break;
                            }
                        }
                        catch
                        {
                            // 若获取时间失败，则保守地认为可能有重叠，跳过添加。
                            overlaps = true;
                            break;
                        }
                    }

                    if (overlaps)
                        continue;

                    beatmap.Breaks.Add(new BreakPeriod(bStart, bEnd));
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

                    propHitObjects.SetValue(beatmap, listInstance);
                    return;
                }

                // Last resort: try to set directly (may fail if types mismatch)
                propHitObjects.SetValue(beatmap, newPart);
                return;
            }

            // Fallback: use dynamic assignment
            dynamic dyn = beatmap;
            dyn.HitObjects = newPart;
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
            double newStart = source.StartTime + baseOffset;
            clone.StartTime = newStart;

            // Deep copy samples if present
            clone.Samples = copySamples(source.Samples) ?? new List<HitSampleInfo>();

            // Copy end/duration where possible
            double srcEnd = source.GetEndTime();
            var endProp = source.GetType().GetProperty("EndTime");

            if (endProp != null && endProp.CanWrite)
            {
                try
                {
                    endProp.SetValue(clone, srcEnd + baseOffset);
                }
                catch
                {
                    // Some objects (e.g., ConvertSlider) may not support EndTime setter
                }
            }
            else
            {
                var durProp = source.GetType().GetProperty("Duration") ?? source.GetType().GetProperty("Length");

                if (durProp != null && durProp.CanWrite)
                {
                    try
                    {
                        double dur = srcEnd - source.StartTime;
                        durProp.SetValue(clone, Convert.ChangeType(dur, durProp.PropertyType));
                    }
                    catch
                    {
                        // Some objects (e.g., ConvertSlider) require alternative methods (RepeatCount) to adjust duration
                    }
                }
            }

            // // Recursively clone nested hit objects (important for rulesets like Mania which rely on nested objects for sample triggering)
            // try
            // {
            //     var nested = source.NestedHitObjects;
            //
            //     if (nested.Count > 0)
            //     {
            //         var clonedNested = new List<HitObject>(nested.Count);
            //
            //         foreach (var n in nested)
            //         {
            //             var cn = createDeepClone(n, baseOffset);
            //             if (cn != null)
            //                 clonedNested.Add(cn);
            //         }
            //
            //         nested_hit_objects_field?.SetValue(clone, clonedNested);
            //     }
            // }
            // catch { }

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
