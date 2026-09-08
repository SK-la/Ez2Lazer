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
    /// LN：头定品质 + 16 分 tick 只推 combo；尾 Ignore（无松手窗）。
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
        /// LN 头软化（7th 2.0+）：仅 Cool→Kool；Good/Miss 不整档抬升。
        /// </summary>
        public static Ez2AcJudge SoftenLnHeadJudge(Ez2AcJudge judge) => judge switch
        {
            Ez2AcJudge.Cool => Ez2AcJudge.Kool,
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
                judge = SoftenLnHeadJudge(judge);

            return ManiaNoteJudgementOutcome.ApplyResult(MapTo(judge));
        }

        public ManiaNoteJudgementOutcome EvaluateDrawablePress(double timeOffset, HitWindows hitWindows, bool isLnHead) => EvaluatePress(timeOffset, hitWindows, isLnHead);

        /// <summary>LN 尾不计分，仅 Ignore 完结（对齐 Malody 尾角色）。</summary>
        public HitResult EvaluateTail(in HoldTailEvaluationContext context) => HitResult.IgnoreHit;

        public Ez2AcJudge EvaluateTailJudge(in HoldTailEvaluationContext context) => Ez2AcJudge.None;

        /// <summary>
        /// Tick：按住且状态允许 → SliderTailHit（只推 combo）；否则 IgnoreMiss（不断 combo、不计档）。
        /// </summary>
        public HitResult EvaluateTick(Ez2AcHoldState state, bool isHolding)
        {
            if (isHolding && state.ShouldIncreaseCombo)
                return HitResult.SliderTailHit;

            return HitResult.IgnoreMiss;
        }

        public bool CanBeginHoldAt(double time, TailNote tail) => LazerHoldJudgementReplica.Instance.CanBeginHoldAt(time, tail);

        public bool IsHoldBreak(double rawOffset, HitWindows hitWindows) => false;

        public HitResult RejudgeHitEvent(HitEvent hitEvent, HitWindows hitWindows)
        {
            if (hitEvent.HitObject is TailNote)
                return HitResult.IgnoreHit;

            if (hitEvent.HitObject is HoldNoteTick)
            {
                return hitEvent.Result is HitResult.SliderTailHit or HitResult.IgnoreMiss
                    ? hitEvent.Result
                    : HitResult.IgnoreMiss;
            }

            var outcome = EvaluatePress(hitEvent.TimeOffset, hitWindows, hitEvent.HitObject is HeadNote);
            return outcome.Kind == ManiaNoteJudgementOutcomeKind.Apply ? outcome.Result : HitResult.Miss;
        }
    }
}
