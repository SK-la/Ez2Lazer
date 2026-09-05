// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// Drawable 与 Session 共用的 note / hold-tail 判定内核（Phase D KERNEL-ONE）。
    /// O2 热路径遵守 O2-NOMUTATE：不调用 <c>UpdateO2JamBpmFromTime</c>。
    /// </summary>
    internal static class ManiaJudgementKernel
    {
        public enum NoteEvaluationKind
        {
            NotHandled,
            Ignore,
            ApplyNoteOutcome,
            ApplyBmsAction,
        }

        public readonly struct NoteEvaluationRequest
        {
            public ManiaJudgementRound Round { get; init; }

            public double TimeOffset { get; init; }

            public HitWindows HitWindows { get; init; }

            public bool UserTriggered { get; init; }

            public bool IsLnHead { get; init; }

            public double EventTime { get; init; }

            /// <summary>Session 等无列级 BPM 缓存时显式传入 press-time BPM。</summary>
            public double? PressBpm { get; init; }

            public long FrameStableId { get; init; }

            public BmsHitModeJudgement.BmsRouteState? BmsState { get; init; }

            public bool ForcePoorOnTailHoldBreak { get; init; }

            public bool CheckBmsCanRouteOnPress { get; init; }
        }

        public readonly struct NoteEvaluationResult
        {
            public NoteEvaluationKind Kind { get; init; }

            public ManiaNoteJudgementOutcome NoteOutcome { get; init; }

            public BmsHitModeJudgement.DrawableAction BmsAction { get; init; }

            public static NoteEvaluationResult NotHandled => new NoteEvaluationResult { Kind = NoteEvaluationKind.NotHandled };

            public static NoteEvaluationResult Ignore => new NoteEvaluationResult { Kind = NoteEvaluationKind.Ignore };

            public static NoteEvaluationResult FromNoteOutcome(ManiaNoteJudgementOutcome outcome)
                => new NoteEvaluationResult { Kind = NoteEvaluationKind.ApplyNoteOutcome, NoteOutcome = outcome };

            public static NoteEvaluationResult FromBmsAction(BmsHitModeJudgement.DrawableAction action)
                => new NoteEvaluationResult { Kind = NoteEvaluationKind.ApplyBmsAction, BmsAction = action };
        }

        public readonly struct HoldTailEvaluationRequest
        {
            public ManiaJudgementRound Round { get; init; }

            public double TimeOffset { get; init; }

            public double RawOffset { get; init; }

            public HitWindows HitWindows { get; init; }

            public bool UserTriggered { get; init; }

            public bool HeadHit { get; init; }

            public bool HoldBroken { get; init; }

            public bool WasHolding { get; init; }

            public bool HasHoldBreak { get; init; }

            public double EventTime { get; init; }

            public double? PressBpm { get; init; }

            public long FrameStableId { get; init; }

            public BmsHitModeJudgement.BmsRouteState? BmsState { get; init; }
        }

        public readonly struct HoldTailEvaluationResult
        {
            public bool Handled { get; init; }

            public bool ApplyMinResult { get; init; }

            public HitResult? FinalResult { get; init; }

            public BmsHitModeJudgement.DrawableAction BmsAction { get; init; }

            public static HoldTailEvaluationResult Unhandled => default;

            public static HoldTailEvaluationResult HandledNoOp => new HoldTailEvaluationResult { Handled = true };

            public static HoldTailEvaluationResult MinResult => new HoldTailEvaluationResult { Handled = true, ApplyMinResult = true };

            public static HoldTailEvaluationResult FromResult(HitResult result)
                => new HoldTailEvaluationResult { Handled = true, FinalResult = result };

            public static HoldTailEvaluationResult FromBmsAction(BmsHitModeJudgement.DrawableAction action)
                => new HoldTailEvaluationResult { Handled = true, BmsAction = action };
        }

        public static NoteEvaluationResult EvaluateNote(in NoteEvaluationRequest request)
        {
            var round = request.Round;
            var strategy = round.Strategy;

            if (strategy == null || !round.IsEzHitMode)
                return NoteEvaluationResult.NotHandled;

            if (strategy is BmsHitModeJudgement bms)
            {
                if (request.HitWindows is not ManiaHitWindows maniaWindows || request.BmsState == null)
                    return NoteEvaluationResult.Ignore;

                var action = request.UserTriggered
                    ? bms.EvaluatePressAction(
                        maniaWindows,
                        request.TimeOffset,
                        request.BmsState,
                        round.PoorEnabled,
                        checkCanRouteOnPress: request.CheckBmsCanRouteOnPress,
                        forcePoorOnTailHoldBreak: request.ForcePoorOnTailHoldBreak)
                    : bms.EvaluateAutoMissAction(maniaWindows, request.TimeOffset);

                return NoteEvaluationResult.FromBmsAction(action);
            }

            if (strategy is O2HitModeJudgement o2)
            {
                double pressBpm = resolveO2PressBpm(request);

                if (request.UserTriggered)
                {
                    var outcome = o2.EvaluatePress(request.TimeOffset, request.HitWindows, new O2HitModeJudgement.NotePressContext
                    {
                        RawOffset = request.TimeOffset,
                        Bpm = pressBpm,
                        UsePressTimeBpmForJudgement = true,
                        PillModeEnabled = round.PillModeEnabled,
                        State = round.MutableState,
                    });

                    return NoteEvaluationResult.FromNoteOutcome(outcome);
                }

                double autoMissBpm = request.PressBpm ?? round.GetO2BpmForAutoMiss(request.EventTime, request.FrameStableId);

                return NoteEvaluationResult.FromNoteOutcome(o2.EvaluateAutoMiss(request.TimeOffset, request.HitWindows, autoMissBpm));
            }

            if (strategy is Ez2AcHitModeJudgement ez2Ac)
            {
                var outcome = request.UserTriggered
                    ? ez2Ac.EvaluatePress(request.TimeOffset, request.HitWindows, request.IsLnHead)
                    : ez2Ac.EvaluateAutoMiss(request.TimeOffset, request.HitWindows);

                return NoteEvaluationResult.FromNoteOutcome(outcome);
            }

            var genericOutcome = request.UserTriggered
                ? strategy.EvaluatePress(request.TimeOffset, request.HitWindows)
                : strategy.EvaluateAutoMiss(request.TimeOffset, request.HitWindows);

            return NoteEvaluationResult.FromNoteOutcome(genericOutcome);
        }

        public static HoldTailEvaluationResult EvaluateHoldTail(in HoldTailEvaluationRequest request)
        {
            var round = request.Round;
            var strategy = round.Strategy;

            if (strategy == null || !round.IsEzHitMode)
                return HoldTailEvaluationResult.Unhandled;

            if (strategy is MalodyHitModeJudgement)
            {
                if (!request.UserTriggered && request.TimeOffset < 0)
                    return HoldTailEvaluationResult.HandledNoOp;

                if (!request.UserTriggered && request.TimeOffset >= 0)
                    return HoldTailEvaluationResult.FromResult(HitResult.IgnoreHit);

                return HoldTailEvaluationResult.FromResult(HitResult.IgnoreHit);
            }

            if (strategy is BmsHitModeJudgement bms)
            {
                if (request.HitWindows is not ManiaHitWindows maniaWindows || request.BmsState == null)
                    return HoldTailEvaluationResult.HandledNoOp;

                var action = request.UserTriggered
                    ? bms.EvaluatePressAction(
                        maniaWindows,
                        request.TimeOffset,
                        request.BmsState,
                        round.PoorEnabled,
                        checkCanRouteOnPress: false,
                        forcePoorOnTailHoldBreak: request.HoldBroken && !request.WasHolding)
                    : bms.EvaluateAutoMissAction(maniaWindows, request.TimeOffset);

                return HoldTailEvaluationResult.FromBmsAction(action);
            }

            if (strategy is O2HitModeJudgement o2)
            {
                double pressBpm = resolveO2PressBpm(request);

                if (!request.UserTriggered)
                {
                    if (request.HasHoldBreak)
                    {
                        if (request.TimeOffset < 0)
                            return HoldTailEvaluationResult.HandledNoOp;

                        return HoldTailEvaluationResult.FromResult(O2HitModeJudgement.MapTo(O2Judge.Miss));
                    }

                    double mult = ManiaJudgementRound.GetTotalMultiplier(request.HitWindows);

                    if (!O2HitModeExtension.CanBeHit(request.TimeOffset, pressBpm, mult))
                        return HoldTailEvaluationResult.MinResult;

                    return HoldTailEvaluationResult.HandledNoOp;
                }

                var judge = o2.EvaluateTailJudge(new HoldTailEvaluationContext
                {
                    RawOffset = request.RawOffset,
                    TimeOffsetForJudgement = request.TimeOffset,
                    HitWindows = request.HitWindows,
                    HeadHit = request.HeadHit,
                    HoldBreak = o2.IsHoldBreak(request.RawOffset, request.HitWindows),
                    HoldBroken = request.HoldBroken,
                    WasHoldingBeforeRelease = request.WasHolding,
                    PillModeEnabled = round.PillModeEnabled,
                    Bpm = pressBpm,
                    UsePressTimeBpmForJudgement = true,
                    State = round.MutableState,
                });

                if (judge == O2Judge.None)
                    return HoldTailEvaluationResult.HandledNoOp;

                return HoldTailEvaluationResult.FromResult(O2HitModeJudgement.MapTo(judge));
            }

            if (strategy is Ez2AcHitModeJudgement ez2Ac)
            {
                if (!request.UserTriggered)
                {
                    if (request.TimeOffset < 0)
                        return HoldTailEvaluationResult.HandledNoOp;

                    if (!request.HitWindows.CanBeHit(request.TimeOffset))
                        return HoldTailEvaluationResult.MinResult;

                    return HoldTailEvaluationResult.HandledNoOp;
                }

                var tailJudge = ez2Ac.EvaluateTailJudge(new HoldTailEvaluationContext
                {
                    RawOffset = request.RawOffset,
                    TimeOffsetForJudgement = request.TimeOffset,
                    HitWindows = request.HitWindows,
                    HeadHit = request.HeadHit,
                    HoldBreak = ez2Ac.IsHoldBreak(request.RawOffset, request.HitWindows),
                    HoldBroken = request.HoldBroken,
                    WasHoldingBeforeRelease = request.WasHolding,
                });

                if (tailJudge == Ez2AcJudge.None)
                {
                    if (!request.HitWindows.CanBeHit(request.TimeOffset))
                        return HoldTailEvaluationResult.MinResult;

                    return HoldTailEvaluationResult.HandledNoOp;
                }

                return HoldTailEvaluationResult.FromResult(Ez2AcHitModeJudgement.MapTo(tailJudge));
            }

            var genericResult = strategy.EvaluateTail(new HoldTailEvaluationContext
            {
                RawOffset = request.RawOffset,
                TimeOffsetForJudgement = request.TimeOffset,
                HitWindows = request.HitWindows,
                HeadHit = request.HeadHit,
                HoldBreak = strategy.IsHoldBreak(request.RawOffset, request.HitWindows),
                HoldBroken = request.HoldBroken,
                WasHoldingBeforeRelease = request.WasHolding,
                State = round.MutableState,
                EventTime = request.EventTime,
                Bpm = round.O2PressBpm,
                PillModeEnabled = round.PillModeEnabled,
            });

            if (genericResult == HitResult.None)
                return HoldTailEvaluationResult.HandledNoOp;

            return HoldTailEvaluationResult.FromResult(genericResult);
        }

        private static double resolveO2PressBpm(in NoteEvaluationRequest request)
            => request.PressBpm ?? request.Round.O2PressBpm;

        private static double resolveO2PressBpm(in HoldTailEvaluationRequest request)
            => request.PressBpm ?? request.Round.O2PressBpm;
    }
}
