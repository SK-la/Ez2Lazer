// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects;
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

            // 获取所有与当前时间重叠的活跃对象
            var overlappingObjects = getOverlappingObjects(time).ToList();

            if (overlappingObjects.Count == 0)
                return true;

            // 应用优先级策略来确定哪个对象应该被击中
            var selectedObject = selectByPrecedence(overlappingObjects, time, judgePrecedence);
            return selectedObject == hitObject;
        }

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
                double endTime = obj.HitObject.GetEndTime();
                double earlyWindow = hitWindow.WindowFor(HitResult.Miss);
                double lateWindow = hitWindow.WindowFor(HitResult.Miss);

                if (hitWindow is ManiaHitWindows maniaHitWindow)
                {
                    earlyWindow = maniaHitWindow.WindowFor(HitResult.Miss, true);
                    lateWindow = maniaHitWindow.WindowFor(HitResult.Miss, false);
                }

                // 检查时间是否落在此对象的判定窗口内
                if (time >= startTime - earlyWindow && time <= endTime + lateWindow)
                {
                    yield return obj;
                }
            }
        }

        /// <summary>
        /// 根据优先级策略选择可击中的对象。
        /// 实现 beatoraja 的 JudgeAlgorithm 逻辑来处理 note lock 场景。
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

            // 按开始时间排序（最早的在前）以模拟 t1、t2 比较
            var sortedCandidates = candidateList.OrderBy(obj => obj.HitObject.StartTime).ToList();

            // 从第一个候选作为选中对象开始
            DrawableHitObject selected = sortedCandidates[0];

            // 将每个后续候选与当前选择进行比较
            for (int i = 1; i < sortedCandidates.Count; i++)
            {
                DrawableHitObject candidate = sortedCandidates[i];

                bool shouldReplace;

                switch (precedence)
                {
                    case EzEnumJudgePrecedence.Combo:
                        // Combo 算法：如果选中对象超出 GOOD 窗口但候选在窗口内，则优先选择候选
                        shouldReplace = compareCombo(selected, candidate, time);
                        break;

                    case EzEnumJudgePrecedence.Duration:
                        // Duration 算法：如果候选离按键时间更近，则优先选择
                        shouldReplace = compareDuration(selected, candidate, time);
                        break;

                    case EzEnumJudgePrecedence.Earliest:
                        shouldReplace = false;
                        break;

                    default:
                        shouldReplace = false;
                        break;
                }

                if (shouldReplace)
                {
                    selected = candidate;
                }
            }

            return selected;
        }

        /// <summary>
        /// Combo 算法比较（beatoraja 风格）。
        /// 如果 t2 应该替换 t1 则返回 true。
        /// 逻辑：t1 超出 GOOD 窗口（太晚），但 t2 仍在 GOOD 窗口内。
        /// </summary>
        private bool compareCombo(DrawableHitObject t1, DrawableHitObject t2, double ptime)
        {
            var t1Windows = t1.HitObject.HitWindows;
            var t2Windows = t2.HitObject.HitWindows;
            if (t1Windows == null || t2Windows == null)
                return false;

            double t1GoodLate = t1Windows.WindowFor(HitResult.Good);
            double t2GoodEarly = t2Windows.WindowFor(HitResult.Good);

            if (t1Windows is ManiaHitWindows t1ManiaWindows)
                t1GoodLate = t1ManiaWindows.WindowFor(HitResult.Good, false);

            if (t2Windows is ManiaHitWindows t2ManiaWindows)
                t2GoodEarly = t2ManiaWindows.WindowFor(HitResult.Good, true);

            // 对齐 beatoraja:
            // t1.getMicroTime() < ptime + judgetable[2][0] (GOOD 的 late 下界，通常为负值)
            // 等价于：t1Start < ptime - goodLate
            bool t1BeyondGoodLate = t1.HitObject.StartTime < ptime - t1GoodLate;

            // t2.getMicroTime() <= ptime + judgetable[2][1] (GOOD 的 early 上界)
            bool t2WithinGoodEarly = t2.HitObject.StartTime <= ptime + t2GoodEarly;

            // 如果 t1 已经晚过 GOOD，而 t2 仍在 GOOD 的 early 窗内，则切到 t2。
            return t1BeyondGoodLate && t2WithinGoodEarly;
        }

        /// <summary>
        /// Duration 算法比较（beatoraja 风格）。
        /// 如果 t2 应该替换 t1 则返回 true。
        /// 逻辑：|t1.time - ptime| > |t2.time - ptime|（t2 离按键时间更近）
        /// </summary>
        private bool compareDuration(DrawableHitObject t1, DrawableHitObject t2, double ptime)
        {
            double timeDiff1 = Math.Abs(t1.HitObject.StartTime - ptime);
            double timeDiff2 = Math.Abs(t2.HitObject.StartTime - ptime);

            // 如果 t2 比 t1 离按键时间更近，则替换
            return timeDiff1 > timeDiff2;
        }
    }
}
