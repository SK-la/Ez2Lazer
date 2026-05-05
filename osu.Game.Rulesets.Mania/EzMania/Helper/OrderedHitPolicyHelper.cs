// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.EzMania.Helper
{
    /// <summary>
    /// 用于处理 note lock 场景中判定优先级逻辑的辅助类。
    /// 提供不同的策略来决定哪个重叠的音符应该被判定。
    /// </summary>
    public class OrderedHitPolicyHelper
    {
        private readonly HitObjectContainer hitObjectContainer;
        private readonly Ez2ConfigManager ezConfig;

        public OrderedHitPolicyHelper(HitObjectContainer hitObjectContainer)
        {
            this.hitObjectContainer = hitObjectContainer;
            ezConfig = GlobalConfigStore.EzConfig;
        }

        /// <summary>
        /// 判断一个 <see cref="DrawableHitObject"/> 在某个时间点是否可以被击中，
        /// 考虑判定优先级设置。
        /// </summary>
        /// <param name="hitObject">要检查的 <see cref="DrawableHitObject"/>。</param>
        /// <param name="time">要检查的时间点。</param>
        /// <returns><paramref name="hitObject"/> 是否可以在给定的 <paramref name="time"/> 被击中。</returns>
        public bool IsHittableWithPrecedence(DrawableHitObject hitObject, double time)
        {
            var judgePrecedence = ezConfig.Get<EzEnumJudgePrecedence>(Ez2Setting.JudgePrecedence);

            if (isBMS())
            {
                var postJudged = getPostBadJudgedObjects(time).ToList();

                if (postJudged.Count > 0)
                {
                    var nearestPostJudged = postJudged
                                            .OrderBy(o => distanceToNonBadWindow(o, time))
                                            .ThenBy(o => o.HitObject.StartTime)
                                            .First();

                    double postJudgedDistance = distanceToNonBadWindow(nearestPostJudged, time);

                    double nearestUnjudgedDistance = getOverlappingObjects(time)
                                                     .Where(o => !o.Judged)
                                                     .Select(o => distanceToNonBadWindow(o, time))
                                                     .DefaultIfEmpty(double.PositiveInfinity)
                                                     .Min();

                    if (postJudgedDistance <= nearestUnjudgedDistance)
                        return hitObject == nearestPostJudged;
                }
            }

            // 获取所有与当前时间重叠的活跃对象
            var overlappingObjects = getOverlappingObjects(time).ToList();

            if (overlappingObjects.Count == 0)
                return true;

            // 应用优先级策略来确定哪个对象应该被击中
            var selectedObject = selectByPrecedence(overlappingObjects, time, judgePrecedence);
            return selectedObject == hitObject;
        }

        /// <summary>
        /// <see cref="EzEnumJudgePrecedence.Combo"/> 的固定比较定义。
        /// 当前候选在连击最低可保持窗口晚界内，且上一候选已经越过该窗口早界时返回 true。
        /// </summary>
        internal static bool CompareComboByPrecedence(double t1NoteTime, double t2NoteTime, double pressTime, ManiaHitWindows windows)
        {
            double comboEarly = windows.WindowFor(HitResult.Good, true);
            double comboLate = windows.WindowFor(HitResult.Good, false);
            return CompareComboByPrecedence(t1NoteTime, t2NoteTime, pressTime, comboEarly, comboLate);
        }

        internal static bool CompareComboByPrecedence(double t1NoteTime, double t2NoteTime, double pressTime, double comboEarly, double comboLate)
            => t1NoteTime < pressTime - comboEarly && t2NoteTime <= pressTime + comboLate;

        /// <summary>
        /// <see cref="EzEnumJudgePrecedence.Duration"/> 的固定比较定义。
        /// 当前候选更接近输入时间时返回 true。
        /// </summary>
        internal static bool CompareDurationByPrecedence(double t1NoteTime, double t2NoteTime, double pressTime)
            => Math.Abs(t1NoteTime - pressTime) > Math.Abs(t2NoteTime - pressTime);

        /// <summary>
        /// 获取所有判定窗口与给定时间重叠的活跃击打对象。
        /// </summary>
        /// <param name="time">检查重叠对象的时间点。</param>
        /// <returns>重叠的可绘制击打对象的可枚举集合。</returns>
        private IEnumerable<DrawableHitObject> getOverlappingObjects(double time)
        {
            foreach (var obj in hitObjectContainer.AliveObjects)
            {
                if (obj.Judged)
                    continue;

                // 检查对象的判定窗口是否与时间重叠
                var hitWindow = obj.HitObject.HitWindows;
                if (hitWindow == null || hitWindow.WindowFor(HitResult.Miss) == 0)
                    continue;

                double startTime = obj.HitObject.StartTime;
                double earlyWindow = hitWindow.WindowFor(HitResult.Miss);
                double lateWindow = hitWindow.WindowFor(HitResult.Miss);

                if (hitWindow is ManiaHitWindows maniaHitWindow)
                {
                    earlyWindow = maniaHitWindow.WindowFor(HitResult.Miss, true);
                    lateWindow = maniaHitWindow.WindowFor(HitResult.Miss, false);
                }

                // 检查时间是否落在此对象的判定窗口内
                if (time >= startTime - earlyWindow && time <= startTime + lateWindow)
                {
                    yield return obj;
                }
            }
        }

        private IEnumerable<DrawableHitObject> getPostBadJudgedObjects(double time)
        {
            foreach (var obj in hitObjectContainer.AliveObjects)
            {
                if (!obj.Judged || !isWithinMissWindow(obj, time))
                    continue;

                if (isPostBadKPoorRoutable(obj))
                    yield return obj;
            }
        }

        private static bool isPostBadKPoorRoutable(DrawableHitObject obj)
            => obj switch
            {
                BMSDrawableNote note => note.CanRouteToKPoor,
                BMSDrawableHoldNoteHead head => head.CanRouteToKPoor,
                BMSDrawableHoldNoteTail tail => tail.CanRouteToKPoor,
                _ => false
            };

        private static bool isWithinMissWindow(DrawableHitObject obj, double time)
        {
            var hitWindow = obj.HitObject.HitWindows;
            if (hitWindow == null || hitWindow.WindowFor(HitResult.Miss) == 0)
                return false;

            double startTime = obj.HitObject.StartTime;
            double earlyWindow = hitWindow.WindowFor(HitResult.Miss);
            double lateWindow = hitWindow.WindowFor(HitResult.Miss);

            if (hitWindow is ManiaHitWindows maniaHitWindow)
            {
                earlyWindow = maniaHitWindow.WindowFor(HitResult.Miss, true);
                lateWindow = maniaHitWindow.WindowFor(HitResult.Miss, false);
            }

            return time >= startTime - earlyWindow && time <= startTime + lateWindow;
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

        /// <summary>
        /// 根据优先级策略选择当前输入应命中的对象。
        /// BMS 模式走折叠比较；其它模式走通用优先级。
        /// </summary>
        /// <param name="candidates">候选击打对象列表。</param>
        /// <param name="time">当前时间（按键时间）。</param>
        /// <param name="precedence">要使用的优先级策略。</param>
        /// <returns>选中的击打对象，如果没有候选则返回 null。</returns>
        private DrawableHitObject? selectByPrecedence(IEnumerable<DrawableHitObject> candidates, double time, EzEnumJudgePrecedence precedence)
        {
            var candidateList = candidates.ToList();

            if (candidateList.Count == 0)
                return null;

            if (candidateList.Count == 1)
                return candidateList[0];

            // 全量候选统一决策，避免“链式替换”带来的顺序偏差。
            switch (precedence)
            {
                case EzEnumJudgePrecedence.Duration:
                    if (isBMS())
                    {
                        var orderedD = candidateList.OrderBy(c => c.HitObject.StartTime).ToList();
                        var pickedD = SelectFoldDrawable(orderedD, time, comboAlgorithm: false);
                        return pickedD ?? orderedD[0];
                    }

                    return candidateList
                           .OrderBy(c => Math.Abs(c.HitObject.StartTime - time))
                           .ThenBy(c => c.HitObject.StartTime)
                           .First();

                case EzEnumJudgePrecedence.Combo:
                    if (isBMS())
                    {
                        var orderedC = candidateList.OrderBy(c => c.HitObject.StartTime).ToList();
                        var pickedC = SelectFoldDrawable(orderedC, time, comboAlgorithm: true);
                        return pickedC ?? orderedC[0];
                    }

                    var orderedCombo = candidateList.OrderBy(c => c.HitObject.StartTime).ToList();
                    var selectedCombo = orderedCombo[0];

                    for (int i = 1; i < orderedCombo.Count; i++)
                    {
                        var candidate = orderedCombo[i];
                        if (compareComboByPrecedence(selectedCombo, candidate, time))
                            selectedCombo = candidate;
                    }

                    return selectedCombo;

                case EzEnumJudgePrecedence.Earliest:
                default:
                    return candidateList.OrderBy(c => c.HitObject.StartTime).First();
            }
        }

        private bool isBMS()
        {
            var mode = ezConfig.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            return HitModeHelper.IsBMSHitMode(mode);
        }

        internal static DrawableHitObject? SelectFoldDrawable(IReadOnlyList<DrawableHitObject> sortedByStartTime, double pressTime, bool comboAlgorithm)
        {
            return SelectFold(
                sortedByStartTime,
                d => d.Judged,
                d => d.HitObject.StartTime,
                d => d.HitObject.HitWindows as ManiaHitWindows,
                pressTime,
                comboAlgorithm);
        }

        internal static T? SelectFold<T>(
            IReadOnlyList<T> sortedCandidates,
            Func<T, bool> isJudged,
            Func<T, double> noteTime,
            Func<T, ManiaHitWindows?> windows,
            double pressTime,
            bool comboAlgorithm) where T : class
        {
            T? tNote = null;

            foreach (var judgeNote in sortedCandidates)
            {
                if (isJudged(judgeNote))
                    continue;

                var w = windows(judgeNote);
                if (w == null)
                    continue;

                double t2 = noteTime(judgeNote);
                bool enterOuter = tNote == null
                                  || isJudged(tNote)
                                  || (comboAlgorithm
                                      ? CompareComboByPrecedence(noteTime(tNote), t2, pressTime, w)
                                      : CompareDurationByPrecedence(noteTime(tNote), t2, pressTime));

                if (!enterOuter)
                    continue;

                double offset = pressTime - t2;
                int newRank = JudgementRankForRouting(w.ResultFor(offset));

                if (newRank == int.MaxValue)
                {
                    tNote = null;
                    continue;
                }

                if (tNote == null)
                {
                    tNote = judgeNote;
                    continue;
                }

                var tw = windows(tNote);

                if (tw == null)
                {
                    tNote = judgeNote;
                    continue;
                }

                double t1 = noteTime(tNote);
                int oldRank = JudgementRankForRouting(tw.ResultFor(pressTime - t1));

                if (oldRank == int.MaxValue)
                {
                    tNote = judgeNote;
                    continue;
                }

                if (newRank < oldRank || (newRank == oldRank && Math.Abs(t2 - pressTime) < Math.Abs(t1 - pressTime)))
                    tNote = judgeNote;
            }

            return tNote;
        }

        internal static int JudgementRankForRouting(HitResult result)
        {
            if (result == HitResult.None)
                return int.MaxValue;

            int i = result.GetIndexForOrderedDisplay();
            return i < 0 ? int.MaxValue : i;
        }

        private static bool compareComboByPrecedence(DrawableHitObject t1, DrawableHitObject t2, double pressTime)
        {
            var windows = t2.HitObject.HitWindows;
            if (windows == null)
                return false;

            double comboEarly = windows.WindowFor(HitResult.Good);
            double comboLate = windows.WindowFor(HitResult.Good);

            if (windows is ManiaHitWindows maniaWindows)
            {
                comboEarly = maniaWindows.WindowFor(HitResult.Good, true);
                comboLate = maniaWindows.WindowFor(HitResult.Good, false);
            }

            return CompareComboByPrecedence(
                t1.HitObject.StartTime,
                t2.HitObject.StartTime,
                pressTime,
                comboEarly,
                comboLate);
        }
    }
}
