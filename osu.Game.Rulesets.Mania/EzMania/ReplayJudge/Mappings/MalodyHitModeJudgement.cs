// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    public enum MalodyJudge
    {
        None,
        Best,
        Cool,
        Good,
        Miss,
    }

    public sealed class MalodyHitModeJudgement : IManiaHitModeJudgement
    {
        public static MalodyHitModeJudgement Instance { get; } = new MalodyHitModeJudgement();

        public static HitResult MapTo(MalodyJudge judge)
            => judge switch
            {
                MalodyJudge.Best => HitResult.Perfect,
                MalodyJudge.Cool => HitResult.Great,
                MalodyJudge.Good => HitResult.Good,
                MalodyJudge.Miss => HitResult.Miss,
                _ => HitResult.None,
            };

        public static MalodyJudge FromHitResult(HitResult result)
            => result switch
            {
                HitResult.Perfect => MalodyJudge.Best,
                HitResult.Great => MalodyJudge.Cool,
                HitResult.Good => MalodyJudge.Good,
                HitResult.Miss => MalodyJudge.Miss,
                _ => MalodyJudge.None,
            };

        public ManiaNoteJudgementOutcome EvaluateAutoMiss(double timeOffset, HitWindows hitWindows)
        {
            if (!hitWindows.CanBeHit(timeOffset))
                return ManiaNoteJudgementOutcome.ApplyResult(MapTo(MalodyJudge.Miss));

            return ManiaNoteJudgementOutcome.Ignore;
        }

        public ManiaNoteJudgementOutcome EvaluatePress(double timeOffset, HitWindows hitWindows)
        {
            var judge = FromHitResult(hitWindows.ResultFor(timeOffset));

            if (judge == MalodyJudge.None)
                return ManiaNoteJudgementOutcome.Ignore;

            return ManiaNoteJudgementOutcome.ApplyResult(MapTo(judge));
        }

        /// <summary>
        /// Malody LN：tail 仅以 <see cref="HitResult.IgnoreHit"/> 完成，不计入成绩。
        /// </summary>
        public HitResult EvaluateTail(in HoldTailEvaluationContext context) => HitResult.IgnoreHit;

        public MalodyJudge EvaluateTailJudge(in HoldTailEvaluationContext context) => MalodyJudge.None;

        public bool CanBeginHoldAt(double time, TailNote tail) => LazerHoldJudgementReplica.Instance.CanBeginHoldAt(time, tail);

        public bool IsHoldBreak(double rawOffset, HitWindows hitWindows) => false;

        public HitResult RejudgeHitEvent(HitEvent hitEvent, HitWindows hitWindows)
        {
            // TailNote 不计入成绩（Malody LN tail 仅以 IgnoreHit 完成）
            if (hitEvent.HitObject is TailNote)
                return HitResult.IgnoreHit;

            var outcome = EvaluatePress(hitEvent.TimeOffset, hitWindows);
            return outcome.Kind == ManiaNoteJudgementOutcomeKind.Apply ? outcome.Result : HitResult.Miss;
        }

        public static bool IsMalodyMode(EzEnumHitMode hitMode)
            => hitMode is EzEnumHitMode.Malody_E or EzEnumHitMode.Malody_B;
    }
}
