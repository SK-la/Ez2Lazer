// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    internal static class ManiaEzDrawableJudgement
    {
        private static readonly ConditionalWeakTable<DrawableNote, BmsHitModeJudgement.BmsRouteState> note_bms_states =
            new ConditionalWeakTable<DrawableNote, BmsHitModeJudgement.BmsRouteState>();

        private static readonly ConditionalWeakTable<DrawableHoldNoteTail, BmsHitModeJudgement.BmsRouteState> tail_bms_states =
            new ConditionalWeakTable<DrawableHoldNoteTail, BmsHitModeJudgement.BmsRouteState>();

        private static readonly ConditionalWeakTable<DrawableHoldNote, O2HitModeJudgement.HoldBreakState> o2_hold_states =
            new ConditionalWeakTable<DrawableHoldNote, O2HitModeJudgement.HoldBreakState>();

        public static bool CanRouteToKPoor(DrawableNote note) => GetBmsState(note).CanRouteToKPoor;

        public static bool CanRouteToKPoor(DrawableHoldNoteTail tail) => GetBmsState(tail).CanRouteToKPoor;

        public static bool ShouldHideTailDisplayResult(ManiaJudgementRound? round)
            => round?.Environment.ManiaHitMode == EzEnumHitMode.O2Jam;

        internal static BmsHitModeJudgement.BmsRouteState GetBmsState(DrawableNote note)
        {
            if (note.FindClosestParent<Column>() is Column column && column.TryGetBmsRoute(note, out var route))
                return route;

            return note_bms_states.GetValue(note, _ => new BmsHitModeJudgement.BmsRouteState());
        }

        internal static BmsHitModeJudgement.BmsRouteState GetBmsState(DrawableHoldNoteTail tail)
            => tail_bms_states.GetValue(tail, _ => new BmsHitModeJudgement.BmsRouteState());

        internal static ManiaJudgementRound GetJudgementRound(DrawableHitObject? drawable = null)
            => getJudgementRound(drawable);

        private static ManiaJudgementRound getJudgementRound(DrawableHitObject? drawable = null)
        {
            if (drawable is DrawableManiaHitObject maniaDrawable
                && maniaDrawable.EzDrawableManiaRuleset?.JudgementRound is { } cachedRound)
            {
                return cachedRound;
            }

            if (drawable?.FindClosestParent<DrawableManiaRuleset>() is { JudgementRound: { } round })
                return round;

            var ruleset = drawable?.FindClosestParent<DrawableRuleset>();
            var env = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForLive, ruleset?.ReplayScore?.ScoreInfo, ignoreOffset: ruleset?.ReplayScore != null);
            return ManiaJudgementRound.Create(env);
        }

        internal static bool TryMalodyHoldOnReleased(DrawableHoldNote hold)
        {
            var round = getJudgementRound(hold);

            if (!MalodyHitModeJudgement.IsMalodyMode(round.Environment.ManiaHitMode))
                return false;

            if (!hold.IsHolding.Value)
                return false;

            hold.Tail.UpdateResult();
            hold.EzTriggerMalodyBodyOnRelease();
            hold.EzReportHoldReleased();
            return true;
        }

        internal static bool TryMalodyHoldCheckForResult(DrawableHoldNote hold, bool userTriggered, double timeOffset)
        {
            var round = getJudgementRound(hold);

            if (!MalodyHitModeJudgement.IsMalodyMode(round.Environment.ManiaHitMode))
                return false;

            if (!hold.Tail.AllJudged)
                return false;

            hold.EzFinalizeMalodyHoldFromTail();
            return true;
        }

        internal static bool TryHitModeCheckForResult(DrawableNote note, bool userTriggered, double timeOffset)
            => TryApplyEzNoteCheckForResult(note, userTriggered, timeOffset);

        internal static bool TryHoldTailCheckForResult(DrawableHoldNoteTail tail, bool userTriggered, double timeOffset)
            => TryApplyEzHoldTailCheckForResult(tail, userTriggered, timeOffset);

        internal static bool TryBmsOnPressed(DrawableNote note, KeyBindingPressEvent<ManiaAction> e)
        {
            if (note.HitObject.HitWindows is not ManiaHitWindows maniaWindows)
                return false;

            var round = getJudgementRound(note);
            var state = GetBmsState(note);
            var action = BmsHitModeJudgement.Instance.TryPostBadOnPressed(maniaWindows, state, round.PoorEnabled);

            if (!action.Handled)
                return false;

            ApplyBmsAction(note, round, action, state);
            return true;
        }

        internal static void TryO2HoldUpdate(DrawableHoldNote hold)
        {
            // 非 O2：避免每帧走 getJudgementRound（无缓存时还会 Create）。
            if (hold.EzDrawableManiaRuleset?.JudgementRound is not { IsO2Jam: true })
                return;

            var state = o2_hold_states.GetValue(hold, _ => new O2HitModeJudgement.HoldBreakState());
            O2HitModeJudgement.Instance.ApplyDrawableHoldBreakUpdate(hold, state);
        }

        internal static bool TryO2HoldCheckForResult(DrawableHoldNote hold, bool userTriggered, double timeOffset)
        {
            var round = getJudgementRound(hold);

            if (round.Environment.ManiaHitMode != EzEnumHitMode.O2Jam)
                return false;

            return O2HitModeJudgement.Instance.TryO2HoldCheckForResult(hold, userTriggered, timeOffset);
        }

        internal static void ApplyNoteOutcome(DrawableNote drawable, ManiaJudgementRound round, ManiaNoteJudgementOutcome outcome, bool userTriggered = true)
        {
            switch (outcome.Kind)
            {
                case ManiaNoteJudgementOutcomeKind.Apply:
                    if (!userTriggered && outcome.Result == HitResult.Miss)
                        drawable.EzApplyPassiveMissWithStoredOffset();
                    else
                        drawable.EzApplyFinalResult(outcome.Result, round.Environment.ManiaHitMode);

                    break;

                case ManiaNoteJudgementOutcomeKind.DispatchExtra:
                    drawable.EzDispatchExtraResult(outcome.Result);
                    break;
            }
        }

        internal static void ApplyBmsAction(DrawableNote drawable, ManiaJudgementRound round, BmsHitModeJudgement.DrawableAction action, BmsHitModeJudgement.BmsRouteState state)
            => applyBmsAction(drawable, r => drawable.EzApplyFinalResult(r, round.Environment.ManiaHitMode), drawable.EzDispatchExtraResult, action, state);

        internal static void ApplyBmsAction(DrawableHoldNoteTail drawable, ManiaJudgementRound round, BmsHitModeJudgement.DrawableAction action, BmsHitModeJudgement.BmsRouteState state)
            => applyBmsAction(drawable, r => drawable.EzApplyFinalResult(r, round.Environment.ManiaHitMode), drawable.EzDispatchExtraResult, action, state);

        private static void applyBmsAction<T>(
            T _,
            Action<HitResult> applyFinal,
            Action<HitResult> dispatchExtra,
            BmsHitModeJudgement.DrawableAction action,
            BmsHitModeJudgement.BmsRouteState state)
        {
            if (!action.Handled)
                return;

            BmsHitModeJudgement.ApplyRouteState(state, action);

            var result = BmsHitModeJudgement.MapTo(action.Judge);

            if (action.DispatchExtra)
                dispatchExtra(result);
            else if (action.ApplyFinal)
                applyFinal(result);
        }

        internal static bool TryColumnHoldTailRelease(DrawableHoldNote hold, double currentTime, ManiaJudgementRound round)
        {
            if (!hold.IsHolding.Value)
                return false;

            if (TryMalodyHoldOnReleased(hold))
                return true;

            hold.Tail.UpdateResult();
            hold.EzTriggerBodyAfterTailRelease();
            hold.Result.ReportHoldState(currentTime, false);
            return true;
        }

        internal static bool TryApplyEzNoteCheckForResult(DrawableNote drawable, bool userTriggered, double timeOffset)
        {
            ManiaJudgeHotPathTrace.RecordCheckForResult();

            var round = getJudgementRound(drawable);

            // UpdateResult(false) 已通过 ShouldDeferAutoMissUpdate；勿再调 Gate（曾每帧 WindowFor）。
            // 用户按键路径仍可能走 UpdateResult(true)，Gate 不适用。

            if (!round.IsEzHitMode || round.Strategy == null)
                return false;

            var bmsState = round.Strategy is BmsHitModeJudgement ? GetBmsState(drawable) : null;

            var result = ManiaJudgementKernel.EvaluateNote(new ManiaJudgementKernel.NoteEvaluationRequest
            {
                Round = round,
                TimeOffset = timeOffset,
                HitWindows = drawable.HitObject.HitWindows!,
                UserTriggered = userTriggered,
                IsLnHead = drawable.HitObject is HeadNote,
                EventTime = drawable.Time.Current,
                FrameStableId = getFrameStableId(drawable),
                BmsState = bmsState,
                CheckBmsCanRouteOnPress = userTriggered,
            });

            publishO2RuntimeState(round);
            return applyNoteEvaluation(drawable, round, result, bmsState, userTriggered);
        }

        internal static bool TryApplyEzHoldTailCheckForResult(DrawableHoldNoteTail drawable, bool userTriggered, double timeOffset)
        {
            ManiaJudgeHotPathTrace.RecordCheckForResult();

            var round = getJudgementRound(drawable);

            // 同 Note：automiss 已在 ShouldDefer 过滤。

            if (!round.IsEzHitMode)
                return false;

            var bmsState = round.Strategy is BmsHitModeJudgement ? GetBmsState(drawable) : null;

            var result = ManiaJudgementKernel.EvaluateHoldTail(new ManiaJudgementKernel.HoldTailEvaluationRequest
            {
                Round = round,
                TimeOffset = timeOffset,
                RawOffset = timeOffset,
                HitWindows = drawable.HitObject.HitWindows!,
                UserTriggered = userTriggered,
                HeadHit = drawable.HoldNote.Head.IsHit,
                HoldBroken = drawable.HoldNote.Body.HasHoldBreak,
                WasHolding = drawable.HoldNote.IsHolding.Value,
                HasHoldBreak = drawable.HoldNote.Body.HasHoldBreak,
                EventTime = drawable.Time.Current,
                FrameStableId = getFrameStableId(drawable),
                BmsState = bmsState,
            });

            publishO2RuntimeState(round);

            if (!result.Handled)
                return false;

            if (result.ApplyMinResult)
            {
                if (!userTriggered)
                    drawable.EzApplyPassiveMissWithStoredOffset();
                else
                    drawable.EzApplyMinResult();

                return true;
            }

            if (result.BmsAction.Handled)
            {
                if (!userTriggered && result.BmsAction.ApplyFinal)
                    applyBmsAutoMissFinal(drawable, round, result.BmsAction, bmsState!);
                else
                    ApplyBmsAction(drawable, round, result.BmsAction, bmsState!);

                return true;
            }

            if (result.FinalResult != null)
            {
                drawable.EzApplyFinalResult(result.FinalResult.Value, round.Environment.ManiaHitMode);
                return true;
            }

            return true;
        }

        private static bool applyNoteEvaluation(
            DrawableNote drawable,
            ManiaJudgementRound round,
            ManiaJudgementKernel.NoteEvaluationResult result,
            BmsHitModeJudgement.BmsRouteState? bmsState,
            bool userTriggered)
        {
            switch (result.Kind)
            {
                case ManiaJudgementKernel.NoteEvaluationKind.NotHandled:
                    return false;

                case ManiaJudgementKernel.NoteEvaluationKind.Ignore:
                    return true;

                case ManiaJudgementKernel.NoteEvaluationKind.ApplyBmsAction:
                    if (!userTriggered && result.BmsAction.ApplyFinal)
                        applyBmsAutoMissFinal(drawable, round, result.BmsAction, bmsState!);
                    else
                        ApplyBmsAction(drawable, round, result.BmsAction, bmsState!);

                    return true;

                case ManiaJudgementKernel.NoteEvaluationKind.ApplyNoteOutcome:
                    ApplyNoteOutcome(drawable, round, result.NoteOutcome, userTriggered);
                    return true;

                default:
                    return true;
            }
        }

        private static void applyBmsAutoMissFinal(
            DrawableManiaHitObject drawable,
            ManiaJudgementRound round,
            BmsHitModeJudgement.DrawableAction action,
            BmsHitModeJudgement.BmsRouteState state)
        {
            if (!action.Handled || !action.ApplyFinal)
                return;

            BmsHitModeJudgement.ApplyRouteState(state, action);
            var result = BmsHitModeJudgement.MapTo(action.Judge);
            drawable.EzApplyBmsAutoMissFinalWithStoredOffset(result, round.Environment.ManiaHitMode);
        }

        private static long getFrameStableId(DrawableHitObject drawable)
            => (long)(drawable.Time.Current * 1000);

        private static void publishO2RuntimeState(ManiaJudgementRound round)
        {
            if (!round.IsO2Jam)
                return;

            O2HitModeExtension.PILL_COUNT.Value = round.MutableState.O2PillCount;
            O2HitModeExtension.CoolCombo = round.MutableState.O2CoolCombo;
        }
    }
}
