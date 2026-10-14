// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    public enum O2Judge
    {
        None,
        Cool,
        Good,
        Bad,
        Miss,
    }

    /// <summary>
    /// Session 侧跨输入事件的可变判定状态（如 O2 Pill）。
    /// </summary>
    public sealed class ManiaReplayJudgementState
    {
        public int O2PillCount { get; set; }

        public int O2CoolCombo { get; set; }
    }

    public sealed class O2JudgeModeJudgement : IManiaJudgeModeJudgement
    {
        public static O2JudgeModeJudgement Instance { get; } = new O2JudgeModeJudgement();

        // O2 首次断连即封掉后续输入（ApplyDrawableHoldBreakUpdate），Body 维持单次。
        public bool AllowsRepeatedHoldBreak => false;

        public static HitResult MapTo(O2Judge judge) => judge switch
        {
            O2Judge.Cool => HitResult.Perfect,
            O2Judge.Good => HitResult.Good,
            O2Judge.Bad => HitResult.Meh,
            O2Judge.Miss => HitResult.Miss,
            _ => HitResult.None,
        };

        public static O2Judge FromHitResult(HitResult result) => result switch
        {
            HitResult.Perfect => O2Judge.Cool,
            HitResult.Good => O2Judge.Good,
            HitResult.Meh => O2Judge.Bad,
            HitResult.Miss => O2Judge.Miss,
            _ => O2Judge.None,
        };

        public readonly struct NotePressContext
        {
            public double RawOffset { get; init; }

            public double Bpm { get; init; }

            public bool PillModeEnabled { get; init; }

            public ManiaReplayJudgementState State { get; init; }

            public bool UsePressTimeBpmForJudgement { get; init; }
        }

        public sealed class HoldBreakState
        {
            public Func<DrawableHitObject, double, bool>? CheckHittableBackup;
        }

        public ManiaNoteJudgementOutcome EvaluateAutoMiss(double timeOffset, HitWindows hitWindows)
            => EvaluateAutoMiss(timeOffset, hitWindows, bpm: 0);

        public ManiaNoteJudgementOutcome EvaluateAutoMiss(double timeOffset, HitWindows hitWindows, double bpm)
        {
            bool canHit = bpm > 0
                ? O2JudgeModeExtension.CanBeHit(timeOffset, bpm, ManiaJudgementRound.GetTotalMultiplier(hitWindows))
                : hitWindows.CanBeHit(timeOffset);

            if (!canHit)
                return ManiaNoteJudgementOutcome.ApplyResult(MapTo(O2Judge.Miss));

            return ManiaNoteJudgementOutcome.Ignore;
        }

        public ManiaNoteJudgementOutcome EvaluatePress(double timeOffset, HitWindows hitWindows) => EvaluatePress(timeOffset, hitWindows, default);

        public ManiaNoteJudgementOutcome EvaluatePress(double timeOffset, HitWindows hitWindows, in NotePressContext context)
        {
            var judge = FromHitResult(resolvePressResult(timeOffset, hitWindows, context.UsePressTimeBpmForJudgement, context.Bpm));

            if (judge == O2Judge.None)
                return ManiaNoteJudgementOutcome.Ignore;

            if (context.PillModeEnabled)
                ApplyPillLogic(Math.Abs(context.RawOffset), context.Bpm, context.State, ref judge);

            return ManiaNoteJudgementOutcome.ApplyResult(MapTo(judge));
        }

        public HitResult EvaluateTail(in HoldTailEvaluationContext context) => MapTo(EvaluateTailJudge(context));

        public O2Judge EvaluateTailJudge(in HoldTailEvaluationContext context)
        {
            var judge = FromHitResult(resolvePressResult(context.TimeOffsetForJudgement, context.HitWindows, context.UsePressTimeBpmForJudgement, context.Bpm));

            if (context.PillModeEnabled)
                ApplyPillLogic(Math.Abs(context.RawOffset), context.Bpm, context.State, ref judge);

            if (context.HoldBroken && context.RawOffset < 0)
                return O2Judge.None;

            if (context.HoldBreak || !context.HeadHit || context.HoldBroken)
                judge = O2Judge.Miss;

            return judge;
        }

        public void ApplyDrawableHoldBreakUpdate(DrawableHoldNote hold, HoldBreakState state)
        {
            if (hold.EzHasEarlyHoldBreak)
            {
                if (hold.EzIsHoldingBindable is Bindable<bool> holding)
                    holding.Value = false;

                state.CheckHittableBackup ??= hold.EzCheckHittable;
                hold.EzDisableCheckHittable();
                return;
            }

            if (state.CheckHittableBackup != null)
            {
                hold.EzRestoreCheckHittable(state.CheckHittableBackup);
                state.CheckHittableBackup = null;
            }
        }

        public bool TryO2HoldCheckForResult(DrawableHoldNote hold, bool userTriggered, double timeOffset)
        {
            if (!hold.EzTailAllJudged)
                return true;

            hold.EzFinalizeO2HoldFromTail();
            return true;
        }

        public bool CanBeginHoldAt(double time, TailNote tail) => LazerHoldJudgementReplica.Instance.CanBeginHoldAt(time, tail);

        public bool IsHoldBreak(double rawOffset, HitWindows hitWindows) => LazerHoldJudgementReplica.Instance.IsHoldBreak(rawOffset, hitWindows);

        private static HitResult resolvePressResult(double timeOffset, HitWindows hitWindows, bool usePressTimeBpm, double bpm)
        {
            if (usePressTimeBpm && bpm > 0)
                return O2JudgeModeExtension.ResultFor(timeOffset, bpm, ManiaJudgementRound.GetTotalMultiplier(hitWindows));

            return hitWindows.ResultFor(timeOffset);
        }

        public HitResult RejudgeHitEvent(HitEvent hitEvent, HitWindows hitWindows)
        {
            if (hitEvent.HitObject is TailNote)
            {
                var tailOutcome = EvaluatePress(hitEvent.TimeOffset, hitWindows);
                return tailOutcome.Kind == ManiaNoteJudgementOutcomeKind.Apply ? tailOutcome.Result : HitResult.Miss;
            }

            var outcome = EvaluatePress(hitEvent.TimeOffset, hitWindows);
            return outcome.Kind == ManiaNoteJudgementOutcomeKind.Apply ? outcome.Result : HitResult.Miss;
        }

        internal static void ApplyPillLogic(double absOffset, double bpm, ManiaReplayJudgementState state, ref O2Judge judge)
        {
            double coolRange = O2JudgeModeExtension.BASE_COOL / bpm;
            double goodRange = O2JudgeModeExtension.BASE_GOOD / bpm;
            double badRange = O2JudgeModeExtension.BASE_BAD / bpm;

            if (absOffset <= coolRange)
            {
                state.O2CoolCombo++;

                if (state.O2CoolCombo >= 15)
                {
                    state.O2CoolCombo = 0;
                    state.O2PillCount = Math.Clamp(state.O2PillCount + 1, 0, 5);
                }

                return;
            }

            if (absOffset <= goodRange)
            {
                state.O2CoolCombo = 0;
                return;
            }

            if (absOffset > badRange)
                return;

            state.O2CoolCombo = 0;

            if (state.O2PillCount <= 0)
                return;

            state.O2PillCount = Math.Clamp(state.O2PillCount - 1, 0, 5);
            judge = O2Judge.Cool;
        }
    }
}
