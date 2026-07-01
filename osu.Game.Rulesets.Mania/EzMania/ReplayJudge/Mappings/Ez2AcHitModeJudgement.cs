// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    /// <summary>
    /// EZ2AC 判定 — Mode 原生名 + MapTo；
    /// Session 与 Drawable 唯一源。
    /// </summary>
    public enum Ez2AcJudge
    {
        None,
        Kool,
        Cool,
        Good,
        Miss,
        Fail,
    }

    public sealed class Ez2AcHitModeJudgement : IManiaHitModeJudgement
    {
        public static Ez2AcHitModeJudgement Instance { get; } = new Ez2AcHitModeJudgement();

        public static HitResult MapTo(Ez2AcJudge judge) => judge switch
        {
            Ez2AcJudge.Kool => HitResult.Perfect,
            Ez2AcJudge.Cool => HitResult.Great,
            Ez2AcJudge.Good => HitResult.Good,
            Ez2AcJudge.Miss => HitResult.Meh,
            Ez2AcJudge.Fail => HitResult.Miss,
            _ => HitResult.None,
        };

        public static Ez2AcJudge FromHitResult(HitResult result) => result switch
        {
            HitResult.Perfect => Ez2AcJudge.Kool,
            HitResult.Great => Ez2AcJudge.Cool,
            HitResult.Good => Ez2AcJudge.Good,
            HitResult.Meh => Ez2AcJudge.Miss,
            HitResult.Miss => Ez2AcJudge.Fail,
            _ => Ez2AcJudge.None,
        };

        /// <summary>
        /// LN 尾判软化：因长条更难对准，将尾判结果放松一档。
        /// </summary>
        public static Ez2AcJudge SoftenLnJudge(Ez2AcJudge judge) => judge switch
        {
            Ez2AcJudge.Cool => Ez2AcJudge.Kool,
            Ez2AcJudge.Good => Ez2AcJudge.Cool,
            Ez2AcJudge.Miss => Ez2AcJudge.Good,
            _ => judge,
        };

        public ManiaNoteJudgementOutcome EvaluateAutoMiss(double timeOffset, HitWindows hitWindows)
        {
            if (!hitWindows.CanBeHit(timeOffset))
                return ManiaNoteJudgementOutcome.ApplyResult(MapTo(Ez2AcJudge.Fail));

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

            if (judge > Ez2AcJudge.Miss && (!context.HeadHit || context.HoldBreak || context.HoldBroken))
                judge = Ez2AcJudge.Good;

            return judge;
        }

        public bool CanBeginHoldAt(double time, TailNote tail) => LazerHoldJudgementReplica.Instance.CanBeginHoldAt(time, tail);

        public bool IsHoldBreak(double rawOffset, HitWindows hitWindows) => LazerHoldJudgementReplica.Instance.IsHoldBreak(rawOffset, hitWindows);

        public HitResult RejudgeHitEvent(HitEvent hitEvent, HitWindows hitWindows)
        {
            // TailNote 保留原始结果（尾判依赖 headHit/holdBreak/holdBroken 上下文的 SoftenLnJudge + cap）
            if (hitEvent.HitObject is TailNote)
                return hitEvent.Result;

            // HeadNote 应用 LN 头软化
            var outcome = EvaluatePress(hitEvent.TimeOffset, hitWindows, hitEvent.HitObject is HeadNote);
            return outcome.Kind == ManiaNoteJudgementOutcomeKind.Apply ? outcome.Result : HitResult.Miss;
        }
    }
}
