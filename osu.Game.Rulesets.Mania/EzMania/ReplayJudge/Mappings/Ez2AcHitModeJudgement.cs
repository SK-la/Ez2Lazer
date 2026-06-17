// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    public enum Ez2AcJudge
    {
        None,
        Marvelous,
        Perfect,
        Great,
        Good,
        Bad,
        Miss,
    }

    public sealed class Ez2AcHitModeJudgement : IManiaHitModeJudgement
    {
        public static Ez2AcHitModeJudgement Instance { get; } = new Ez2AcHitModeJudgement();

        public static HitResult MapTo(Ez2AcJudge judge) => judge switch
        {
            Ez2AcJudge.Marvelous => HitResult.Perfect,
            Ez2AcJudge.Perfect => HitResult.Perfect,
            Ez2AcJudge.Great => HitResult.Great,
            Ez2AcJudge.Good => HitResult.Good,
            Ez2AcJudge.Bad => HitResult.Meh,
            Ez2AcJudge.Miss => HitResult.Miss,
            _ => HitResult.None,
        };

        public static Ez2AcJudge FromHitResult(HitResult result) => result switch
        {
            HitResult.Perfect => Ez2AcJudge.Perfect,
            HitResult.Great => Ez2AcJudge.Great,
            HitResult.Good => Ez2AcJudge.Good,
            HitResult.Meh => Ez2AcJudge.Bad,
            HitResult.Miss => Ez2AcJudge.Miss,
            _ => Ez2AcJudge.None,
        };

        public static Ez2AcJudge SoftenLnJudge(Ez2AcJudge judge) => judge switch
        {
            Ez2AcJudge.Great => Ez2AcJudge.Perfect,
            Ez2AcJudge.Good => Ez2AcJudge.Great,
            Ez2AcJudge.Bad => Ez2AcJudge.Good,
            _ => judge,
        };

        public ManiaNoteJudgementOutcome EvaluateAutoMiss(double timeOffset, HitWindows hitWindows)
        {
            if (!hitWindows.CanBeHit(timeOffset))
                return ManiaNoteJudgementOutcome.ApplyResult(MapTo(Ez2AcJudge.Miss));

            return ManiaNoteJudgementOutcome.Ignore;
        }

        public ManiaNoteJudgementOutcome EvaluatePress(double timeOffset, HitWindows hitWindows) => EvaluatePress(timeOffset, hitWindows, isLnHead: false);

        public ManiaNoteJudgementOutcome EvaluatePress(double timeOffset, HitWindows hitWindows, bool isLnHead)
        {
            var judge = FromHitResult(hitWindows.ResultFor(timeOffset));

            if (judge == Ez2AcJudge.None)
                return ManiaNoteJudgementOutcome.Ignore;

            if (isLnHead)
                judge = SoftenLnJudge(judge);

            return ManiaNoteJudgementOutcome.ApplyResult(MapTo(judge));
        }

        public ManiaNoteJudgementOutcome EvaluateDrawablePress(double timeOffset, HitWindows hitWindows, bool isLnHead) => EvaluatePress(timeOffset, hitWindows, isLnHead);

        public HitResult EvaluateTail(in HoldTailEvaluationContext context) => MapTo(EvaluateTailJudge(context));

        public Ez2AcJudge EvaluateTailJudge(in HoldTailEvaluationContext context)
        {
            var judge = FromHitResult(context.HitWindows.ResultFor(context.TimeOffsetForJudgement));

            if (judge == Ez2AcJudge.None)
                return Ez2AcJudge.None;

            judge = SoftenLnJudge(judge);

            if (judge > Ez2AcJudge.Bad && (!context.HeadHit || context.HoldBreak || context.HoldBroken))
                judge = Ez2AcJudge.Good;

            return judge;
        }

        public bool CanBeginHoldAt(double time, TailNote tail) => LazerHoldJudgementReplica.Instance.CanBeginHoldAt(time, tail);

        public bool IsHoldBreak(double rawOffset, HitWindows hitWindows) => LazerHoldJudgementReplica.Instance.IsHoldBreak(rawOffset, hitWindows);
    }
}
