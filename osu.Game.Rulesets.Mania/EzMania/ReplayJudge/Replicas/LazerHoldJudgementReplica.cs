// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
//
// Replica of DrawableHoldNote / DrawableHoldNoteTail release judging — sync when merging ppy/osu master.

using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas
{
    /// <summary>
    /// Ez 侧 Lazer hold 判定复刻，供 <see cref="ManiaReplaySession"/> 使用。
    /// </summary>
    public sealed class LazerHoldJudgementReplica
    {
        public static LazerHoldJudgementReplica Instance { get; } = new LazerHoldJudgementReplica();

        /// <summary>
        /// 对齐 <c>ManiaScoreHitEventGenerator.evaluateLazerResult</c> 的 tail 分支。
        /// </summary>
        public HitResult EvaluateTail(double rawOffset, HitWindows hitWindows, bool headHit, bool holdBreak)
        {
            double timeOffsetForJudgement = rawOffset / TailNote.RELEASE_WINDOW_LENIENCE;
            var result = hitWindows.ResultFor(timeOffsetForJudgement);

            if (result == HitResult.None)
                return HitResult.None;

            if (result > HitResult.Meh && (!headHit || holdBreak))
                return HitResult.Meh;

            return result;
        }

        /// <summary>
        /// 对齐 <see cref="Objects.Drawables.DrawableHoldNote.OnPressed"/> 中不得晚于 tail 窗口开始 hold 的约束。
        /// </summary>
        public bool CanBeginHoldAt(double time, TailNote tail)
        {
            if (tail.HitWindows == null || ReferenceEquals(tail.HitWindows, HitWindows.Empty))
                return true;

            return time <= tail.StartTime || tail.HitWindows.CanBeHit(time - tail.StartTime);
        }

        public bool IsHoldBreak(double rawOffset, HitWindows hitWindows)
        {
            double missWindow = hitWindows.WindowFor(HitResult.Miss) * TailNote.RELEASE_WINDOW_LENIENCE;
            return rawOffset < -missWindow;
        }
    }
}
