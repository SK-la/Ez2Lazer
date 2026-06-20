// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using static osu.Game.Rulesets.Mania.EzMania.ReplayJudge.ManiaColumnSimulator;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    /// <summary>
    /// BMS 系（IIDX / LR2 / Raja）判定 — Mode 原生名 + MapTo；Session 与 Drawable 唯一源。
    /// </summary>
    public enum BmsJudge
    {
        None,
        PGreat,
        Great,
        Good,
        Bad,
        Poor,
        KPoor,
    }

    public sealed class BmsHitModeJudgement : IManiaHitModeJudgement
    {
        public static BmsHitModeJudgement Instance { get; } = new BmsHitModeJudgement();

        public static HitResult MapTo(BmsJudge judge)
            => judge switch
            {
                BmsJudge.PGreat => HitResult.Perfect,
                BmsJudge.Great => HitResult.Great,
                BmsJudge.Good => HitResult.Good,
                BmsJudge.Bad => HitResult.Meh,
                BmsJudge.Poor => HitResult.Miss,
                BmsJudge.KPoor => HitResult.Poor,
                _ => HitResult.None,
            };

        public static BmsJudge FromHitResult(HitResult result)
            => result switch
            {
                HitResult.Perfect => BmsJudge.PGreat,
                HitResult.Great => BmsJudge.Great,
                HitResult.Good => BmsJudge.Good,
                HitResult.Meh => BmsJudge.Bad,
                HitResult.Miss => BmsJudge.Poor,
                HitResult.Poor => BmsJudge.KPoor,
                _ => BmsJudge.None,
            };

        public sealed class BmsRouteState
        {
            public bool HasLateKPoor;
            public bool CanRouteToKPoor;
        }

        public readonly struct DrawableAction
        {
            public bool Handled { get; init; }

            public bool ApplyFinal { get; init; }

            public bool DispatchExtra { get; init; }

            public BmsJudge Judge { get; init; }

            public bool ClearCanRouteToKPoor { get; init; }

            public bool? SetCanRouteToKPoor { get; init; }

            public bool? SetHasLateKPoor { get; init; }

            public static DrawableAction NotHandled => new DrawableAction();

            public static DrawableAction Ignore => new DrawableAction { Handled = true };

            public static DrawableAction Final(BmsJudge judge, bool? setCanRouteToKPoor = null)
                => new DrawableAction { Handled = true, ApplyFinal = true, Judge = judge, SetCanRouteToKPoor = setCanRouteToKPoor };

            public static DrawableAction Extra(BmsJudge judge, bool? setHasLateKPoor = null, bool clearCanRouteToKPoor = false)
                => new DrawableAction
                {
                    Handled = true,
                    DispatchExtra = true,
                    Judge = judge,
                    SetHasLateKPoor = setHasLateKPoor,
                    ClearCanRouteToKPoor = clearCanRouteToKPoor,
                };
        }

        public enum SessionPressKind
        {
            None,
            ApplyFinal,
            DispatchExtra,
        }

        public readonly struct SessionPressOutcome
        {
            public SessionPressKind Kind { get; init; }

            public BmsJudge Judge { get; init; }

            public bool EnableCanRouteToKPoor { get; init; }
        }

        public static bool IsLateOutsideBad(double timeOffset, double badLate) => timeOffset > badLate;

        public static double WindowFor(ManiaHitWindows windows, BmsJudge judge, bool early)
            => windows.WindowFor(MapTo(judge), early);

        public static double WindowFor(HitModeHelper helper, BmsJudge judge, bool early)
            => helper.WindowFor(MapTo(judge), early);

        public static void ExpandMissCollectionWindows(HitModeHelper helper, double lenienceFactor, ref double missEarly, ref double missLate)
        {
            double badLateWindow = WindowFor(helper, BmsJudge.Bad, false) * lenienceFactor;
            double kPoorEarlyWindow = WindowFor(helper, BmsJudge.KPoor, true) * lenienceFactor;
            double kPoorLateWindow = WindowFor(helper, BmsJudge.KPoor, false) * lenienceFactor;
            missEarly = Math.Max(missEarly, kPoorEarlyWindow);
            missLate = Math.Max(missLate, Math.Max(kPoorLateWindow, badLateWindow + kPoorEarlyWindow));
        }

        public DrawableAction TryPostBadOnPressed(ManiaHitWindows windows, BmsRouteState state)
        {
            if (!state.CanRouteToKPoor || !windows.IsHitResultAllowed(MapTo(BmsJudge.KPoor)))
                return DrawableAction.NotHandled;

            return DrawableAction.Extra(BmsJudge.KPoor, clearCanRouteToKPoor: true);
        }

        public DrawableAction EvaluateDrawableAutoMiss(ManiaHitWindows windows, double timeOffset)
        {
            double badLate = WindowFor(windows, BmsJudge.Bad, false);

            if (shouldAutoMiss(windows, timeOffset, badLate))
                return DrawableAction.Final(BmsJudge.Poor);

            return DrawableAction.Ignore;
        }

        public DrawableAction EvaluateDrawablePress(ManiaHitWindows windows, double timeOffset, BmsRouteState state, bool forcePoorOnTailHoldBreak = false)
        {
            double badLate = WindowFor(windows, BmsJudge.Bad, false);

            if (state.CanRouteToKPoor && windows.IsHitResultAllowed(MapTo(BmsJudge.KPoor)))
                return DrawableAction.Extra(BmsJudge.KPoor, clearCanRouteToKPoor: true);

            var judge = resolvePressedJudge(windows, timeOffset, badLate);

            if (judge == BmsJudge.None)
                return DrawableAction.Ignore;

            if (forcePoorOnTailHoldBreak)
                judge = BmsJudge.Poor;

            if (judge == BmsJudge.KPoor)
            {
                if (!windows.IsHitResultAllowed(MapTo(BmsJudge.KPoor)))
                    return DrawableAction.Ignore;

                bool isLatePress = IsLateOutsideBad(timeOffset, badLate);

                if (!isLatePress || !state.HasLateKPoor)
                    return DrawableAction.Extra(BmsJudge.KPoor, setHasLateKPoor: isLatePress);

                return DrawableAction.Ignore;
            }

            return DrawableAction.Final(judge, setCanRouteToKPoor: judge == BmsJudge.Bad && timeOffset < 0);
        }

        public static void ApplyRouteState(BmsRouteState state, DrawableAction action)
        {
            if (action.ClearCanRouteToKPoor)
                state.CanRouteToKPoor = false;

            if (action.SetCanRouteToKPoor.HasValue)
                state.CanRouteToKPoor = action.SetCanRouteToKPoor.Value;

            if (action.SetHasLateKPoor.HasValue)
                state.HasLateKPoor = action.SetHasLateKPoor.Value;
        }

        internal SessionPressOutcome EvaluateSessionPress(ManiaHitWindows windows, double timeOffset, BmsRouteState state, bool poorEnabled)
        {
            var outcome = EvaluatePress(timeOffset, windows);

            if (outcome.Kind == ManiaNoteJudgementOutcomeKind.None)
                return default;

            var judge = FromHitResult(outcome.Result);

            if (outcome.Kind == ManiaNoteJudgementOutcomeKind.DispatchExtra)
            {
                double badLate = WindowFor(windows, BmsJudge.Bad, false);
                bool isLatePress = IsLateOutsideBad(timeOffset, badLate);

                if (isLatePress && state.HasLateKPoor)
                    return default;

                if (isLatePress)
                    state.HasLateKPoor = true;

                return new SessionPressOutcome { Kind = SessionPressKind.DispatchExtra, Judge = judge };
            }

            return new SessionPressOutcome
            {
                Kind = SessionPressKind.ApplyFinal,
                Judge = judge,
                EnableCanRouteToKPoor = judge == BmsJudge.Bad && timeOffset < 0 && poorEnabled,
            };
        }

        internal bool TryRoutePostBadKPoor(
            IReadOnlyList<LaneTargetState> laneStates,
            IEnumerable<LaneTargetState> unjudgedCandidates,
            double inputTime,
            double offsetPlusMania,
            HitModeHelper hitWindowHelper,
            Action<HitObject, HitResult> applyTransient)
        {
            var postBadCandidate = laneStates
                                   .Where(s => s.Judged && s.BmsRoute.CanRouteToKPoor && isWithinMissWindow(hitWindowHelper, s.Target.StartTime, inputTime))
                                   .OrderBy(s => distanceToNonBadWindow(s.Target.StartTime, inputTime, hitWindowHelper))
                                   .ThenBy(s => s.Target.StartTime)
                                   .FirstOrDefault();

            if (postBadCandidate == null)
                return false;

            double postBadDistance = distanceToNonBadWindow(postBadCandidate.Target.StartTime, inputTime, hitWindowHelper);

            double unjudgedDistance = unjudgedCandidates
                                      .Where(s => !s.Judged)
                                      .Select(s => distanceToNonBadWindow(s.Target.StartTime, inputTime, hitWindowHelper))
                                      .DefaultIfEmpty(double.PositiveInfinity)
                                      .Min();

            if (postBadDistance > unjudgedDistance || postBadCandidate.BmsRoute.HasLateKPoor)
                return false;

            postBadCandidate.BmsRoute.HasLateKPoor = true;
            postBadCandidate.BmsRoute.CanRouteToKPoor = false;

            double offset = inputTime - postBadCandidate.Target.StartTime + offsetPlusMania;
            applyTransient(postBadCandidate.Target, MapTo(BmsJudge.KPoor));
            return true;
        }

        public ManiaNoteJudgementOutcome EvaluateAutoMiss(double timeOffset, HitWindows hitWindows)
        {
            if (hitWindows is not ManiaHitWindows maniaWindows)
                return ManiaNoteJudgementOutcome.Ignore;

            double badLate = WindowFor(maniaWindows, BmsJudge.Bad, false);

            if (shouldAutoMiss(maniaWindows, timeOffset, badLate))
                return ManiaNoteJudgementOutcome.ApplyResult(MapTo(BmsJudge.Poor));

            return ManiaNoteJudgementOutcome.Ignore;
        }

        public ManiaNoteJudgementOutcome EvaluatePress(double timeOffset, HitWindows hitWindows)
        {
            if (hitWindows is not ManiaHitWindows maniaWindows)
                return ManiaNoteJudgementOutcome.Ignore;

            double badLate = WindowFor(maniaWindows, BmsJudge.Bad, false);
            var judge = resolvePressedJudge(maniaWindows, timeOffset, badLate);

            if (judge == BmsJudge.None)
                return ManiaNoteJudgementOutcome.Ignore;

            if (judge == BmsJudge.KPoor)
                return ManiaNoteJudgementOutcome.DispatchExtraResult(MapTo(BmsJudge.KPoor));

            return ManiaNoteJudgementOutcome.ApplyResult(MapTo(judge));
        }

        public HitResult EvaluateTail(in HoldTailEvaluationContext context)
            => MapTo(EvaluateTailJudge(context));

        public BmsJudge EvaluateTailJudge(in HoldTailEvaluationContext context)
        {
            if (context.HitWindows is not ManiaHitWindows maniaWindows)
                return BmsJudge.None;

            double badLate = WindowFor(maniaWindows, BmsJudge.Bad, false);
            var judge = FromHitResult(maniaWindows.ResultFor(context.TimeOffsetForJudgement));

            if (judge != BmsJudge.None)
                return judge;

            if (IsLateOutsideBad(context.TimeOffsetForJudgement, badLate))
                return BmsJudge.Poor;

            if (context.HoldBroken)
                return BmsJudge.Poor;

            if (context.WasHoldingBeforeRelease && IsLateOutsideBad(context.TimeOffsetForJudgement, badLate))
                return BmsJudge.Poor;

            if (context.HoldBreak)
                return BmsJudge.Poor;

            double badEarly = WindowFor(maniaWindows, BmsJudge.Bad, true);
            double kPoorEarly = WindowFor(maniaWindows, BmsJudge.KPoor, true);

            if (kPoorEarly > badEarly && context.TimeOffsetForJudgement < -badEarly && context.TimeOffsetForJudgement >= -kPoorEarly)
                return BmsJudge.KPoor;

            return BmsJudge.None;
        }

        public bool CanBeginHoldAt(double time, TailNote tail) => Replicas.LazerHoldJudgementReplica.Instance.CanBeginHoldAt(time, tail);

        public bool IsHoldBreak(double rawOffset, HitWindows hitWindows) => Replicas.LazerHoldJudgementReplica.Instance.IsHoldBreak(rawOffset, hitWindows);

        public HitResult RejudgeHitEvent(HitEvent hitEvent, HitWindows hitWindows)
        {
            // TailNote 保留原始结果（尾判依赖 BmsRouteState / holdBreak 等上下文）
            if (hitEvent.HitObject is TailNote)
                return hitEvent.Result;

            var outcome = EvaluatePress(hitEvent.TimeOffset, hitWindows);
            // BMS KPoor（DispatchExtra）在重判场景无跨物体路由可用，视为 Miss
            return outcome.Kind == ManiaNoteJudgementOutcomeKind.Apply ? outcome.Result : HitResult.Miss;
        }

        private static bool shouldAutoMiss(ManiaHitWindows windows, double timeOffset, double badLate)
            => windows.ResultFor(timeOffset) == HitResult.None && IsLateOutsideBad(timeOffset, badLate);

        private static BmsJudge resolvePressedJudge(ManiaHitWindows windows, double timeOffset, double badLate)
        {
            var judge = FromHitResult(windows.ResultFor(timeOffset));

            if (judge != BmsJudge.None)
                return judge;

            if (IsLateOutsideBad(timeOffset, badLate))
                return BmsJudge.Poor;

            double badEarly = WindowFor(windows, BmsJudge.Bad, true);
            double kPoorEarly = WindowFor(windows, BmsJudge.KPoor, true);

            if (kPoorEarly > badEarly && timeOffset < -badEarly && timeOffset >= -kPoorEarly)
                return BmsJudge.KPoor;

            return BmsJudge.None;
        }

        private static bool isWithinMissWindow(HitModeHelper hitWindowHelper, double noteTime, double inputTime)
        {
            double early = hitWindowHelper.WindowFor(HitResult.Miss, true);
            double late = hitWindowHelper.WindowFor(HitResult.Miss, false);
            return inputTime >= noteTime - early && inputTime <= noteTime + late;
        }

        private static double distanceToNonBadWindow(double noteTime, double inputTime, HitModeHelper hitWindowHelper)
        {
            double early = hitWindowHelper.WindowFor(HitResult.Good, true);
            double late = hitWindowHelper.WindowFor(HitResult.Good, false);
            double start = noteTime - early;
            double end = noteTime + late;

            if (inputTime < start)
                return start - inputTime;

            if (inputTime > end)
                return inputTime - end;

            return 0;
        }
    }
}
