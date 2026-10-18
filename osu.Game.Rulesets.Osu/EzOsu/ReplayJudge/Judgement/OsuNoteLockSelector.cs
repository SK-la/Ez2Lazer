// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Osu.UI;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement
{
    /// <summary>
    /// 单个物件的 note lock 视图。Drawable（<c>DrawableOsuHitObject</c>）与无头 Session 各自适配后
    /// 共用 <see cref="OsuNoteLockSelector"/> 的同一套判定。
    /// </summary>
    public readonly struct OsuNoteLockEntry
    {
        public OsuNoteLockEntry(double startTime, double endTime, bool judged, bool allJudged, int stackHeight, bool canBlock)
        {
            StartTime = startTime;
            EndTime = endTime;
            Judged = judged;
            AllJudged = allJudged;
            StackHeight = stackHeight;
            CanBlock = canBlock;
        }

        public double StartTime { get; }

        public double EndTime { get; }

        /// <summary>本物件自身是否已判定（不含嵌套）。</summary>
        public bool Judged { get; }

        /// <summary>本物件及其嵌套是否全部已判定。</summary>
        public bool AllJudged { get; }

        public int StackHeight { get; }

        /// <summary>是否可阻挡后续物件的输入（对应 <c>DrawableHitCircle</c>，含滑条头）。</summary>
        public bool CanBlock { get; }
    }

    /// <summary>
    /// lazer 默认 note lock 求解（与 Drawable 解耦）：把 <c>StartTimeOrderedHitPolicy</c> 中
    /// 「该输入此刻能否命中该物件」抽成纯函数，供无头 Session 与 Drawable 共用同一份语义。
    /// </summary>
    /// <remarks>
    /// classic 侧（<c>LegacyHitPolicy</c>）的判定在 <see cref="StableClassic.StableClassicNoteLock"/>；
    /// 「本次结果本身为 <c>HitResult.None</c>」的分支由调用方先行判定（那属于判窗而非 note lock），
    /// 故此处只表达顺序/可达性约束。
    /// </remarks>
    public static class OsuNoteLockSelector
    {
        /// <summary>
        /// lazer 默认 <c>StartTimeOrderedHitPolicy</c>：target 之前最后一个可阻挡圆尚未判定、
        /// 且当前时刻早于该圆的起始时刻时，输入被挡下。
        /// </summary>
        /// <param name="objectsUpToTarget">target 之前（含并列起始时刻）的存活物件，按容器顺序；只有 <see cref="OsuNoteLockEntry.CanBlock"/> 参与判定。</param>
        /// <param name="targetStartTime">目标物件的起始时刻。</param>
        /// <param name="time">本次输入时刻。</param>
        public static ClickAction CheckTimeOrdered(IReadOnlyList<OsuNoteLockEntry> objectsUpToTarget, double targetStartTime, double time)
        {
            OsuNoteLockEntry? blockingObject = null;

            for (int i = 0; i < objectsUpToTarget.Count; i++)
            {
                var entry = objectsUpToTarget[i];

                if (entry.StartTime >= targetStartTime)
                    break;

                if (entry.CanBlock)
                    blockingObject = entry;
            }

            // 完全同刻的输入仍放行（地图存在同时物件）。
            if (blockingObject is { } blocker && !blocker.Judged && time < blocker.StartTime)
                return ClickAction.Shake;

            return ClickAction.Hit;
        }
    }
}
