// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Scoring;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
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

        private static readonly ConditionalWeakTable<DrawableRuleset, ManiaReplayJudgementState> o2_judgement_states =
            new ConditionalWeakTable<DrawableRuleset, ManiaReplayJudgementState>();

        public static bool CanRouteToKPoor(DrawableNote note) => GetBmsState(note).CanRouteToKPoor;

        public static bool CanRouteToKPoor(DrawableHoldNoteTail tail) => GetBmsState(tail).CanRouteToKPoor;

        public static bool ShouldHideTailDisplayResult()
        {
            var environment = getGameplayEnvironment();
            return environment.ManiaHitMode == EzEnumHitMode.O2Jam;
        }

        internal static BmsHitModeJudgement.BmsRouteState GetBmsState(DrawableNote note)
            => note_bms_states.GetValue(note, _ => new BmsHitModeJudgement.BmsRouteState());

        internal static BmsHitModeJudgement.BmsRouteState GetBmsState(DrawableHoldNoteTail tail)
            => tail_bms_states.GetValue(tail, _ => new BmsHitModeJudgement.BmsRouteState());

        private static ManiaReplayJudgementState getO2JudgementState(DrawableHitObject drawable)
        {
            var ruleset = drawable.FindClosestParent<DrawableRuleset>();

            if (ruleset == null)
                return new ManiaReplayJudgementState();

            return o2_judgement_states.GetValue(ruleset, _ => new ManiaReplayJudgementState());
        }

        private static ManiaGameplayEnvironment getGameplayEnvironment(DrawableHitObject? drawable = null)
        {
            var ruleset = drawable?.FindClosestParent<DrawableRuleset>();

            if (ruleset?.ReplayScore?.ScoreInfo != null)
                return ManiaRuleset.ResolveEnvironment(ruleset.ReplayScore.ScoreInfo, GlobalConfigStore.EzConfig, ManiaReplayRunPurpose.ForStoredStatistics);

            return ManiaRuleset.ResolveEnvironment(null, GlobalConfigStore.EzConfig, ManiaReplayRunPurpose.ForLiveAnalysis);
        }

        internal static bool TryMalodyHoldOnReleased(DrawableHoldNote hold)
        {
            var environment = getGameplayEnvironment(hold);

            if (!MalodyHitModeJudgement.IsMalodyMode(environment.ManiaHitMode))
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
            var environment = getGameplayEnvironment(hold);

            if (!MalodyHitModeJudgement.IsMalodyMode(environment.ManiaHitMode))
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

            var state = GetBmsState(note);
            var action = BmsHitModeJudgement.Instance.TryPostBadOnPressed(maniaWindows, state);

            if (!action.Handled)
                return false;

            ApplyBmsAction(note, action, state);
            return true;
        }

        internal static void TryO2HoldUpdate(DrawableHoldNote hold)
        {
            var environment = getGameplayEnvironment(hold);

            if (environment.ManiaHitMode != EzEnumHitMode.O2Jam)
                return;

            var state = o2_hold_states.GetValue(hold, _ => new O2HitModeJudgement.HoldBreakState());
            O2HitModeJudgement.Instance.ApplyDrawableHoldBreakUpdate(hold, state);
        }

        internal static bool TryO2HoldCheckForResult(DrawableHoldNote hold, bool userTriggered, double timeOffset)
        {
            var environment = getGameplayEnvironment(hold);

            if (environment.ManiaHitMode != EzEnumHitMode.O2Jam)
                return false;

            return O2HitModeJudgement.Instance.TryO2HoldCheckForResult(hold, userTriggered, timeOffset);
        }

        internal static void ApplyNoteOutcome(DrawableNote drawable, ManiaNoteJudgementOutcome outcome)
        {
            switch (outcome.Kind)
            {
                case ManiaNoteJudgementOutcomeKind.Apply:
                    drawable.EzApplyFinalResult(outcome.Result);
                    break;

                case ManiaNoteJudgementOutcomeKind.DispatchExtra:
                    drawable.EzDispatchExtraResult(outcome.Result);
                    break;
            }
        }

        internal static void ApplyBmsAction(DrawableNote drawable, BmsHitModeJudgement.DrawableAction action, BmsHitModeJudgement.BmsRouteState state)
            => applyBmsAction(drawable, drawable.EzApplyFinalResult, drawable.EzDispatchExtraResult, action, state);

        internal static void ApplyBmsAction(DrawableHoldNoteTail drawable, BmsHitModeJudgement.DrawableAction action, BmsHitModeJudgement.BmsRouteState state)
            => applyBmsAction(drawable, drawable.EzApplyFinalResult, drawable.EzDispatchExtraResult, action, state);

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

        internal static bool TryApplyEzNoteCheckForResult(DrawableNote drawable, bool userTriggered, double timeOffset)
        {
            var environment = getGameplayEnvironment(drawable);

            if (!ManiaJudgementRegistry.IsEzHitMode(environment.ManiaHitMode))
                return false;

            var hitMode = ManiaJudgementRegistry.GetHitModeJudgement(environment.ManiaHitMode);
            if (hitMode == null)
                return false;

            if (hitMode is BmsHitModeJudgement bms)
            {
                if (drawable.HitObject.HitWindows is not ManiaHitWindows maniaWindows)
                    return true;

                var state = GetBmsState(drawable);

                var action = userTriggered
                    ? bms.EvaluateDrawablePress(maniaWindows, timeOffset, state)
                    : bms.EvaluateDrawableAutoMiss(maniaWindows, timeOffset);

                ApplyBmsAction(drawable, action, state);
                return true;
            }

            if (hitMode is O2HitModeJudgement o2)
            {
                (drawable.HitObject.HitWindows as ManiaHitWindows)?.UpdateO2JamBpmFromTime(drawable.Time.Current);

                if (userTriggered)
                {
                    bool cont = O2HitModeExtension.PillCheck(timeOffset, drawable.Time.Current, out bool _, out bool upgradeToPerfect);
                    var outcome = o2.EvaluateDrawableNotePress(timeOffset, drawable.HitObject.HitWindows!, new O2HitModeJudgement.DrawableNoteContext
                    {
                        CurrentTime = drawable.Time.Current,
                        PillCheckPassed = cont,
                        UpgradeToPerfect = upgradeToPerfect,
                    }, getO2JudgementState(drawable));

                    if (outcome != null)
                        ApplyNoteOutcome(drawable, outcome.Value);

                    return true;
                }

                ApplyNoteOutcome(drawable, o2.EvaluateAutoMiss(timeOffset, drawable.HitObject.HitWindows!));
                return true;
            }

            if (hitMode is Ez2AcHitModeJudgement ez2Ac)
            {
                if (userTriggered)
                {
                    ApplyNoteOutcome(drawable, ez2Ac.EvaluateDrawablePress(timeOffset, drawable.HitObject.HitWindows!, drawable.HitObject is HeadNote));
                    return true;
                }

                ApplyNoteOutcome(drawable, ez2Ac.EvaluateAutoMiss(timeOffset, drawable.HitObject.HitWindows!));
                return true;
            }

            if (userTriggered)
            {
                ApplyNoteOutcome(drawable, hitMode.EvaluatePress(timeOffset, drawable.HitObject.HitWindows!));
                return true;
            }

            ApplyNoteOutcome(drawable, hitMode.EvaluateAutoMiss(timeOffset, drawable.HitObject.HitWindows!));
            return true;
        }

        internal static bool TryApplyEzHoldTailCheckForResult(DrawableHoldNoteTail drawable, bool userTriggered, double timeOffset)
        {
            var environment = getGameplayEnvironment(drawable);

            if (!ManiaJudgementRegistry.IsEzHitMode(environment.ManiaHitMode))
                return false;

            var hitMode = ManiaJudgementRegistry.GetHitModeJudgement(environment.ManiaHitMode);

            if (hitMode is MalodyHitModeJudgement)
            {
                if (!userTriggered && timeOffset >= 0)
                    drawable.EzApplyFinalResult(HitResult.IgnoreHit);

                return true;
            }

            if (hitMode is BmsHitModeJudgement bms)
            {
                if (drawable.HitObject.HitWindows is not ManiaHitWindows maniaWindows)
                    return true;

                var state = GetBmsState(drawable);
                bool forcePoor = !drawable.HoldNote.IsHolding.Value && drawable.HoldNote.Body.HasHoldBreak;

                var action = userTriggered
                    ? bms.EvaluateDrawablePress(maniaWindows, timeOffset, state, forcePoorOnTailHoldBreak: forcePoor)
                    : bms.EvaluateDrawableAutoMiss(maniaWindows, timeOffset);

                ApplyBmsAction(drawable, action, state);
                return true;
            }

            if (hitMode is O2HitModeJudgement o2)
            {
                (drawable.HitObject.HitWindows as ManiaHitWindows)?.UpdateO2JamBpmFromTime(drawable.Time.Current);

                if (!userTriggered)
                {
                    if (drawable.HoldNote.Body.HasHoldBreak)
                    {
                        if (timeOffset < 0)
                            return true;

                        drawable.EzApplyFinalResult(O2HitModeJudgement.MapTo(O2Judge.Miss));
                        return true;
                    }

                    if (!drawable.HitObject.HitWindows!.CanBeHit(timeOffset))
                        drawable.EzApplyMinResult();

                    return true;
                }

                bool cont = O2HitModeExtension.PillCheck(timeOffset, drawable.Time.Current, out bool _, out bool upgradeToPerfect);
                var result = o2.EvaluateDrawableTailPress(timeOffset, drawable.HitObject.HitWindows!, new O2HitModeJudgement.DrawableTailContext
                {
                    CurrentTime = drawable.Time.Current,
                    PillCheckPassed = cont,
                    UpgradeToPerfect = upgradeToPerfect,
                    HeadHit = drawable.HoldNote.Head.IsHit,
                    HoldBroken = drawable.HoldNote.Body.HasHoldBreak,
                    WasHolding = drawable.HoldNote.IsHolding.Value,
                    PillModeEnabled = environment.ManiaHealthMode.ToString().Contains("O2Jam"),
                }, getO2JudgementState(drawable));

                if (result != null)
                    drawable.EzApplyFinalResult(result.Value);

                return true;
            }

            if (hitMode is Ez2AcHitModeJudgement ez2Ac)
            {
                bool headMissOrBreak = !drawable.HoldNote.Head.IsHit || drawable.HoldNote.Body.HasHoldBreak;

                if (!userTriggered)
                {
                    if (timeOffset < 0)
                        return true;

                    if (headMissOrBreak)
                    {
                        if (!drawable.HitObject.HitWindows!.CanBeHit(timeOffset))
                            drawable.EzApplyMinResult();

                        return true;
                    }
                }

                var tailJudge = ez2Ac.EvaluateTailJudge(new HoldTailEvaluationContext
                {
                    RawOffset = timeOffset,
                    TimeOffsetForJudgement = timeOffset,
                    HitWindows = drawable.HitObject.HitWindows!,
                    HeadHit = drawable.HoldNote.Head.IsHit,
                    HoldBreak = ez2Ac.IsHoldBreak(timeOffset, drawable.HitObject.HitWindows!),
                    HoldBroken = drawable.HoldNote.Body.HasHoldBreak,
                    WasHoldingBeforeRelease = drawable.HoldNote.IsHolding.Value,
                });

                if (tailJudge == Ez2AcJudge.None)
                {
                    if (!userTriggered && !drawable.HitObject.HitWindows!.CanBeHit(timeOffset))
                        drawable.EzApplyMinResult();

                    return true;
                }

                drawable.EzApplyFinalResult(Ez2AcHitModeJudgement.MapTo(tailJudge));
                return true;
            }

            if (hitMode == null)
                return false;

            var genericResult = hitMode.EvaluateTail(new HoldTailEvaluationContext
            {
                RawOffset = timeOffset,
                TimeOffsetForJudgement = timeOffset,
                HitWindows = drawable.HitObject.HitWindows!,
                HeadHit = drawable.HoldNote.Head.IsHit,
                HoldBreak = hitMode.IsHoldBreak(timeOffset, drawable.HitObject.HitWindows!),
                HoldBroken = drawable.HoldNote.Body.HasHoldBreak,
                WasHoldingBeforeRelease = drawable.HoldNote.IsHolding.Value,
            });

            if (genericResult == HitResult.None)
                return true;

            drawable.EzApplyFinalResult(genericResult);
            return true;
        }
    }
}
