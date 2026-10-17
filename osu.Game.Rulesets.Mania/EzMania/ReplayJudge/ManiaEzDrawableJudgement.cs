// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Judgements;
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
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    internal static class ManiaEzDrawableJudgement
    {
        private static readonly ConditionalWeakTable<DrawableNote, BmsJudgeModeJudgement.BmsRouteState> note_bms_states =
            new ConditionalWeakTable<DrawableNote, BmsJudgeModeJudgement.BmsRouteState>();

        private static readonly ConditionalWeakTable<DrawableHoldNoteTail, BmsJudgeModeJudgement.BmsRouteState> tail_bms_states =
            new ConditionalWeakTable<DrawableHoldNoteTail, BmsJudgeModeJudgement.BmsRouteState>();

        private static readonly ConditionalWeakTable<DrawableHoldNote, O2JudgeModeJudgement.HoldBreakState> o2_hold_states =
            new ConditionalWeakTable<DrawableHoldNote, O2JudgeModeJudgement.HoldBreakState>();

        private static readonly ConditionalWeakTable<DrawableHoldNote, Ez2AcHoldState> ez2ac_hold_states =
            new ConditionalWeakTable<DrawableHoldNote, Ez2AcHoldState>();

        private static readonly ConditionalWeakTable<DrawableHoldNote, StableClassicHoldState> stable_classic_hold_states =
            new ConditionalWeakTable<DrawableHoldNote, StableClassicHoldState>();

        public static bool CanRouteToKPoor(DrawableNote note) => GetBmsState(note).CanRouteToKPoor;

        public static bool CanRouteToKPoor(DrawableHoldNoteTail tail) => GetBmsState(tail).CanRouteToKPoor;

        /// <summary>
        /// 应用判定结果前的区间校验。判定区间来自 <see cref="osu.Game.Rulesets.Objects.HitObject.Judgement"/> 的绑定，
        /// 若该实例被异 JudgeMode 绑定改写过（见 <see cref="ManiaBeatmapBinding"/>），内核给出的结果会落在区间外，
        /// 直接交给 <c>DrawableHitObject.ApplyResult</c> 会抛 <see cref="InvalidOperationException"/> 中断整局。
        /// 这里降级为「不计分的合法结果」并计数告警——仿真已在独立副本上判定，这里是 Release 的最后一道兜底，
        /// 命中即说明上游绑定出了问题，故计数不受诊断开关控制。
        /// </summary>
        internal static HitResult SanitizeResult(DrawableManiaHitObject drawable, HitResult result)
            => SanitizeResult(drawable.Result?.Judgement, result, drawable.EzDrawableManiaRuleset?.JudgementRound?.Environment.JudgeMode);

        /// <summary>区间校验与降级决策本体（与 Drawable 解耦，便于单测）。</summary>
        internal static HitResult SanitizeResult(Judgement? judgement, HitResult result, EzEnumJudgeMode? judgeMode)
        {
            if (judgement == null || result.IsValidHitResult(judgement.MinResult, judgement.MaxResult))
                return result;

            var downgraded = HitResult.IgnoreHit.IsValidHitResult(judgement.MinResult, judgement.MaxResult)
                ? HitResult.IgnoreHit
                : judgement.MinResult;

            ManiaJudgeHotPathTrace.RecordJudgementResultDowngrade();

            Logger.Log($"[ManiaJudgeBinding] result {result} outside [{judgement.MinResult} ... {judgement.MaxResult}] "
                       + $"(judgement={judgement.GetType().ReadableName()}, JudgeMode={judgeMode?.ToString() ?? "unknown"}); downgraded to {downgraded}", Ez2ConfigManager.LOGGER_NAME);

            return downgraded;
        }

        public static bool ShouldHideTailDisplayResult(ManiaJudgementRound? round)
            => round?.Environment.JudgeMode == EzEnumJudgeMode.O2Jam;

        internal static BmsJudgeModeJudgement.BmsRouteState GetBmsState(DrawableNote note)
        {
            if (note.FindClosestParent<Column>() is Column column && column.TryGetBmsRoute(note, out var route))
                return route;

            return note_bms_states.GetValue(note, _ => new BmsJudgeModeJudgement.BmsRouteState());
        }

        internal static BmsJudgeModeJudgement.BmsRouteState GetBmsState(DrawableHoldNoteTail tail)
            => tail_bms_states.GetValue(tail, _ => new BmsJudgeModeJudgement.BmsRouteState());

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
            var env = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForLive, ruleset?.ReplayScore?.ScoreInfo, ignoreOffset: ruleset?.ReplayScore != null, ruleset: ruleset?.Ruleset.RulesetInfo);
            return ManiaJudgementRound.Create(env);
        }

        internal static bool TryMalodyHoldOnReleased(DrawableHoldNote hold)
        {
            var round = getJudgementRound(hold);

            if (!MalodyJudgeModeJudgement.IsMalodyMode(round.Environment.JudgeMode))
                return false;

            if (!hold.IsHolding.Value)
                return false;

            hold.Tail.UpdateResult();
            hold.EzTriggerMalodyBodyOnRelease();
            hold.EzReportHoldReleased();
            hold.EzTryFinalizeAfterTailJudged();
            return true;
        }

        internal static bool TryMalodyHoldCheckForResult(DrawableHoldNote hold, bool userTriggered, double timeOffset)
        {
            var round = getJudgementRound(hold);

            if (!MalodyJudgeModeJudgement.IsMalodyMode(round.Environment.JudgeMode))
                return false;

            if (!hold.Tail.AllJudged)
                return false;

            hold.EzFinalizeMalodyHoldFromTail();
            return true;
        }

        internal static bool TryEz2AcHoldOnReleased(DrawableHoldNote hold)
        {
            var round = getJudgementRound(hold);

            if (round.Environment.JudgeMode != EzEnumJudgeMode.EZ2AC)
                return false;

            if (!hold.IsHolding.Value)
                return false;

            var state = GetEz2AcHoldState(hold);
            state.OnRelease();
            hold.Result.ReportHoldState(hold.Time.Current, false);

            // 尾到达后才完结；中途松手可再抓，不判尾、不断 Body。
            if (hold.Time.Current >= hold.Tail.HitObject.StartTime)
            {
                hold.Tail.UpdateResult();
                if (!hold.Body.AllJudged)
                    hold.Body.TriggerResult(true);
                hold.EzTryFinalizeAfterTailJudged();
            }

            return true;
        }

        internal static bool TryEz2AcHoldCheckForResult(DrawableHoldNote hold, bool userTriggered, double timeOffset)
        {
            var round = getJudgementRound(hold);

            if (round.Environment.JudgeMode != EzEnumJudgeMode.EZ2AC)
                return false;

            if (!hold.Tail.AllJudged)
                return false;

            hold.EzFinalizeEz2AcHoldFromTail();
            return true;
        }

        internal static bool TryHoldTickCheckForResult(DrawableHoldNoteTick tick, bool userTriggered, double timeOffset)
        {
            if (userTriggered)
                return true;

            if (timeOffset < 0)
                return true;

            if (tick.HoldNote is not DrawableHoldNote hold)
                return false;

            var round = getJudgementRound(tick);

            if (round.Environment.JudgeMode != EzEnumJudgeMode.EZ2AC)
                return false;

            var state = GetEz2AcHoldState(hold);
            bool holding = hold.IsHolding.Value;
            var result = Ez2AcJudgeModeJudgement.Instance.EvaluateTick(state, holding);
            tick.EzApplyTickResult(result);
            return true;
        }

        internal static Ez2AcHoldState GetEz2AcHoldState(DrawableHoldNote hold)
            => ez2ac_hold_states.GetValue(hold, _ => new Ez2AcHoldState());

        /// <summary>
        /// stable classic 的 per-LN 持有态：尾判需要「最近按下 / 首次有效松开 / 断连」，同一条 LN 只出一个档位。
        /// </summary>
        /// <remarks>
        /// 状态按 <see cref="DrawableHoldNote"/> 实例缓存，而该实例由对象池复用、会顺序承载多条 LN，
        /// 故每次取用时按谱面条目换绑（见 <see cref="StableClassicHoldState.EnsureBoundTo"/>）。
        /// </remarks>
        internal static StableClassicHoldState GetStableClassicHoldState(DrawableHoldNote hold)
        {
            var state = stable_classic_hold_states.GetValue(
                hold,
                _ => new StableClassicHoldState(hold.HitObject.StartTime, hold.HitObject.EndTime));

            if (hold.Entry is { } entry)
                state.EnsureBoundTo(entry, hold.HitObject.StartTime, hold.HitObject.EndTime);

            return state;
        }

        /// <summary>
        /// stable classic：松手。只有不早于 <c>EndTime - HitWindow50</c> 的松手算有效松开，更早的只标断连。
        /// </summary>
        internal static void NotifyStableClassicReleased(DrawableHoldNote hold)
        {
            var round = getJudgementRound(hold);

            if (!StableClassicJudgeModeJudgement.IsSelected(round.Environment.JudgeMode))
                return;

            double releaseWindow = hold.Tail.HitObject.HitWindows?.WindowFor(HitResult.Meh) ?? 0;
            GetStableClassicHoldState(hold).OnReleased(hold.Time.Current, releaseWindow);
        }

        /// <summary>
        /// stable classic 长条持有期的连击增量：每 100ms 一枚，只推 combo，不加分、不进判定计数。
        /// </summary>
        /// <remarks>
        /// 局内与 <see cref="ManiaReplaySessionSimulator"/> 必须同源，否则总分只在 Session 侧多出增量。
        /// 帧钟与 <see cref="StableClassicHoldState"/> 共用；后退时直接返回，由 <c>ResetFromReplayFrame</c> 重建累加器。
        /// </remarks>
        internal static void TryStableClassicHoldComboTick(DrawableHoldNote hold)
        {
            if (hold.EzDrawableManiaRuleset is not { JudgementRound: { } round, StableClassicComboProcessor: { } processor })
                return;

            if (!StableClassicJudgeModeJudgement.IsSelected(round.Environment.JudgeMode))
                return;

            if ((hold.Clock as IGameplayClock)?.IsRewinding == true)
                return;

            if (GetStableClassicHoldState(hold).TryAdvanceHoldComboTick(hold.Time.Current))
                processor.ApplyClassicHoldCombo();
        }

        /// <summary>
        /// stable classic：按住不松时尾在 <c>EndTime + HitWindow50</c> 到点落判（该时刻把
        /// <c>TimeRelease</c> 写成当前时刻）。父 Hold 的收束仍交给 <c>DrawableHoldNote</c>。
        /// </summary>
        internal static void TryStableClassicSettleHeldTail(DrawableHoldNote hold)
        {
            // 每帧调用：只读已缓存的 round，未缓存时不做判定（与 TryO2HoldUpdate 同）。
            if (hold.EzDrawableManiaRuleset?.JudgementRound is not { } round
                || !StableClassicJudgeModeJudgement.IsSelected(round.Environment.JudgeMode))
                return;

            if (hold.Tail.AllJudged || !hold.IsHolding.Value)
                return;

            double settleTime = hold.Tail.HitObject.StartTime + (hold.Tail.HitObject.HitWindows?.WindowFor(HitResult.Meh) ?? 0);

            if (hold.Time.Current < settleTime)
                return;

            hold.Tail.UpdateResult();
            hold.EzTryFinalizeAfterTailJudged();
        }

        /// <summary>
        /// stable classic：过 <c>StartTime + HitWindow50</c> 仍从未按下时立即断连。
        /// </summary>
        /// <remarks>
        /// 只落 Body 的 ComboBreak 并置断连标志，尾判仍由尾结算负责，LN 可继续重抓。
        /// 已按下过（含已提前松手断连）时不补判，避免与松手路径重复发断连。
        /// </remarks>
        internal static void TryStableClassicHoldBreak(DrawableHoldNote hold)
        {
            // 每帧调用：只读已缓存的 round，未缓存时不做判定（与 TryStableClassicSettleHeldTail 同）。
            if (hold.EzDrawableManiaRuleset?.JudgementRound is not { } round
                || !StableClassicJudgeModeJudgement.IsSelected(round.Environment.JudgeMode))
                return;

            if (hold.Tail.AllJudged || hold.Body.AllJudged)
                return;

            var state = GetStableClassicHoldState(hold);

            if (state.TimePress != null || state.Pressed || state.HasHoldBreak)
                return;

            double breakTime = hold.Head.HitObject.StartTime + (hold.Tail.HitObject.HitWindows?.WindowFor(HitResult.Meh) ?? 0);

            if (hold.Time.Current < breakTime)
                return;

            // 实时持有态：同一次松手可能同时经 Column 与 Hold 结算，缓存 bindable 会读到旧值。
            if (hold.Result.IsHolding(hold.Time.Current))
                return;

            state.MarkHoldBreak();
            hold.Body.TriggerResult(false);
        }

        /// <summary>
        /// stable classic：头已判定后重按同一条 LN（断连后再抓）。按下分支只在未按住时进入，
        /// 因此重按会刷新 <c>TimePress</c>（尾判的按下基准）。
        /// </summary>
        internal static void NotifyStableClassicRepress(DrawableHoldNote hold)
        {
            var round = getJudgementRound(hold);

            if (!StableClassicJudgeModeJudgement.IsSelected(round.Environment.JudgeMode))
                return;

            GetStableClassicHoldState(hold).OnPressed(hold.Time.Current);
        }

        /// <summary>头判定应用后同步 EZ2AC LN 状态机。</summary>
        internal static void NotifyEz2AcHeadJudged(DrawableHoldNote hold, HitResult headResult, bool wasHoldingBeforePress)
        {
            var round = getJudgementRound(hold);

            if (round.Environment.JudgeMode != EzEnumJudgeMode.EZ2AC)
                return;

            var state = GetEz2AcHoldState(hold);
            var judge = Ez2AcJudgeModeJudgement.FromHitResult(headResult);
            state.OnHeadJudged(judge, preHeld: wasHoldingBeforePress);
        }

        internal static void NotifyEz2AcRepress(DrawableHoldNote hold)
        {
            var round = getJudgementRound(hold);

            if (round.Environment.JudgeMode != EzEnumJudgeMode.EZ2AC)
                return;

            if (!hold.Head.AllJudged)
                return;

            GetEz2AcHoldState(hold).OnRepress();
        }

        internal static bool TryJudgeModeCheckForResult(DrawableNote note, bool userTriggered, double timeOffset)
            => TryApplyEzNoteCheckForResult(note, userTriggered, timeOffset);

        internal static bool TryHoldTailCheckForResult(DrawableHoldNoteTail tail, bool userTriggered, double timeOffset)
            => TryApplyEzHoldTailCheckForResult(tail, userTriggered, timeOffset);

        internal static bool TryBmsOnPressed(DrawableNote note, KeyBindingPressEvent<ManiaAction> e)
        {
            if (note.HitObject.HitWindows is not ManiaHitWindows)
                return false;

            var round = getJudgementRound(note);
            var state = GetBmsState(note);
            var action = BmsJudgeModeJudgement.Instance.TryPostBadOnPressed(state, round.PoorEnabled);

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

            var state = o2_hold_states.GetValue(hold, _ => new O2JudgeModeJudgement.HoldBreakState());
            O2JudgeModeJudgement.Instance.ApplyDrawableHoldBreakUpdate(hold, state);
        }

        internal static bool TryO2HoldCheckForResult(DrawableHoldNote hold, bool userTriggered, double timeOffset)
        {
            var round = getJudgementRound(hold);

            if (round.Environment.JudgeMode != EzEnumJudgeMode.O2Jam)
                return false;

            return O2JudgeModeJudgement.Instance.TryO2HoldCheckForResult(hold, userTriggered, timeOffset);
        }

        internal static void ApplyNoteOutcome(DrawableNote drawable, ManiaJudgementRound round, ManiaNoteJudgementOutcome outcome, bool userTriggered = true)
        {
            switch (outcome.Kind)
            {
                case ManiaNoteJudgementOutcomeKind.Apply:
                    if (!userTriggered && outcome.Result == HitResult.Miss)
                        drawable.EzApplyPassiveMissWithStoredOffset(resolveStableClassicHeadResult(drawable, round, HitResult.Miss));
                    else
                        drawable.EzApplyFinalResult(resolveStableClassicHeadResult(drawable, round, outcome.Result), round.Environment.JudgeMode);

                    if (drawable is DrawableHoldNoteHead head && head.ParentHold is DrawableHoldNote hold)
                    {
                        NotifyEz2AcHeadJudged(hold, outcome.Result, wasHoldingBeforePress: false);

                        // stable classic：喂按下边沿（尾判的时间基准），头本身的判定被压成 IgnoreHit。
                        if (userTriggered && StableClassicJudgeModeJudgement.IsSelected(round.Environment.JudgeMode))
                            GetStableClassicHoldState(hold).OnPressed(drawable.Time.Current);
                    }

                    break;

                case ManiaNoteJudgementOutcomeKind.DispatchExtra:
                    drawable.EzDispatchExtraResult(outcome.Result);
                    break;
            }
        }

        internal static void ApplyBmsAction(DrawableNote drawable, ManiaJudgementRound round, BmsJudgeModeJudgement.DrawableAction action, BmsJudgeModeJudgement.BmsRouteState state)
            => applyBmsAction(drawable, r => drawable.EzApplyFinalResult(r, round.Environment.JudgeMode), drawable.EzDispatchExtraResult, action, state);

        internal static void ApplyBmsAction(DrawableHoldNoteTail drawable, ManiaJudgementRound round, BmsJudgeModeJudgement.DrawableAction action, BmsJudgeModeJudgement.BmsRouteState state)
            => applyBmsAction(drawable, r => drawable.EzApplyFinalResult(r, round.Environment.JudgeMode), drawable.EzDispatchExtraResult, action, state);

        private static void applyBmsAction<T>(
            T _,
            Action<HitResult> applyFinal,
            Action<HitResult> dispatchExtra,
            BmsJudgeModeJudgement.DrawableAction action,
            BmsJudgeModeJudgement.BmsRouteState state)
        {
            if (!action.Handled)
                return;

            BmsJudgeModeJudgement.ApplyRouteState(state, action);

            var result = BmsJudgeModeJudgement.MapTo(action.Judge);

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

            if (TryEz2AcHoldOnReleased(hold))
                return true;

            hold.Tail.UpdateResult();
            hold.EzTriggerBodyAfterTailRelease();
            hold.Result.ReportHoldState(currentTime, false);
            hold.EzTryFinalizeAfterTailJudged();
            return true;
        }

        /// <summary>本局 JudgeMode 是否允许一条 LN 中途多次断连各计一次（见 <see cref="IManiaHoldJudgementStrategy.AllowsRepeatedHoldBreak"/>）。</summary>
        internal static bool AllowsRepeatedHoldBreak(DrawableHoldNote hold)
            => ManiaJudgementRegistry.GetHoldStrategy(getJudgementRound(hold).Environment).AllowsRepeatedHoldBreak;

        internal static bool TryApplyEzNoteCheckForResult(DrawableNote drawable, bool userTriggered, double timeOffset)
        {
            ManiaJudgeHotPathTrace.RecordCheckForResult();

            var round = getJudgementRound(drawable);

            // UpdateResult(false) 已通过 ShouldDeferAutoMissUpdate；勿再调 Gate（曾每帧 WindowFor）。
            // 用户按键路径仍可能走 UpdateResult(true)，Gate 不适用。

            if (round.Strategy == null)
                return false;

            var bmsState = round.Strategy is BmsJudgeModeJudgement ? GetBmsState(drawable) : null;

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

            if (round.Strategy == null)
                return false;

            // stable classic：尾判只在尾「可判」时发起（松手宽限同 lazer，即 HitWindows.ResultFor 会给档的范围）。
            // 到点判尾由帧钟触发，过早的松手只记断连、不落判；不设这道 gate，
            // `DrawableHoldNote.OnReleased` 的 `Tail.UpdateResult()` 会在提前松手那一刻当场判 Miss
            // （Session 侧的同类松手有候选窗 gate，于是两侧同源破裂）。
            if (StableClassicJudgeModeJudgement.IsSelected(round.Environment.JudgeMode)
                && timeOffset < 0
                && drawable.HitObject.HitWindows is { } tailWindows
                && !ReferenceEquals(tailWindows, HitWindows.Empty)
                && -timeOffset > tailWindows.WindowFor(HitResult.Miss) * TailNote.RELEASE_WINDOW_LENIENCE)
            {
                return false;
            }

            var bmsState = round.Strategy is BmsJudgeModeJudgement ? GetBmsState(drawable) : null;

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
                StableClassicState = GetStableClassicHoldState(drawable.HoldNote),
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
                drawable.EzApplyFinalResult(result.FinalResult.Value, round.Environment.JudgeMode);
                return true;
            }

            return true;
        }

        /// <summary>
        /// stable classic：LN 头不产生独立判定，压成 <see cref="HitResult.IgnoreHit"/> / <see cref="HitResult.IgnoreMiss"/> ——
        /// 不计分、不计 combo、不进 acc，持有态另由 <see cref="StableClassicHoldState"/> 承担。
        /// </summary>
        /// <remarks>
        /// 头绑定的是 <see cref="IgnoreJudgement"/>，落 [IgnoreMiss, IgnoreHit] 之外的结果会被计分器校验拒绝，
        /// 故被动 Miss 也必须走本映射。
        /// </remarks>
        private static HitResult resolveStableClassicHeadResult(DrawableNote drawable, ManiaJudgementRound round, HitResult result)
        {
            if (drawable is not DrawableHoldNoteHead || !StableClassicJudgeModeJudgement.IsSelected(round.Environment.JudgeMode))
                return result;

            switch (result)
            {
                case HitResult.Miss:
                    return HitResult.IgnoreMiss;

                case HitResult.Perfect:
                case HitResult.Great:
                case HitResult.Good:
                case HitResult.Ok:
                case HitResult.Meh:
                    return HitResult.IgnoreHit;

                default:
                    return result;
            }
        }

        private static bool applyNoteEvaluation(
            DrawableNote drawable,
            ManiaJudgementRound round,
            ManiaJudgementKernel.NoteEvaluationResult result,
            BmsJudgeModeJudgement.BmsRouteState? bmsState,
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
            BmsJudgeModeJudgement.DrawableAction action,
            BmsJudgeModeJudgement.BmsRouteState state)
        {
            if (!action.Handled || !action.ApplyFinal)
                return;

            BmsJudgeModeJudgement.ApplyRouteState(state, action);
            var result = BmsJudgeModeJudgement.MapTo(action.Judge);
            drawable.EzApplyBmsAutoMissFinalWithStoredOffset(result, round.Environment.JudgeMode);
        }

        private static long getFrameStableId(DrawableHitObject drawable)
            => (long)(drawable.Time.Current * 1000);

        private static void publishO2RuntimeState(ManiaJudgementRound round)
        {
            if (!round.IsO2Jam)
                return;

            O2JudgeModeExtension.PILL_COUNT.Value = round.MutableState.O2PillCount;
            O2JudgeModeExtension.CoolCombo = round.MutableState.O2CoolCombo;
        }
    }
}
