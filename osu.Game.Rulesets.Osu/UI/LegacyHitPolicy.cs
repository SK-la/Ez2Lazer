// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Ensures that <see cref="HitObject"/>s are hit in order of appearance. The classic note lock.
    /// <remarks>
    /// Hits will be blocked until the previous <see cref="HitObject"/>s have been judged.
    /// </remarks>
    /// </summary>
    public class LegacyHitPolicy : IHitPolicy
    {
        public IHitObjectContainer? HitObjectContainer { get; set; }

        /// <summary>
        /// 内核驱动时的顺序/叠堆查询；非 <see langword="null"/> 时本策略退化为薄查询层。
        /// </summary>
        public Func<HitObject, double, ClickAction>? KernelQuery { get; set; }

        private readonly double hittableRange;
        private readonly List<OsuNoteLockEntry> precedingObjects = new List<OsuNoteLockEntry>();

        public LegacyHitPolicy(double hittableRange = OsuHitWindows.MISS_WINDOW)
        {
            this.hittableRange = hittableRange;
        }

        public void HandleHit(DrawableHitObject hitObject)
        {
        }

        public virtual ClickAction CheckHittable(DrawableHitObject hitObject, double time, HitResult result)
        {
            if (KernelQuery != null)
            {
                ClickAction kernelAction = KernelQuery(hitObject.HitObject, time);

                if (kernelAction != ClickAction.Hit)
                    return kernelAction;

                return result == HitResult.None ? ClickAction.Shake : ClickAction.Hit;
            }

            if (HitObjectContainer == null)
                throw new InvalidOperationException($"{nameof(HitObjectContainer)} should be set before {nameof(CheckHittable)} is called.");

            // 顺序即容器顺序（AliveObjects 按 StartTime 排列，同刻保持加入序）。
            var aliveObjects = HitObjectContainer.AliveObjects.ToList();
            int index = aliveObjects.IndexOf(hitObject);

            // 嵌套物件（滑条头）不在容器列表里：此时没有「紧邻前一个」，且原实现会扫完整张存活列表。
            // 目标之后的物件 EndTime 必然不早于目标起始时刻，纳入扫描不影响结论。
            int precedingCount = index < 0 ? aliveObjects.Count : index;

            precedingObjects.Clear();

            for (int i = 0; i < precedingCount; i++)
                precedingObjects.Add(toEntry(aliveObjects[i]));

            // 「紧邻叠堆」先于「本次结果本身不可命中」：与判定结果无关的输入拦截。
            ClickAction stackAction = StableClassicNoteLock.CheckPrevious(index > 0 ? toEntry(aliveObjects[index - 1]) : null);

            if (stackAction != ClickAction.Hit)
                return stackAction;

            if (result == HitResult.None)
                return ClickAction.Shake;

            return StableClassicNoteLock.CheckOrder(precedingObjects, hitObject.HitObject.StartTime, time, hittableRange);
        }

        private static OsuNoteLockEntry toEntry(DrawableHitObject hitObject)
            => new OsuNoteLockEntry(
                hitObject.HitObject.StartTime,
                hitObject.HitObject.GetEndTime(),
                hitObject.Judged,
                hitObject.AllJudged,
                hitObject.HitObject is OsuHitObject osuHitObject ? osuHitObject.StackHeight : 0,
                // 本策略只做叠堆与顺序判定，不使用可阻挡标记。
                canBlock: false);
    }
}
