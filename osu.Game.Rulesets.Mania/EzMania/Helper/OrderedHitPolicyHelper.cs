// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Configuration;
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
                double window = hitWindow.WindowFor(HitResult.Miss);

                // 检查时间是否落在此对象的判定窗口内
                if (time >= startTime - window && time <= endTime + window)
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
            var hitWindows = t1.HitObject.HitWindows;
            if (hitWindows == null)
                return false;

            // 获取 GOOD 窗口（beatoraja 中的 judgetable[2]）
            // 在 osu! 中，我们使用 HitResult.Good 作为参考
            double goodWindow = hitWindows.WindowFor(HitResult.Good);

            // 检查 t1 是否超出 GOOD 窗口（太早或太晚无法获得 Good 或更好的判定）
            // beatoraja: t1.getMicroTime() < ptime + judgetable[2][0]
            // 这意味着 t1 早于 (ptime + goodWindow.Late)，即太早/太晚
            bool t1BeyondGood = t1.HitObject.StartTime < ptime - goodWindow;

            // 检查 t2 是否在 GOOD 窗口内
            // beatoraja: t2.getMicroTime() <= ptime + judgetable[2][1]
            bool t2InGood = Math.Abs(t2.HitObject.StartTime - ptime) <= goodWindow;

            // 如果 t1 超出 GOOD 但 t2 仍在 GOOD 内，则用 t2 替换 t1
            return t1BeyondGood && t2InGood;
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
