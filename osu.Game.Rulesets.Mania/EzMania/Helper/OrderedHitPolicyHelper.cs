// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects.Drawables;
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
        private readonly ManiaLaneController? laneController;
        private readonly Ez2ConfigManager ezConfig;
        private readonly Bindable<EzEnumHitMode> hitMode;
        private readonly Bindable<bool> judgmentDiagEnabled;

        // 每判定复用同一批缓冲：候选与后判对象只在本次调用内有效，不去分配新的 List。
        private readonly List<PrecedenceCandidate> candidateBuffer = new List<PrecedenceCandidate>();
        private readonly List<DrawableHitObject> postJudgedBuffer = new List<DrawableHitObject>();
        private readonly List<PrecedenceCandidate> sortedBuffer = new List<PrecedenceCandidate>();

        private const string log_prefix = "[JudgeDiag][PolicyHelper]";

        public OrderedHitPolicyHelper(HitObjectContainer hitObjectContainer, ManiaLaneController? laneController = null)
        {
            this.hitObjectContainer = hitObjectContainer;
            this.laneController = laneController;
            ezConfig = GlobalConfigStore.EzConfig;

            // 缓存 bindable：命中模式与诊断开关在一次按键里要被读好几次，没必要每次都走配置查询。
            hitMode = ezConfig.GetBindable<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            judgmentDiagEnabled = ezConfig.GetBindable<bool>(Ez2Setting.EzJudgmentDiagEnabled);
        }

        private bool JudgmentDiagEnabled => judgmentDiagEnabled.Value;

        public bool IsHittableWithPrecedence(DrawableHitObject hitObject, double time, EzEnumJudgePrecedence? precedenceOverride = null)
        {
            var judgePrecedence = precedenceOverride ?? ezConfig.Get<EzEnumJudgePrecedence>(Ez2Setting.JudgePrecedence);
            bool isBmsMode = isBMS();

            if (laneController != null && hitObject is not (DrawableHoldNoteTail or DrawableHoldNote))
                return laneController.IsHittable(hitObject, time, judgePrecedence);

            collectOverlappingCandidates(time, candidateBuffer);

            if (isBmsMode)
            {
                collectPostBadJudgedObjects(time, postJudgedBuffer);

                if (postJudgedBuffer.Count > 0)
                {
                    // 等价于 OrderBy(distance).ThenBy(StartTime).First()：按 (距离, 开始时间) 取最小，并列时保留先出现的。
                    DrawableHitObject nearestPostJudged = postJudgedBuffer[0];
                    double nearestPostJudgedDistance = distanceToNonBadWindow(nearestPostJudged, time);

                    for (int i = 1; i < postJudgedBuffer.Count; i++)
                    {
                        var candidate = postJudgedBuffer[i];
                        double distance = distanceToNonBadWindow(candidate, time);

                        if (distance < nearestPostJudgedDistance
                            || (distance == nearestPostJudgedDistance && candidate.HitObject.StartTime < nearestPostJudged.HitObject.StartTime))
                        {
                            nearestPostJudged = candidate;
                            nearestPostJudgedDistance = distance;
                        }
                    }

                    double nearestUnjudgedDistance = double.PositiveInfinity;

                    for (int i = 0; i < candidateBuffer.Count; i++)
                    {
                        var candidate = candidateBuffer[i];

                        if (candidate.IsJudged)
                            continue;

                        nearestUnjudgedDistance = Math.Min(nearestUnjudgedDistance, distanceToNonBadWindow(candidate, time));
                    }

                    if (nearestPostJudgedDistance <= nearestUnjudgedDistance)
                        return hitObject == nearestPostJudged;
                }
            }

            // 获取所有与当前时间重叠的活跃路由候选。
            if (candidateBuffer.Count == 0)
            {
                if (JudgmentDiagEnabled)
                    logDiag($"t={time:F3} no-overlap target={describe(hitObject)}");

                return true;
            }

            // 应用优先级策略来确定哪个对象应该被击中
            var selected = selectByPrecedence(candidateBuffer, time, judgePrecedence, allowFallbackToEarliest: isBmsMode);

            if (JudgmentDiagEnabled)
            {
                logDiag(
                    $"t={time:F3} mode={(isBmsMode ? "bms" : "non-bms")} precedence={judgePrecedence} target={describe(hitObject)} " +
                    $"overlap=[{string.Join(", ", candidateBuffer.Select(describe))}] selected={describe(selected)}");
            }

            return selected?.RoutedObject == hitObject;
        }

        /// <summary>
        /// <see cref="EzEnumJudgePrecedence.Combo"/> 的固定比较定义。
        /// 当前候选在连击最低可保持窗口晚界内，且上一候选已经越过该窗口早界时返回 true。
        /// </summary>
        public static bool CompareComboByPrecedence(double t1NoteTime, double t2NoteTime, double pressTime, ManiaHitWindows windows)
        {
            double comboEarly = windows.WindowFor(HitResult.Good, true);
            double comboLate = windows.WindowFor(HitResult.Good, false);
            return CompareComboByPrecedence(t1NoteTime, t2NoteTime, pressTime, comboEarly, comboLate);
        }

        public static bool CompareComboByPrecedence(double t1NoteTime, double t2NoteTime, double pressTime, double comboEarly, double comboLate)
            => t1NoteTime < pressTime - comboEarly && t2NoteTime <= pressTime + comboLate;

        /// <summary>
        /// <see cref="EzEnumJudgePrecedence.Duration"/> 的固定比较定义。
        /// 当前候选更接近输入时间时返回 true。
        /// </summary>
        public static bool CompareDurationByPrecedence(double t1NoteTime, double t2NoteTime, double pressTime)
            => Math.Abs(t1NoteTime - pressTime) > Math.Abs(t2NoteTime - pressTime);

        /// <summary>
        /// 收集所有判定窗口与给定时间重叠的活跃击打对象，写入调用方提供的缓冲（不清空）。
        /// </summary>
        /// <param name="time">检查重叠对象的时间点。</param>
        /// <param name="buffer">接收候选的缓冲，调用前应已清空。</param>
        private void collectOverlappingCandidates(double time, List<PrecedenceCandidate> buffer)
        {
            buffer.Clear();

            foreach (var obj in hitObjectContainer.AliveObjects)
            {
                if (!tryCreatePressCandidate(obj, out var candidate))
                    continue;

                double earlyWindow = candidate.Windows.WindowFor(HitResult.Miss, true);
                double lateWindow = candidate.Windows.WindowFor(HitResult.Miss, false);

                // 检查时间是否落在此对象的判定窗口内
                if (time >= candidate.StartTime - earlyWindow && time <= candidate.StartTime + lateWindow)
                    buffer.Add(candidate);
            }
        }

        private static bool tryCreatePressCandidate(DrawableHitObject obj, out PrecedenceCandidate candidate)
        {
            candidate = null!;

            // Nested LN head/tail are judged through DrawableHoldNote.OnPressed().
            // The routable object must therefore be the parent hold note, while timing/windows come from the head.
            if (obj is DrawableHoldNoteHead or DrawableHoldNoteTail)
                return false;

            if (obj is DrawableHoldNote hold)
            {
                if (hold.Judged || hold.Head.Judged)
                    return false;

                if (hold.Head.HitObject.HitWindows is not ManiaHitWindows headWindows || headWindows.WindowFor(HitResult.Miss) == 0)
                    return false;

                candidate = new PrecedenceCandidate(hold, hold.Head, hold.Head.HitObject.StartTime, headWindows);
                return true;
            }

            if (obj.Judged)
                return false;

            if (obj.HitObject.HitWindows is not ManiaHitWindows windows || windows.WindowFor(HitResult.Miss) == 0)
                return false;

            candidate = new PrecedenceCandidate(obj, obj, obj.HitObject.StartTime, windows);
            return true;
        }

        private void collectPostBadJudgedObjects(double time, List<DrawableHitObject> buffer)
        {
            buffer.Clear();

            foreach (var obj in hitObjectContainer.AliveObjects)
            {
                if (!obj.Judged || !isWithinMissWindow(obj, time))
                    continue;

                if (isPostBadKPoorRoutable(obj))
                    buffer.Add(obj);
            }
        }

        private static bool isPostBadKPoorRoutable(DrawableHitObject obj)
        {
            if (obj is DrawableHoldNoteTail tail)
                return tail.CanRouteToKPoor;

            if (obj is DrawableNote note)
                return note.CanRouteToKPoor;

            return false;
        }

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

        private static double distanceToNonBadWindow(PrecedenceCandidate candidate, double pressTime)
        {
            double early = candidate.Windows.WindowFor(HitResult.Good, true);
            double late = candidate.Windows.WindowFor(HitResult.Good, false);

            double start = candidate.StartTime - early;
            double end = candidate.StartTime + late;

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
        /// <param name="candidateList">候选击打对象列表（不会被修改）。</param>
        /// <param name="time">当前时间（按键时间）。</param>
        /// <param name="precedence">要使用的优先级策略。</param>
        /// <param name="allowFallbackToEarliest">没有候选能产生常规判定时，是否回退到最早候选。</param>
        /// <returns>选中的击打对象，如果没有候选则返回 null。</returns>
        private PrecedenceCandidate? selectByPrecedence(IReadOnlyList<PrecedenceCandidate> candidateList, double time, EzEnumJudgePrecedence precedence, bool allowFallbackToEarliest)
        {
            if (candidateList.Count == 0)
                return null;

            if (candidateList.Count == 1)
                return candidateList[0];

            // 全量候选统一决策，避免“链式替换”带来的顺序偏差。
            switch (precedence)
            {
                case EzEnumJudgePrecedence.Duration:
                    var orderedD = sortByStartTime(candidateList);
                    var pickedD = selectFoldCandidate(orderedD, time, comboAlgorithm: false);
                    return pickedD ?? (allowFallbackToEarliest ? orderedD[0] : null);

                case EzEnumJudgePrecedence.Combo:
                    var orderedC = sortByStartTime(candidateList);
                    var pickedC = selectFoldCandidate(orderedC, time, comboAlgorithm: true);
                    return pickedC ?? (allowFallbackToEarliest ? orderedC[0] : null);

                case EzEnumJudgePrecedence.Earliest:
                default:
                    // 等价于 OrderBy(StartTime).First()：并列时取先出现的。
                    var earliest = candidateList[0];

                    for (int i = 1; i < candidateList.Count; i++)
                    {
                        if (candidateList[i].StartTime < earliest.StartTime)
                            earliest = candidateList[i];
                    }

                    return earliest;
            }
        }

        /// <summary>
        /// 按开始时间稳定排序到内部复用缓冲，等价于 <c>OrderBy(c => c.StartTime).ToList()</c> 但不分配。
        /// </summary>
        /// <remarks>返回的列表在下一次调用本方法时被覆写，只可在当前选择流程内使用。</remarks>
        private IReadOnlyList<PrecedenceCandidate> sortByStartTime(IReadOnlyList<PrecedenceCandidate> source)
        {
            sortedBuffer.Clear();

            for (int i = 0; i < source.Count; i++)
            {
                var item = source[i];
                int j = sortedBuffer.Count;

                // 严格大于才后移，保证与 OrderBy 相同的稳定性（等值保持原有先后）。
                while (j > 0 && sortedBuffer[j - 1].StartTime > item.StartTime)
                {
                    if (j == sortedBuffer.Count)
                        sortedBuffer.Add(sortedBuffer[j - 1]);
                    else
                        sortedBuffer[j] = sortedBuffer[j - 1];

                    j--;
                }

                if (j == sortedBuffer.Count)
                    sortedBuffer.Add(item);
                else
                    sortedBuffer[j] = item;
            }

            return sortedBuffer;
        }

        private bool isBMS()
            => HitModeHelper.IsBMSHitMode(hitMode.Value);

        public static DrawableHitObject? SelectFoldDrawable(IReadOnlyList<DrawableHitObject> sortedByStartTime, double pressTime, bool comboAlgorithm)
        {
            return SelectFold(
                sortedByStartTime,
                d => d.Judged,
                d => d.HitObject.StartTime,
                d => d.HitObject.HitWindows as ManiaHitWindows,
                pressTime,
                comboAlgorithm);
        }

        private static PrecedenceCandidate? selectFoldCandidate(IReadOnlyList<PrecedenceCandidate> sortedByStartTime, double pressTime, bool comboAlgorithm)
        {
            return SelectFold(
                sortedByStartTime,
                c => c.IsJudged,
                c => c.StartTime,
                c => c.Windows,
                pressTime,
                comboAlgorithm);
        }

        public static T? SelectFold<T>(
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

        public static int JudgementRankForRouting(HitResult result)
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

        public static bool IsUserTriggerJudgeableNow(DrawableHitObject obj, double time)
        {
            if (obj.HitObject.HitWindows == null)
                return false;

            // Keep consistent with DrawableHoldNote.OnPressed guard:
            // start is judged on head timing and cannot start in tail late-lenience-only region.
            if (obj is DrawableHoldNote hold)
            {
                // 检查头部是否可被击中
                if (!hold.Head.HitObject.HitWindows.CanBeHit(time - hold.Head.HitObject.StartTime))
                    return false;

                // 只有当时间在尾部开始时间之后，且尾部有判定窗口时才检查尾部
                // 但如果头部已经被击中（玩家正在按住），则不应该因为尾部窗口而返回 false
                // 因为尾部判定是自动的（基于释放或保持状态），不是用户主动触发的
                if (time > hold.Tail.HitObject.StartTime)
                {
                    // 如果头部还未被击中，需要检查尾部窗口（防止在尾部晚期宽容区开始按住）
                    if (!hold.Head.IsHit && !hold.Tail.HitObject.HitWindows.CanBeHit(time - hold.Tail.HitObject.StartTime))
                        return false;

                    // 如果头部已被击中，说明玩家正在按住LN，此时不应因为尾部窗口而拒绝
                    // 尾部判定会在 CheckForResult 中自动处理
                }

                return true;
            }

            return obj.HitObject.HitWindows.CanBeHit(time - obj.HitObject.StartTime);
        }

        private void logDiag(string message)
        {
            if (!ezConfig.Get<bool>(Ez2Setting.EzJudgmentDiagEnabled))
                return;

            Logger.Log($"{log_prefix} {message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
        }

        private static string describe(DrawableHitObject? obj)
        {
            if (obj == null)
                return "null";

            return $"{obj.GetType().Name}@{obj.HitObject.StartTime:F3} judged={obj.Judged}";
        }

        private static string describe(PrecedenceCandidate? candidate)
        {
            if (candidate == null)
                return "null";

            return $"route={describe(candidate.RoutedObject)} judge={describe(candidate.JudgementObject)}";
        }

        private sealed class PrecedenceCandidate
        {
            public readonly DrawableHitObject RoutedObject;
            public readonly DrawableHitObject JudgementObject;
            public readonly double StartTime;
            public readonly ManiaHitWindows Windows;

            public PrecedenceCandidate(DrawableHitObject routedObject, DrawableHitObject judgementObject, double startTime, ManiaHitWindows windows)
            {
                RoutedObject = routedObject;
                JudgementObject = judgementObject;
                StartTime = startTime;
                Windows = windows;
            }

            public bool IsJudged => RoutedObject.Judged || JudgementObject.Judged;
        }
    }
}
