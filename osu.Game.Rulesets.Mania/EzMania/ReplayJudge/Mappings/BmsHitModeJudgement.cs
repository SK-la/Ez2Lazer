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

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    /// <summary>
    /// BMS 系（IIDX / LR2 / Raja）判定 — Mode 原生名 + MapTo；
    /// Session 与 Drawable 唯一源。
    /// </summary>
    public enum BmsJudge
    {
        None,
        Perfect,
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
                BmsJudge.Perfect => HitResult.Perfect,
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
                HitResult.Perfect => BmsJudge.Perfect,
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

        public DrawableAction TryPostBadOnPressed(BmsRouteState state, bool poorEnabled)
        {
            // poorEnabled 来自当局冻结环境（ManiaJudgementRound.PoorEnabled），已蕴含 BmsPoor 开关。
            // 不要再回读 windows.IsHitResultAllowed(Poor)：那会读全局配置，与会用冻结值的 LaneController 结论不一致。
            if (!poorEnabled || !state.CanRouteToKPoor)
                return DrawableAction.NotHandled;

            return DrawableAction.Extra(BmsJudge.KPoor, clearCanRouteToKPoor: true);
        }

        public DrawableAction EvaluateAutoMissAction(ManiaHitWindows windows, double timeOffset)
            => toDrawableAction(evaluatePressCore(windows, timeOffset, state: null, poorEnabled: false, autoMiss: true, forcePoorOnTailHoldBreak: false, checkCanRouteOnPress: false));

        public DrawableAction EvaluatePressAction(
            ManiaHitWindows windows,
            double timeOffset,
            BmsRouteState state,
            bool poorEnabled,
            bool checkCanRouteOnPress,
            bool forcePoorOnTailHoldBreak = false)
            => toDrawableAction(evaluatePressCore(windows, timeOffset, state, poorEnabled, autoMiss: false, forcePoorOnTailHoldBreak, checkCanRouteOnPress));

        public DrawableAction EvaluateDrawableAutoMiss(ManiaHitWindows windows, double timeOffset)
            => EvaluateAutoMissAction(windows, timeOffset);

        public DrawableAction EvaluateDrawablePress(ManiaHitWindows windows, double timeOffset, BmsRouteState state, bool poorEnabled, bool forcePoorOnTailHoldBreak = false)
            => EvaluatePressAction(windows, timeOffset, state, poorEnabled, checkCanRouteOnPress: true, forcePoorOnTailHoldBreak: forcePoorOnTailHoldBreak);

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
            => toSessionOutcome(evaluatePressCore(windows, timeOffset, state, poorEnabled, autoMiss: false, forcePoorOnTailHoldBreak: false, checkCanRouteOnPress: false), state);

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
            if (hitEvent.HitObject is TailNote)
            {
                var tailOutcome = EvaluatePress(hitEvent.TimeOffset, hitWindows);
                return tailOutcome.Kind == ManiaNoteJudgementOutcomeKind.Apply ? tailOutcome.Result : HitResult.Miss;
            }

            var outcome = EvaluatePress(hitEvent.TimeOffset, hitWindows);
            // BMS KPoor（DispatchExtra）在重判场景无跨物体路由可用，视为 Miss
            return outcome.Kind == ManiaNoteJudgementOutcomeKind.Apply ? outcome.Result : HitResult.Miss;
        }

        private static bool shouldAutoMiss(ManiaHitWindows windows, double timeOffset, double badLate)
            => windows.ResultFor(timeOffset) == HitResult.None && IsLateOutsideBad(timeOffset, badLate);

        private enum BmsPressCoreKind
        {
            None,
            Ignore,
            ApplyFinal,
            DispatchExtra,
        }

        private readonly struct BmsPressCoreResult
        {
            public BmsPressCoreKind Kind { get; init; }

            public BmsJudge Judge { get; init; }

            public bool? SetCanRouteToKPoor { get; init; }

            public bool? SetHasLateKPoor { get; init; }

            public bool ClearCanRouteToKPoor { get; init; }
        }

        private static BmsPressCoreResult evaluatePressCore(
            ManiaHitWindows windows,
            double timeOffset,
            BmsRouteState? state,
            bool poorEnabled,
            bool autoMiss,
            bool forcePoorOnTailHoldBreak,
            bool checkCanRouteOnPress)
        {
            double badLate = WindowFor(windows, BmsJudge.Bad, false);

            if (autoMiss)
            {
                if (shouldAutoMiss(windows, timeOffset, badLate))
                    return new BmsPressCoreResult { Kind = BmsPressCoreKind.ApplyFinal, Judge = BmsJudge.Poor };

                return new BmsPressCoreResult { Kind = BmsPressCoreKind.Ignore };
            }

            if (checkCanRouteOnPress && state != null && poorEnabled && state.CanRouteToKPoor)
                return new BmsPressCoreResult { Kind = BmsPressCoreKind.DispatchExtra, Judge = BmsJudge.KPoor, ClearCanRouteToKPoor = true };

            var judge = resolvePressedJudge(windows, timeOffset, badLate);

            if (judge == BmsJudge.None)
                return new BmsPressCoreResult { Kind = BmsPressCoreKind.Ignore };

            if (forcePoorOnTailHoldBreak)
                judge = BmsJudge.Poor;

            if (judge == BmsJudge.KPoor)
            {
                if (!poorEnabled)
                    return new BmsPressCoreResult { Kind = BmsPressCoreKind.Ignore };

                bool isLatePress = IsLateOutsideBad(timeOffset, badLate);

                if (state != null && isLatePress && state.HasLateKPoor)
                    return new BmsPressCoreResult { Kind = BmsPressCoreKind.None };

                return new BmsPressCoreResult
                {
                    Kind = BmsPressCoreKind.DispatchExtra,
                    Judge = BmsJudge.KPoor,
                    SetHasLateKPoor = isLatePress,
                };
            }

            return new BmsPressCoreResult
            {
                Kind = BmsPressCoreKind.ApplyFinal,
                Judge = judge,
                SetCanRouteToKPoor = judge == BmsJudge.Bad && timeOffset < 0 && poorEnabled,
            };
        }

        private static DrawableAction toDrawableAction(BmsPressCoreResult core)
        {
            switch (core.Kind)
            {
                case BmsPressCoreKind.Ignore:
                    return DrawableAction.Ignore;

                case BmsPressCoreKind.ApplyFinal:
                    return DrawableAction.Final(core.Judge, setCanRouteToKPoor: core.SetCanRouteToKPoor);

                case BmsPressCoreKind.DispatchExtra:
                    return DrawableAction.Extra(core.Judge, setHasLateKPoor: core.SetHasLateKPoor, clearCanRouteToKPoor: core.ClearCanRouteToKPoor);

                default:
                    return DrawableAction.NotHandled;
            }
        }

        private static SessionPressOutcome toSessionOutcome(BmsPressCoreResult core, BmsRouteState state)
        {
            switch (core.Kind)
            {
                case BmsPressCoreKind.ApplyFinal:
                    return new SessionPressOutcome
                    {
                        Kind = SessionPressKind.ApplyFinal,
                        Judge = core.Judge,
                        EnableCanRouteToKPoor = core.SetCanRouteToKPoor == true,
                    };

                case BmsPressCoreKind.DispatchExtra:
                    if (core.SetHasLateKPoor == true)
                        state.HasLateKPoor = true;

                    return new SessionPressOutcome { Kind = SessionPressKind.DispatchExtra, Judge = core.Judge };

                default:
                    return default;
            }
        }

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
