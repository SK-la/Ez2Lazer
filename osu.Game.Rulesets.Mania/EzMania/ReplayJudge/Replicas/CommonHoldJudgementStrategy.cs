// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas
{
    /// <summary>
    /// Lazer/Classic hold 判定 replica；Ez HitMode 已迁至 <see cref="Mappings"/>。
    /// </summary>
    public sealed class CommonHoldJudgementStrategy : IManiaHoldJudgementStrategy
    {
        public static CommonHoldJudgementStrategy Instance { get; } = new CommonHoldJudgementStrategy();

        public HitResult EvaluateTail(in HoldTailEvaluationContext context)
            => LazerHoldJudgementReplica.Instance.EvaluateTail(context.RawOffset, context.HitWindows, context.HeadHit, context.HoldBreak || context.HoldBroken);

        public bool CanBeginHoldAt(double time, TailNote tail) => LazerHoldJudgementReplica.Instance.CanBeginHoldAt(time, tail);

        public bool IsHoldBreak(double rawOffset, HitWindows hitWindows) => LazerHoldJudgementReplica.Instance.IsHoldBreak(rawOffset, hitWindows);
    }
}
