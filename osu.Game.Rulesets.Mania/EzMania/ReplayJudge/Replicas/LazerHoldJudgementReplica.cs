// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
        /// 对齐局内松手：仅 Meh+ 成功窗内落判；Miss 窗（Meh..Miss）与窗外一律 None，
        /// 由 Body 断连 / 重按重臂 / 被动 auto-miss 收束，避免提前松手直接 Miss 堵死后续合法松手。
        /// </summary>
        public HitResult EvaluateTail(double rawOffset, HitWindows hitWindows, bool headHit, bool holdBreak)
        {
            double timeOffsetForJudgement = rawOffset / TailNote.RELEASE_WINDOW_LENIENCE;
            var result = hitWindows.ResultFor(timeOffsetForJudgement);

            // ResultFor 在 Meh..Miss 之间会返回 Miss；用户松手路径不得据此终判尾，
            // 否则「断连后重按再松」无法重臂判尾（Eviternity 金标 Miss+4 主因）。
            if (result == HitResult.None || result == HitResult.Miss)
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
