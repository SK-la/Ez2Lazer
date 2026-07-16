// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 列级 press 目标选择（M/N 共用 fold / Earliest / BMS post-Bad 语义）。
    /// </summary>
    internal static class ManiaLanePressSelector
    {
        private static readonly Func<ManiaLaneEntry, bool> entry_is_press_judged = e => e.IsPressJudged;
        private static readonly Func<ManiaLaneEntry, double> entry_start_time = e => e.StartTime;
        private static readonly Func<ManiaLaneEntry, ManiaHitWindows?> entry_press_windows = e => e.PressWindows;

        internal static bool IsHittableEarliest<T>(
            IReadOnlyList<T> column,
            int index,
            double time,
            Func<T, bool> isJudged,
            Func<T, double> startTime)
        {
            for (int i = index + 1; i < column.Count; i++)
            {
                if (isJudged(column[i]))
                    continue;

                if (time >= startTime(column[i]))
                    return false;
            }

            return true;
        }

        internal static LaneTargetState? SelectSessionTarget(
            IReadOnlyList<LaneTargetState> candidates,
            IReadOnlyList<LaneTargetState> laneStates,
            double time,
            EzEnumJudgePrecedence precedence,
            bool allowFallbackToEarliest)
        {
            if (candidates.Count == 0)
                return null;

            if (precedence == EzEnumJudgePrecedence.Earliest)
                return selectEarliestSessionTarget(candidates, laneStates, time);

            return selectByPrecedence(
                candidates,
                time,
                precedence,
                s => s.Judged,
                s => s.Target.StartTime,
                s => s.Target.HitWindows as ManiaHitWindows);
        }

        internal static ManiaLaneEntry? SelectDrawablePressEntry(
            IReadOnlyList<ManiaLaneEntry> entries,
            int cursor,
            double time,
            EzEnumJudgePrecedence precedence,
            bool tryPostBadRoute,
            IReadOnlyList<ManiaLaneEntry> overlapping,
            Func<int, double, bool> isHittableEarliestIndex,
            Func<ManiaLaneEntry, double, bool> isWithinMissWindow,
            Func<ManiaLaneEntry, bool> isPostBadKPoorRoutable,
            Func<DrawableHitObject, double, double> distanceToNonBadWindow)
        {
            if (tryPostBadRoute
                && trySelectPostBadDrawableEntry(entries, time, isWithinMissWindow, isPostBadKPoorRoutable, distanceToNonBadWindow, out var postBad))
            {
                return postBad;
            }

            if (precedence == EzEnumJudgePrecedence.Earliest)
                return selectEarliestDrawableEntry(entries, cursor, time, isWithinMissWindow, isHittableEarliestIndex);

            if (overlapping.Count == 0)
                return null;

            if (overlapping.Count == 1)
                return overlapping[0];

            // CollectOverlappingEntries 已按 StartTime 递增；勿再 new List+Sort；SelectFold 用静态 Func 避免每按分配。
            return OrderedHitPolicyHelper.SelectFold(
                overlapping,
                entry_is_press_judged,
                entry_start_time,
                entry_press_windows,
                time,
                precedence == EzEnumJudgePrecedence.Combo);
        }

        private static LaneTargetState? selectEarliestSessionTarget(
            IReadOnlyList<LaneTargetState> candidates,
            IReadOnlyList<LaneTargetState> laneStates,
            double time)
        {
            var sorted = new List<LaneTargetState>(candidates);
            sorted.Sort((a, b) => a.Target.StartTime.CompareTo(b.Target.StartTime));

            foreach (var candidate in sorted)
            {
                int index = indexOf(laneStates, candidate);
                if (index < 0 || !IsHittableEarliest(laneStates, index, time, static s => s.Judged, static s => s.Target.StartTime))
                    continue;

                return candidate;
            }

            return null;
        }

        private static ManiaLaneEntry? selectEarliestDrawableEntry(
            IReadOnlyList<ManiaLaneEntry> entries,
            int cursor,
            double time,
            Func<ManiaLaneEntry, double, bool> isWithinMissWindow,
            Func<int, double, bool> isHittableEarliestIndex)
        {
            if (cursor >= entries.Count)
                return null;

            var entry = entries[cursor];

            if (entry.IsPressJudged || !isWithinMissWindow(entry, time))
                return null;

            if (!isHittableEarliestIndex(cursor, time))
                return null;

            return entry;
        }

        private static bool trySelectPostBadDrawableEntry(
            IReadOnlyList<ManiaLaneEntry> entries,
            double time,
            Func<ManiaLaneEntry, double, bool> isWithinMissWindow,
            Func<ManiaLaneEntry, bool> isPostBadKPoorRoutable,
            Func<DrawableHitObject, double, double> distanceToNonBadWindow,
            out ManiaLaneEntry? selected)
        {
            selected = null;
            ManiaLaneEntry? postBadCandidate = null;
            double postBadDistance = double.PositiveInfinity;

            foreach (var entry in entries)
            {
                if (!isPostBadKPoorRoutable(entry) || !isWithinMissWindow(entry, time))
                    continue;

                double distance = distanceToNonBadWindow(entry.RoutedObject, time);

                if (postBadCandidate == null || distance < postBadDistance
                                             || (distance == postBadDistance && entry.StartTime < postBadCandidate.StartTime))
                {
                    postBadCandidate = entry;
                    postBadDistance = distance;
                }
            }

            if (postBadCandidate == null)
                return false;

            double unjudgedMin = double.PositiveInfinity;

            foreach (var entry in entries)
            {
                if (entry.IsPressJudged || !isWithinMissWindow(entry, time))
                    continue;

                unjudgedMin = Math.Min(unjudgedMin, distanceToNonBadWindow(entry.RoutedObject, time));
            }

            if (postBadDistance > unjudgedMin || postBadCandidate.BmsRoute.HasLateKPoor)
                return false;

            selected = postBadCandidate;
            return true;
        }

        private static T selectByPrecedence<T>(
            IReadOnlyList<T> candidates,
            double time,
            EzEnumJudgePrecedence precedence,
            Func<T, bool> isJudged,
            Func<T, double> startTime,
            Func<T, ManiaHitWindows?> windows) where T : class
        {
            var overlapping = new List<T>(candidates);
            overlapping.Sort((a, b) => startTime(a).CompareTo(startTime(b)));

            bool combo = precedence == EzEnumJudgePrecedence.Combo;

            var picked = OrderedHitPolicyHelper.SelectFold(
                overlapping,
                isJudged,
                startTime,
                windows,
                time,
                combo);

            return picked ?? overlapping[0];
        }

        private static int indexOf(IReadOnlyList<LaneTargetState> laneStates, LaneTargetState candidate)
        {
            for (int i = 0; i < laneStates.Count; i++)
            {
                if (ReferenceEquals(laneStates[i], candidate))
                    return i;
            }

            return -1;
        }
    }
}
