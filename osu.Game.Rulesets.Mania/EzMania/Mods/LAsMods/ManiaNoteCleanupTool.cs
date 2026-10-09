// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods
{
    public enum NoteCleanupKeepStrategy
    {
        Older = 1,
        Newer = 2,
    }

    /// <summary>
    /// 单点落在 LN 体内时的处理。三种都会执行其中一种。
    /// </summary>
    public enum LnBodyTapMode
    {
        [LocalisableDescription(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.LN_BODY_DROP_TAP))]
        DropTap,

        [LocalisableDescription(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.LN_BODY_TRUNCATE))]
        Truncate,

        [LocalisableDescription(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.LN_BODY_CONTINUE))]
        Continue,
    }

    /// <summary>
    /// 清理参数。重叠单点每次都删。默认再做 LN 体内截断并延续，以及 16ms 过密删偶数项。
    /// </summary>
    public sealed class NoteCleanupOptions
    {
        public static NoteCleanupOptions Default { get; } = new NoteCleanupOptions();

        public LnBodyTapMode LnBodyTapMode { get; init; } = LnBodyTapMode.Continue;

        /// <summary>
        /// 单点过密：删偶数项，留下结尾那颗非密集 note。
        /// </summary>
        public bool CleanDenseNotes { get; init; } = true;

        /// <summary>
        /// LN 过密：与相邻 note 过近时按同一间隙收尾，或把 LN 头后移。默认开。
        /// </summary>
        public bool CleanLnDensity { get; init; } = true;

        /// <summary>
        /// 非 null 时，间隙取该时刻 <c>BeatLength / 分母</c>，不再用 <see cref="MinimumGapMs"/>。
        /// </summary>
        public int? BeatDivisor { get; init; }

        public int MinimumGapMs { get; init; } = 16;

        public bool UseKeepStrategy { get; init; }

        public NoteCleanupKeepStrategy KeepStrategy { get; init; } = NoteCleanupKeepStrategy.Older;
    }

    /// <summary>
    /// 一次清理改了什么。无变化时不写日志。
    /// </summary>
    public sealed class NoteCleanupReport
    {
        public int Dropped { get; set; }

        public int Truncated { get; set; }

        public int ConvertedToTap { get; set; }

        public int Continued { get; set; }

        public List<(double Start, double End)> EmptySpans { get; } = new List<(double Start, double End)>();

        public bool HasChanges => Dropped > 0 || Truncated > 0 || ConvertedToTap > 0 || Continued > 0 || EmptySpans.Count > 0;
    }

    public static class ManiaNoteCleanupTool
    {
        private const double empty_span_floor_ms = 2000;
        private const double empty_span_beats = 4;

        /// <summary>
        /// 统一格式化铺面。重叠单点必删，默认再截断 LN 并延续、按 16ms 删过密偶数项。
        /// </summary>
        public static NoteCleanupReport CleanupBeatmap(ManiaBeatmap beatmap, int? seed = null)
        {
            _ = seed;
            return CleanupBeatmap(beatmap, NoteCleanupOptions.Default);
        }

        public static NoteCleanupReport CleanupBeatmap(ManiaBeatmap beatmap, NoteCleanupOptions options)
        {
            var report = new NoteCleanupReport();

            if (beatmap.HitObjects.Count == 0)
                return report;

            List<ManiaHitObject> original = beatmap.HitObjects.ToList();
            List<ManiaHitObject> resolved = resolveColumns(beatmap, original, options, report);

            if (options.CleanDenseNotes || options.CleanLnDensity)
                resolved = applyDensity(beatmap, resolved, options, report);

            replaceHitObjects(beatmap, resolved);

            collectEmptySpans(beatmap, original, beatmap.HitObjects, report);
            logReport(report);
            return report;
        }

        /// <summary>
        /// 同列重叠：删重叠单点，并按 <see cref="LnBodyTapMode"/> 处理落在 LN 体内的单点。
        /// </summary>
        public static void CleanOverlapNotes(ManiaBeatmap beatmap) =>
            CleanOverlapNotes(beatmap, NoteCleanupOptions.Default);

        public static void CleanOverlapNotes(ManiaBeatmap beatmap, NoteCleanupOptions options)
        {
            if (beatmap.HitObjects.Count == 0)
                return;

            var report = new NoteCleanupReport();
            List<ManiaHitObject> resolved = resolveColumns(beatmap, beatmap.HitObjects.ToList(), options, report);
            replaceHitObjects(beatmap, resolved);
            logReport(report);
        }

        public static double GetMinimumGapMs(ManiaBeatmap beatmap, int beat = 8)
        {
            if (beatmap.HitObjects.Count == 0)
                return 0;

            double startTime = beatmap.HitObjects.Min(h => h.StartTime);
            return gapAt(beatmap, startTime, new NoteCleanupOptions { BeatDivisor = beat });
        }

        public static double GetMinimumGapMs(ManiaBeatmap beatmap, double time, NoteCleanupOptions options) =>
            gapAt(beatmap, time, options);

        /// <summary>
        /// 过密段删偶数项，留下每段最后一颗非密集 note。
        /// </summary>
        public static void EnforceMinimumGaps(ManiaBeatmap beatmap) =>
            EnforceMinimumGaps(beatmap, NoteCleanupOptions.Default);

        public static void EnforceMinimumGaps(ManiaBeatmap beatmap, NoteCleanupOptions options)
        {
            if (beatmap.HitObjects.Count == 0)
                return;

            var report = new NoteCleanupReport();
            List<ManiaHitObject> thinned = applyDensity(beatmap, beatmap.HitObjects.ToList(), options, report);
            replaceHitObjects(beatmap, thinned);
            logReport(report);
        }

        /// <summary>
        /// 截断过短的反键缝隙。默认按 1/8 拍与 30ms 取较大值，供仍单独调用这条的转谱使用。
        /// </summary>
        public static void EnforceHoldReleaseGap(ManiaBeatmap beatmap, int beat = 8) =>
            EnforceHoldReleaseGap(beatmap, new NoteCleanupOptions { BeatDivisor = beat, MinimumGapMs = 30 });

        public static void EnforceHoldReleaseGap(ManiaBeatmap beatmap, NoteCleanupOptions options)
        {
            if (beatmap.HitObjects.Count == 0)
                return;

            var groups = beatmap.HitObjects.GroupBy(o => o.Column).ToList();

            foreach (var group in groups)
            {
                var list = group.OrderBy(o => o.StartTime).ToList();

                for (int i = 0; i < list.Count - 1; i++)
                {
                    if (list[i] is not HoldNote hold || !isHold(hold))
                        continue;

                    var next = list[i + 1];
                    double minGapMs = holdReleaseGap(beatmap, next.StartTime, options);
                    double gap = next.StartTime - hold.EndTime;

                    if (gap >= minGapMs)
                        continue;

                    double newEnd = next.StartTime - minGapMs;

                    if (newEnd <= hold.EndTime)
                        hold.EndTime = newEnd;

                    if (hold.EndTime - hold.StartTime < minGapMs)
                    {
                        var note = toTap(hold);
                        beatmap.HitObjects.Remove(hold);
                        beatmap.HitObjects.Add(note);
                        list[i] = note;
                    }
                }
            }
        }

        private static List<ManiaHitObject> resolveColumns(ManiaBeatmap beatmap, List<ManiaHitObject> source, NoteCleanupOptions options, NoteCleanupReport report)
        {
            int columnCount = Math.Max(1, beatmap.TotalColumns);

            foreach (ManiaHitObject hitObject in source)
                columnCount = Math.Max(columnCount, hitObject.Column + 1);

            var index = new ManiaObjectColumnIndex(columnCount);
            var skipped = new List<ManiaHitObject>();

            foreach (ManiaHitObject hitObject in source)
            {
                if ((uint)hitObject.Column >= (uint)columnCount)
                    skipped.Add(hitObject);
                else
                    index.Add(hitObject);
            }

            var resolved = new List<ManiaHitObject>(source.Count);
            var column = new List<ManiaHitObject>();

            for (int i = 0; i < columnCount; i++)
            {
                index.CopyColumn(i, column);
                resolved.AddRange(resolveColumn(beatmap, column, options, report));
            }

            resolved.AddRange(skipped);
            return resolved;
        }

        private static List<ManiaHitObject> resolveColumn(ManiaBeatmap beatmap, List<ManiaHitObject> column, NoteCleanupOptions options, NoteCleanupReport report)
        {
            var kept = new List<ManiaHitObject>(column.Count);

            foreach (ManiaHitObject obj in column)
            {
                ManiaHitObject incoming = obj;

                if (kept.Count > 0 && isTap(incoming) && incoming.StartTime == kept[^1].StartTime)
                {
                    if (isTap(kept[^1]) && options.UseKeepStrategy && options.KeepStrategy == NoteCleanupKeepStrategy.Newer)
                        kept[^1] = incoming;

                    report.Dropped++;
                    continue;
                }

                if (kept.Count > 0 && isTap(kept[^1]) && isHold(incoming) && incoming.StartTime == kept[^1].StartTime)
                {
                    kept.RemoveAt(kept.Count - 1);
                    report.Dropped++;
                }

                if (kept.Count > 0 && isHold(kept[^1]) && isTap(incoming) && isInsideHold(kept[^1], incoming.StartTime))
                {
                    if (!applyBodyTap(beatmap, kept, ref incoming, options, report))
                        continue;
                }
                else if (kept.Count > 0 && isHold(kept[^1]) && isHold(incoming))
                {
                    if (!applyHoldOverlap(beatmap, kept, incoming, options, report))
                        continue;
                }

                kept.Add(incoming);
            }

            return kept;
        }

        private static bool applyBodyTap(ManiaBeatmap beatmap, List<ManiaHitObject> kept, ref ManiaHitObject incoming, NoteCleanupOptions options, NoteCleanupReport report)
        {
            var hold = (HoldNote)kept[^1];
            double originalEnd = hold.EndTime;
            double gap = gapAt(beatmap, incoming.StartTime, options);

            if (options.LnBodyTapMode == LnBodyTapMode.DropTap)
            {
                report.Dropped++;
                return false;
            }

            truncateHold(kept, hold, incoming.StartTime - gap, gap, report);

            if (options.LnBodyTapMode == LnBodyTapMode.Continue && originalEnd - incoming.StartTime >= gap)
            {
                incoming = toHold(incoming, originalEnd);
                report.Continued++;
            }

            return true;
        }

        /// <summary>
        /// 返回 false 表示后一条 LN 被丢掉。
        /// </summary>
        private static bool applyHoldOverlap(ManiaBeatmap beatmap, List<ManiaHitObject> kept, ManiaHitObject incoming, NoteCleanupOptions options, NoteCleanupReport report)
        {
            ManiaHitObject prev = kept[^1];

            if (incoming.StartTime == prev.StartTime)
            {
                if (endTime(incoming) > endTime(prev))
                    kept[^1] = incoming;

                report.Dropped++;
                return false;
            }

            if (incoming.StartTime < endTime(prev))
            {
                double gap = gapAt(beatmap, incoming.StartTime, options);
                truncateHold(kept, (HoldNote)prev, incoming.StartTime - gap, gap, report);
            }

            return true;
        }

        private static void truncateHold(List<ManiaHitObject> kept, HoldNote hold, double newEnd, double gap, NoteCleanupReport report)
        {
            if (newEnd - hold.StartTime < gap)
            {
                kept[^1] = toTap(hold);
                report.ConvertedToTap++;
                return;
            }

            if (newEnd < hold.EndTime)
            {
                hold.EndTime = newEnd;
                report.Truncated++;
            }
        }

        private static List<ManiaHitObject> thinDenseNotes(ManiaBeatmap beatmap, List<ManiaHitObject> source, NoteCleanupOptions options, NoteCleanupReport report)
        {
            int columnCount = 1;

            foreach (ManiaHitObject hitObject in source)
                columnCount = Math.Max(columnCount, hitObject.Column + 1);

            var index = new ManiaObjectColumnIndex(columnCount);
            var skipped = new List<ManiaHitObject>();

            foreach (ManiaHitObject hitObject in source)
            {
                if ((uint)hitObject.Column >= (uint)columnCount)
                    skipped.Add(hitObject);
                else
                    index.Add(hitObject);
            }

            var thinned = new List<ManiaHitObject>(source.Count);
            var column = new List<ManiaHitObject>();

            for (int i = 0; i < columnCount; i++)
            {
                index.CopyColumn(i, column);
                thinned.AddRange(thinColumn(beatmap, column, options, report));
            }

            thinned.AddRange(skipped);
            return thinned;
        }

        private static List<ManiaHitObject> thinColumn(ManiaBeatmap beatmap, List<ManiaHitObject> notes, NoteCleanupOptions options, NoteCleanupReport report)
        {
            var result = new List<ManiaHitObject>(notes.Count);
            int i = 0;

            while (i < notes.Count)
            {
                if (i == notes.Count - 1
                    || gapBetween(notes[i], notes[i + 1]) >= gapAt(beatmap, notes[i + 1].StartTime, options)
                    || involvesHold(notes[i], notes[i + 1]))
                {
                    result.Add(notes[i]);
                    i++;
                    continue;
                }

                int j = i + 1;

                while (j < notes.Count
                       && gapBetween(notes[j - 1], notes[j]) < gapAt(beatmap, notes[j].StartTime, options)
                       && !involvesHold(notes[j - 1], notes[j]))
                    j++;

                bool stoppedBeforeHold = j < notes.Count && involvesHold(notes[j - 1], notes[j]);

                if (stoppedBeforeHold)
                {
                    int lastTap = j - 1;

                    for (int k = i; k <= lastTap; k++)
                    {
                        bool isTerminator = k == lastTap;
                        bool dropEven = !isTerminator && ((k - i) % 2 == 1);

                        if (dropEven)
                            report.Dropped++;
                        else
                            result.Add(notes[k]);
                    }

                    i = j;
                    continue;
                }

                int terminator = j < notes.Count ? j : notes.Count - 1;

                for (int k = i; k <= terminator; k++)
                {
                    bool isTerminator = k == terminator;
                    bool dropEven = !isTerminator && ((k - i) % 2 == 1);

                    if (dropEven)
                        report.Dropped++;
                    else
                        result.Add(notes[k]);
                }

                i = terminator + 1;
            }

            return result;
        }

        private static List<ManiaHitObject> applyDensity(ManiaBeatmap beatmap, List<ManiaHitObject> source, NoteCleanupOptions options, NoteCleanupReport report)
        {
            List<ManiaHitObject> current = source;

            if (options.CleanDenseNotes)
                current = thinDenseNotes(beatmap, current, options, report);

            if (options.CleanLnDensity)
                current = openLnGaps(beatmap, current, options, report);

            return current;
        }

        private static List<ManiaHitObject> openLnGaps(ManiaBeatmap beatmap, List<ManiaHitObject> source, NoteCleanupOptions options, NoteCleanupReport report)
        {
            int columnCount = 1;

            foreach (ManiaHitObject hitObject in source)
                columnCount = Math.Max(columnCount, hitObject.Column + 1);

            var index = new ManiaObjectColumnIndex(columnCount);
            var skipped = new List<ManiaHitObject>();

            foreach (ManiaHitObject hitObject in source)
            {
                if ((uint)hitObject.Column >= (uint)columnCount)
                    skipped.Add(hitObject);
                else
                    index.Add(hitObject);
            }

            var opened = new List<ManiaHitObject>(source.Count);
            var column = new List<ManiaHitObject>();

            for (int i = 0; i < columnCount; i++)
            {
                index.CopyColumn(i, column);
                opened.AddRange(openColumn(beatmap, column, options, report));
            }

            opened.AddRange(skipped);
            return opened;
        }

        private static List<ManiaHitObject> openColumn(ManiaBeatmap beatmap, List<ManiaHitObject> notes, NoteCleanupOptions options, NoteCleanupReport report)
        {
            var result = new List<ManiaHitObject>(notes.Count);

            for (int i = 0; i < notes.Count; i++)
            {
                if (i == notes.Count - 1)
                {
                    result.Add(notes[i]);
                    break;
                }

                ManiaHitObject prev = notes[i];
                ManiaHitObject next = notes[i + 1];
                double required = gapAt(beatmap, next.StartTime, options);

                if (gapBetween(prev, next) >= required || !involvesHold(prev, next))
                {
                    result.Add(prev);
                    continue;
                }

                if (isHold(prev))
                {
                    double newEnd = next.StartTime - required;

                    if (newEnd - prev.StartTime < required)
                    {
                        result.Add(toTap(prev));
                        report.ConvertedToTap++;
                    }
                    else
                    {
                        ((HoldNote)prev).EndTime = newEnd;
                        result.Add(prev);
                        report.Truncated++;
                    }

                    continue;
                }

                result.Add(prev);

                var hold = (HoldNote)next;
                double end = hold.EndTime;
                double newStart = endTime(prev) + required;

                if (end - newStart < required)
                {
                    notes[i + 1] = toTap(hold);
                    report.ConvertedToTap++;
                }
                else
                {
                    hold.StartTime = newStart;
                    hold.EndTime = end;
                    report.Truncated++;
                }
            }

            return result;
        }

        private static bool involvesHold(ManiaHitObject prev, ManiaHitObject next) =>
            isHold(prev) || isHold(next);

        private static void collectEmptySpans(ManiaBeatmap beatmap, List<ManiaHitObject> original, IEnumerable<ManiaHitObject> survivors, NoteCleanupReport report)
        {
            List<double> originalTimes = original.Select(h => h.StartTime).OrderBy(t => t).ToList();

            if (originalTimes.Count == 0)
                return;

            List<double> survivorTimes = survivors.Select(h => h.StartTime).Distinct().OrderBy(t => t).ToList();

            if (survivorTimes.Count == 0)
            {
                tryAddEmptySpan(beatmap, originalTimes, originalTimes[0], originalTimes[^1], report);
                return;
            }

            for (int i = 0; i < survivorTimes.Count - 1; i++)
                tryAddEmptySpan(beatmap, originalTimes, survivorTimes[i], survivorTimes[i + 1], report);
        }

        private static void tryAddEmptySpan(ManiaBeatmap beatmap, List<double> originalTimes, double start, double end, NoteCleanupReport report)
        {
            double span = end - start;
            double beatLength = beatmap.ControlPointInfo.TimingPointAt(start).BeatLength;
            double limit = beatLength > 0 ? Math.Max(empty_span_floor_ms, beatLength * empty_span_beats) : empty_span_floor_ms;

            if (span <= limit)
                return;

            if (!originalTimes.Any(t => t > start && t < end))
                return;

            report.EmptySpans.Add((start, end));
        }

        private static void logReport(NoteCleanupReport report)
        {
            if (!report.HasChanges)
                return;

            string spans = report.EmptySpans.Count == 0
                ? string.Empty
                : " empty=" + string.Join(",", report.EmptySpans.Select(s => $"{s.Start:0.###}-{s.End:0.###}"));

            Logger.Log($"[ManiaNoteCleanup] dropped={report.Dropped} truncated={report.Truncated} toTap={report.ConvertedToTap} continued={report.Continued}{spans}",
                Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
        }

        private static void replaceHitObjects(ManiaBeatmap beatmap, List<ManiaHitObject> objects)
        {
            beatmap.HitObjects.Clear();
            beatmap.HitObjects.AddRange(objects.OrderBy(o => o.StartTime).ThenBy(o => o.Column));
        }

        private static double gapAt(ManiaBeatmap beatmap, double time, NoteCleanupOptions options)
        {
            if (options.BeatDivisor is int divisor && divisor > 0)
            {
                double beatLength = beatmap.ControlPointInfo.TimingPointAt(time).BeatLength;

                if (beatLength > 0)
                    return beatLength / divisor;
            }

            return options.MinimumGapMs;
        }

        private static double holdReleaseGap(ManiaBeatmap beatmap, double time, NoteCleanupOptions options)
        {
            int divisor = Math.Max(1, options.BeatDivisor ?? 8);
            double beatLength = beatmap.ControlPointInfo.TimingPointAt(time).BeatLength;

            if (beatLength <= 0)
                return options.MinimumGapMs;

            return Math.Max(options.MinimumGapMs, beatLength / divisor);
        }

        private static double gapBetween(ManiaHitObject prev, ManiaHitObject next) =>
            next.StartTime - endTime(prev);

        private static double endTime(ManiaHitObject hitObject) =>
            isHold(hitObject) ? ((HoldNote)hitObject).EndTime : hitObject.StartTime;

        private static bool isHold(ManiaHitObject hitObject) =>
            hitObject is HoldNote hold && hold.EndTime > hold.StartTime;

        private static bool isTap(ManiaHitObject hitObject) => !isHold(hitObject);

        private static bool isInsideHold(ManiaHitObject hold, double time) =>
            time > hold.StartTime && time < endTime(hold);

        private static Note toTap(ManiaHitObject source) => new Note
        {
            StartTime = source.StartTime,
            Column = source.Column,
            Samples = source.Samples?.ToList() ?? new List<HitSampleInfo>(),
        };

        private static HoldNote toHold(ManiaHitObject tap, double end) => new HoldNote
        {
            Column = tap.Column,
            StartTime = tap.StartTime,
            Duration = end - tap.StartTime,
            NodeSamples = new List<IList<HitSampleInfo>> { tap.Samples?.ToList() ?? new List<HitSampleInfo>(), Array.Empty<HitSampleInfo>() },
        };
    }
}
