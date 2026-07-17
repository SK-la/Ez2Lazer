// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 列内 note-lock 状态机：有序目标 + 游标。全 JudgePrecedence 下 Drawable / Session 共用判定语义。
    /// </summary>
    public sealed class ManiaLaneController
    {
        private readonly List<ManiaLaneEntry> entries = new List<ManiaLaneEntry>();
        private readonly Dictionary<DrawableHitObject, int> drawableIndices = new Dictionary<DrawableHitObject, int>();
        private int cursor;

        private readonly List<AutoMissEntry> autoMissEntries = new List<AutoMissEntry>();
        private readonly Dictionary<DrawableManiaHitObject, AutoMissEntry> autoMissByDrawable = new Dictionary<DrawableManiaHitObject, AutoMissEntry>();
        private int autoMissCursor;
        private bool autoMissSchedulingEnabled;

        private double lastSelectPressTime = double.NaN;
        private EzEnumJudgePrecedence lastSelectPrecedence;
        private ManiaLaneEntry? lastSelectResult;

        private HitModeHelper? missWindowHelper;
        private double cachedMaxMissEarly;
        private double cachedMaxMissLate;
        private bool overlapSearchBoundsValid;
        private readonly List<ManiaLaneEntry> overlapScratch = new List<ManiaLaneEntry>();

        // 热路径避免每次 SelectPress 把实例方法转成新 Func。
        private readonly Func<int, double, bool> isHittableEarliestIndexFunc;
        private readonly Func<ManiaLaneEntry, double, bool> isWithinMissWindowFunc;

        public ManiaLaneController()
        {
            isHittableEarliestIndexFunc = IsHittableEarliestIndex;
            isWithinMissWindowFunc = isWithinMissWindow;
        }

        /// <summary>当前列是否处于 BMS hit mode（由 <see cref="ConfigureMissCollection"/> 自动设置）。</summary>
        internal bool IsBmsMode { get; private set; }

        /// <summary>是否允许 KPoor（post-Bad Poor）路由。通过 <see cref="SetPoorEnabled"/> 从 <see cref="ManiaJudgementRound"/> 同步。</summary>
        internal bool PoorEnabled { get; private set; }

        /// <summary>设置 <see cref="PoorEnabled"/>。由 <see cref="Column.resolvePressRouting"/> 在获取 round 时调用。</summary>
        internal void SetPoorEnabled(bool value) => PoorEnabled = value;

        public IReadOnlyList<ManiaLaneEntry> Entries => entries;

        public void Register(DrawableHitObject drawable, bool scheduleAutoMiss = false)
        {
            if (scheduleAutoMiss || autoMissSchedulingEnabled)
                registerAutoMiss(drawable);

            if (!TryCreateEntry(drawable, out var entry))
                return;

            if (drawableIndices.ContainsKey(drawable))
                Unregister(drawable);

            int insertIndex = entries.BinarySearch(entry, ManiaLaneEntry.StartTimeComparer.INSTANCE);
            if (insertIndex < 0)
                insertIndex = ~insertIndex;

            insertEntryAt(insertIndex, entry);
            invalidateOverlapSearchBounds();
        }

        public void RegisterIfNeeded(DrawableHitObject drawable, bool scheduleAutoMiss = false)
        {
            if (scheduleAutoMiss || autoMissSchedulingEnabled)
                registerAutoMiss(drawable);

            if (!drawableIndices.ContainsKey(drawable))
                Register(drawable, scheduleAutoMiss);
        }

        public void EnableAutoMissScheduling()
        {
            if (autoMissSchedulingEnabled)
                return;

            autoMissSchedulingEnabled = true;

            foreach (var entry in entries)
                registerAutoMiss(entry.Drawable);
        }

        public void Unregister(DrawableHitObject drawable)
        {
            unregisterAutoMiss(drawable);

            if (!drawableIndices.TryGetValue(drawable, out int index))
                return;

            entries.RemoveAt(index);
            drawableIndices.Remove(drawable);

            for (int i = index; i < entries.Count; i++)
                drawableIndices[entries[i].Drawable] = i;

            if (index < cursor)
                cursor--;

            invalidateSelectPressCache();
            invalidateOverlapSearchBounds();
        }

        /// <summary>
        /// 仅检查已经越过 late miss 边界的对象。远期存活对象不再逐帧参与 Drawable 更新。
        /// </summary>
        public int ProcessAutoMiss(double time, bool evaluateResults = true)
        {
            int dueCount = 0;

            for (int i = autoMissCursor; i < autoMissEntries.Count; i++)
            {
                var entry = autoMissEntries[i];

                if (entry.EvaluationStartTime > time)
                    break;

                if (!entry.Drawable.Judged)
                {
                    dueCount++;

                    if (evaluateResults)
                        entry.Drawable.EvaluateColumnAutoMiss();
                }
            }

            while (autoMissCursor < autoMissEntries.Count && autoMissEntries[autoMissCursor].Drawable.Judged)
                autoMissCursor++;

            return dueCount;
        }

        private void registerAutoMiss(DrawableHitObject drawable)
        {
            if (drawable is DrawableHoldNote hold)
            {
                registerAutoMissDrawable(hold.Head);
                registerAutoMissDrawable(hold.Tail);
                registerAutoMissDrawable(hold);
                hold.Body.ColumnSchedulesAutoMiss = true;
                return;
            }

            if (drawable is DrawableManiaHitObject maniaDrawable)
                registerAutoMissDrawable(maniaDrawable);
        }

        private void registerAutoMissDrawable(DrawableManiaHitObject drawable)
        {
            if (autoMissByDrawable.ContainsKey(drawable))
                return;

            double evaluationStartTime = GetAutoMissEvaluationTime(drawable);

            if (!double.IsFinite(evaluationStartTime))
            {
                // Body and other Empty-window auxiliaries are finalized by their owner.
                drawable.ColumnSchedulesAutoMiss = true;
                return;
            }

            var entry = new AutoMissEntry(drawable, evaluationStartTime);
            int index = autoMissEntries.BinarySearch(entry, AutoMissEntry.StartTimeComparer.INSTANCE);

            if (index < 0)
                index = ~index;

            autoMissEntries.Insert(index, entry);
            autoMissByDrawable.Add(drawable, entry);
            drawable.ColumnSchedulesAutoMiss = true;

            if (index < autoMissCursor)
                autoMissCursor = index;
        }

        private void unregisterAutoMiss(DrawableHitObject drawable)
        {
            if (drawable is DrawableHoldNote hold)
            {
                unregisterAutoMissDrawable(hold.Head);
                unregisterAutoMissDrawable(hold.Tail);
                unregisterAutoMissDrawable(hold);
                hold.Body.ColumnSchedulesAutoMiss = false;
                return;
            }

            if (drawable is DrawableManiaHitObject maniaDrawable)
                unregisterAutoMissDrawable(maniaDrawable);
        }

        private void unregisterAutoMissDrawable(DrawableManiaHitObject drawable)
        {
            drawable.ColumnSchedulesAutoMiss = false;

            if (!autoMissByDrawable.Remove(drawable, out var entry))
                return;

            int index = autoMissEntries.IndexOf(entry);

            if (index < 0)
                return;

            autoMissEntries.RemoveAt(index);

            if (index < autoMissCursor)
                autoMissCursor--;
        }

        internal static double GetAutoMissEvaluationTime(DrawableManiaHitObject drawable)
        {
            if (drawable.HitObject.HitWindows is ManiaHitWindows windows)
                return drawable.HitObject.GetEndTime() + windows.MissLateWindow;

            if (drawable is DrawableHoldNote)
                return drawable.HitObject.GetEndTime();

            return double.PositiveInfinity;
        }

        public void UnregisterByHitObject(HitObject hitObject)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].Drawable.HitObject == hitObject)
                    Unregister(entries[i].Drawable);
            }
        }

        public void NotifyJudged(DrawableHitObject drawable)
        {
            drawable = resolveJudgedDrawable(drawable);

            if (!drawableIndices.ContainsKey(drawable))
                return;

            advanceCursor();
        }

        private static DrawableHitObject resolveJudgedDrawable(DrawableHitObject drawable)
        {
            if (drawable is DrawableHoldNoteHead head && head.ParentHold != null)
                return head.ParentHold;

            return drawable;
        }

        /// <summary>
        /// Earliest note-lock：仅游标处未判定物件可击打，且不得越过更晚物件的 StartTime。
        /// </summary>
        public bool IsHittableEarliest(DrawableHitObject drawable, double time)
        {
            if (!drawableIndices.TryGetValue(drawable, out int index))
                return false;

            // Scan forward from cursor to find the first entry that can actually be hit.
            // This handles the case where the cursor note is past its hit window (IsHittableEarliestIndex
            // returns false because a later note has already started) but hasn't been auto-missed yet.
            int effectiveIndex = findEffectiveEarliestIndex(time);

            if (index != effectiveIndex)
                return false;

            if (entries[index].IsPressJudged)
                return false;

            return IsHittableEarliestIndex(index, time);
        }

        /// <summary>
        /// Find the first entry from cursor that is within its miss window and not blocked by a later note.
        /// </summary>
        private int findEffectiveEarliestIndex(double time)
        {
            for (int i = cursor; i < entries.Count; i++)
            {
                if (entries[i].IsPressJudged)
                    continue;

                if (!isWithinMissWindow(entries[i], time))
                    continue;

                if (!IsHittableEarliestIndex(i, time))
                    continue;

                return i;
            }

            return -1;
        }

        public bool IsHittableEarliestIndex(int index, double time)
            => ManiaLanePressSelector.IsHittableEarliest(entries, index, time, static e => e.IsPressJudged, static e => e.StartTime);

        public void ConfigureMissCollection(EzEnumHitMode hitMode, double overallDifficulty, double bpm = 180)
        {
            IsBmsMode = HitModeHelper.IsBMSHitMode(hitMode);

            missWindowHelper = new HitModeHelper(hitMode)
            {
                OverallDifficulty = overallDifficulty,
                BPM = bpm,
            };

            recomputeOverlapSearchBounds();
            invalidateSelectPressCache();
        }

        public bool IsHittable(DrawableHitObject drawable, double time, EzEnumJudgePrecedence precedence)
        {
            if (precedence == EzEnumJudgePrecedence.Earliest)
                return IsHittableEarliest(drawable, time);

            var entry = selectPressEntry(time, precedence);

            if (entry == null)
                return true;

            return ReferenceEquals(entry.RoutedObject, drawable);
        }

        public bool TryGetEntry(DrawableHitObject drawable, out ManiaLaneEntry entry)
        {
            if (drawableIndices.TryGetValue(drawable, out int index))
            {
                entry = entries[index];
                return true;
            }

            entry = null!;
            return false;
        }

        public DrawableHoldNote? ActiveHold { get; private set; }

        internal void SetActiveHold(DrawableHoldNote? hold) => ActiveHold = hold;

        public IEnumerable<ManiaLaneEntry> EnumerateForceMissBefore(double targetStartTime)
        {
            foreach (var entry in entries)
            {
                if (entry.IsPressJudged)
                    continue;

                if (entry.StartTime >= targetStartTime)
                    break;

                yield return entry;
            }
        }

        /// <summary>
        /// 收集 miss 窗内、未判定的列内 press 候选（O(log n + k)）。
        /// 返回新列表（测试/外部可用）；热路径请走 <see cref="selectPressEntryCore"/> 的 scratch 复用。
        /// </summary>
        public List<ManiaLaneEntry> CollectOverlappingEntries(double time)
        {
            var result = new List<ManiaLaneEntry>();
            collectOverlappingInto(time, result);
            return result;
        }

        private void collectOverlappingInto(double time, List<ManiaLaneEntry> result)
        {
            result.Clear();

            if (entries.Count == 0)
                return;

            ensureOverlapSearchBounds();

            if (cachedMaxMissEarly == 0 && cachedMaxMissLate == 0)
                return;

            double searchLowerBound = time - cachedMaxMissLate;
            int lo = 0;
            int hi = entries.Count;

            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;

                if (entries[mid].StartTime < searchLowerBound)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            double searchUpperBound = time + cachedMaxMissEarly;

            for (int i = lo; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (entry.StartTime > searchUpperBound)
                    break;

                if (entry.IsPressJudged || entry.PressWindows == null)
                    continue;

                getMissWindows(entry, out double early, out double late);

                if (time >= entry.StartTime - early && time <= entry.StartTime + late)
                    result.Add(entry);
            }
        }

        private void ensureOverlapSearchBounds()
        {
            if (!overlapSearchBoundsValid)
                recomputeOverlapSearchBounds();
        }

        private void recomputeOverlapSearchBounds()
        {
            cachedMaxMissEarly = 0;
            cachedMaxMissLate = 0;

            if (missWindowHelper != null)
            {
                cachedMaxMissEarly = missWindowHelper.WindowFor(HitResult.Miss, true);
                cachedMaxMissLate = missWindowHelper.WindowFor(HitResult.Miss, false);

                if (IsBmsMode)
                    BmsHitModeJudgement.ExpandMissCollectionWindows(missWindowHelper, 1, ref cachedMaxMissEarly, ref cachedMaxMissLate);
            }
            else
            {
                foreach (var entry in entries)
                {
                    if (entry.PressWindows == null)
                        continue;

                    getMissWindows(entry, out double early, out double late);
                    cachedMaxMissEarly = Math.Max(cachedMaxMissEarly, early);
                    cachedMaxMissLate = Math.Max(cachedMaxMissLate, late);
                }
            }

            overlapSearchBoundsValid = true;
        }

        private void invalidateOverlapSearchBounds() => overlapSearchBoundsValid = false;

        /// <summary>
        /// 按 JudgePrecedence 选择本列 press 目标（含 BMS post-Bad KPoor）。
        /// </summary>
        public ManiaLaneEntry? SelectPressEntry(double time, EzEnumJudgePrecedence precedence)
            => selectPressEntry(time, precedence);

        private ManiaLaneEntry? selectPressEntry(double time, EzEnumJudgePrecedence precedence)
        {
            if (time == lastSelectPressTime && precedence == lastSelectPrecedence)
                return lastSelectResult;

            lastSelectPressTime = time;
            lastSelectPrecedence = precedence;
            lastSelectResult = selectPressEntryCore(time, precedence);
            return lastSelectResult;
        }

        private ManiaLaneEntry? selectPressEntryCore(double time, EzEnumJudgePrecedence precedence)
        {
            bool tryPostBadRoute = IsBmsMode && PoorEnabled;

            if (precedence == EzEnumJudgePrecedence.Earliest)
            {
                return ManiaLanePressSelector.SelectDrawablePressEntry(
                    entries,
                    cursor,
                    time,
                    precedence,
                    tryPostBadRoute,
                    Array.Empty<ManiaLaneEntry>(),
                    isHittableEarliestIndexFunc,
                    isWithinMissWindowFunc,
                    isPostBadKPoorRoutable,
                    distanceToNonBadWindow);
            }

            collectOverlappingInto(time, overlapScratch);

            return ManiaLanePressSelector.SelectDrawablePressEntry(
                entries,
                cursor,
                time,
                precedence,
                tryPostBadRoute,
                overlapScratch,
                isHittableEarliestIndexFunc,
                isWithinMissWindowFunc,
                isPostBadKPoorRoutable,
                distanceToNonBadWindow);
        }

        private bool isWithinMissWindow(ManiaLaneEntry entry, double time)
        {
            if (entry.PressWindows == null)
                return false;

            getMissWindows(entry, out double early, out double late);
            return time >= entry.StartTime - early && time <= entry.StartTime + late;
        }

        private void getMissWindows(ManiaLaneEntry entry, out double early, out double late)
        {
            early = entry.PressWindows!.WindowFor(HitResult.Miss, true);
            late = entry.PressWindows.WindowFor(HitResult.Miss, false);

            if (IsBmsMode && missWindowHelper != null)
                BmsHitModeJudgement.ExpandMissCollectionWindows(missWindowHelper, 1, ref early, ref late);
        }

        private static bool isPostBadKPoorRoutable(ManiaLaneEntry entry)
        {
            if (!entry.IsPressJudged || !entry.BmsRoute.CanRouteToKPoor)
                return false;

            if (entry.RoutedObject is DrawableNote note)
                return note.CanRouteToKPoor;

            if (entry.RoutedObject is DrawableHoldNote hold && hold.Tail.Judged)
                return hold.Tail.CanRouteToKPoor;

            return false;
        }

        private static double distanceToNonBadWindow(DrawableHitObject obj, double pressTime)
        {
            var windows = obj.HitObject.HitWindows;

            if (windows == null)
                return double.PositiveInfinity;

            double early = windows.WindowFor(HitResult.Good);
            double late = windows.WindowFor(HitResult.Good);

            if (windows is ManiaHitWindows maniaWindows)
            {
                early = maniaWindows.WindowFor(HitResult.Good, true);
                late = maniaWindows.WindowFor(HitResult.Good, false);
            }

            double start = obj.HitObject.StartTime - early;
            double end = obj.HitObject.StartTime + late;

            if (pressTime < start)
                return start - pressTime;

            if (pressTime > end)
                return pressTime - end;

            return 0;
        }

        public static bool TryCreateEntry(DrawableHitObject drawable, out ManiaLaneEntry entry)
        {
            entry = null!;

            if (drawable.HitObject == null)
                return false;

            if (drawable is DrawableHoldNoteHead or DrawableHoldNoteTail)
                return false;

            if (drawable is DrawableHoldNote hold)
            {
                if (hold.Judged || hold.Head.Judged)
                    return false;

                if (hold.Head.HitObject.HitWindows is not ManiaHitWindows headWindows || headWindows.WindowFor(HitResult.Miss) == 0)
                    return false;

                entry = new ManiaLaneEntry(hold, hold.Head, hold.Head.HitObject.StartTime, headWindows);
                return true;
            }

            if (drawable.Judged)
                return false;

            if (drawable.HitObject.HitWindows is not ManiaHitWindows windows || windows.WindowFor(HitResult.Miss) == 0)
                return false;

            entry = new ManiaLaneEntry(drawable, drawable, drawable.HitObject.StartTime, windows);
            return true;
        }

        private void advanceCursor()
        {
            while (cursor < entries.Count && entries[cursor].IsPressJudged)
                cursor++;
        }

        private void insertEntryAt(int insertIndex, ManiaLaneEntry entry)
        {
            entries.Insert(insertIndex, entry);
            drawableIndices[entry.Drawable] = insertIndex;

            for (int i = insertIndex + 1; i < entries.Count; i++)
                drawableIndices[entries[i].Drawable] = i;

            if (insertIndex < cursor)
                cursor++;

            invalidateSelectPressCache();
        }

        private void invalidateSelectPressCache()
        {
            lastSelectPressTime = double.NaN;
            lastSelectResult = null;
        }

        private sealed class AutoMissEntry
        {
            public DrawableManiaHitObject Drawable { get; }

            public double EvaluationStartTime { get; }

            public AutoMissEntry(DrawableManiaHitObject drawable, double evaluationStartTime)
            {
                Drawable = drawable;
                EvaluationStartTime = evaluationStartTime;
            }

            internal sealed class StartTimeComparer : IComparer<AutoMissEntry>
            {
                public static readonly StartTimeComparer INSTANCE = new StartTimeComparer();

                public int Compare(AutoMissEntry? x, AutoMissEntry? y)
                {
                    if (ReferenceEquals(x, y))
                        return 0;

                    if (x is null)
                        return -1;

                    if (y is null)
                        return 1;

                    return x.EvaluationStartTime.CompareTo(y.EvaluationStartTime);
                }
            }
        }
    }

    public sealed class ManiaLaneEntry
    {
        public DrawableHitObject Drawable { get; }

        public DrawableHitObject RoutedObject { get; }

        public DrawableHitObject JudgementObject { get; }

        public double StartTime { get; }

        public ManiaHitWindows? PressWindows { get; }

        public BmsHitModeJudgement.BmsRouteState BmsRoute { get; } = new BmsHitModeJudgement.BmsRouteState();

        public bool IsJudged => Drawable.Judged;

        public bool IsPressJudged => RoutedObject.Judged || JudgementObject.Judged;

        public ManiaLaneEntry(DrawableHitObject routedObject, DrawableHitObject judgementObject, double startTime, ManiaHitWindows? pressWindows)
        {
            Drawable = routedObject;
            RoutedObject = routedObject;
            JudgementObject = judgementObject;
            StartTime = startTime;
            PressWindows = pressWindows;
        }

        internal sealed class StartTimeComparer : IComparer<ManiaLaneEntry>
        {
            public static readonly StartTimeComparer INSTANCE = new StartTimeComparer();

            public int Compare(ManiaLaneEntry? x, ManiaLaneEntry? y)
            {
                if (ReferenceEquals(x, y))
                    return 0;

                if (x is null)
                    return -1;

                if (y is null)
                    return 1;

                return x.StartTime.CompareTo(y.StartTime);
            }
        }
    }
}
