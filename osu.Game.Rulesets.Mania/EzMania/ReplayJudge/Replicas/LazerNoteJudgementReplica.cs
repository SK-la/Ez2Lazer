// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
//
// Replica of Objects.Drawables.DrawableNote.CheckForResult — sync when merging ppy/osu master.

using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas
{
    /// <summary>
    /// Ez 侧 Lazer tap 判定复刻，供 <see cref="ManiaReplaySession"/> 使用。
    /// 官方 Drawable 路径不得引用此类。
    /// </summary>
    public sealed class LazerNoteJudgementReplica : IManiaNoteJudgementStrategy
    {
        public static LazerNoteJudgementReplica Instance { get; } = new LazerNoteJudgementReplica();

        public ManiaNoteJudgementOutcome EvaluateAutoMiss(double timeOffset, HitWindows hitWindows)
        {
            if (!hitWindows.CanBeHit(timeOffset))
                return ManiaNoteJudgementOutcome.ApplyResult(HitResult.Miss);

            return ManiaNoteJudgementOutcome.Ignore;
        }

        public ManiaNoteJudgementOutcome EvaluatePress(double timeOffset, HitWindows hitWindows)
        {
            var result = hitWindows.ResultFor(timeOffset);

            if (result == HitResult.None)
                return ManiaNoteJudgementOutcome.Ignore;

            return ManiaNoteJudgementOutcome.ApplyResult(result);
        }

        public HitResult RejudgeHitEvent(HitEvent hitEvent, HitWindows hitWindows)
        {
            // TailNote 保留原始结果（尾判依赖 hold 上下文，Lazer 尾判有 RELEASE_WINDOW_LENIENCE/cap 逻辑）
            if (hitEvent.HitObject is TailNote)
                return hitEvent.Result;

            var outcome = EvaluatePress(hitEvent.TimeOffset, hitWindows);
            return outcome.Kind == ManiaNoteJudgementOutcomeKind.Apply ? outcome.Result : HitResult.Miss;
        }
    }
}
