// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Clip / next / interrupt state machine. Idle seconds are not reset by automatic idle clips.
    /// </summary>
    public class EzPetStateMachine
    {
        public const string FALLBACK_STATE = "idle";

        private EzPetPackDefinition definition = new EzPetPackDefinition();
        private HashSet<string> availableClips = new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<int> firedComboAts = new HashSet<int>();
        private readonly HashSet<double> firedIdleAfter = new HashSet<double>();

        private int missStreak;
        private string? pendingNext;
        private bool clipFinished = true;
        private bool currentClipLoops = true;
        private string hoverStateName = "hover";

        public string CurrentState { get; private set; } = FALLBACK_STATE;

        public string CurrentClip { get; private set; } = FALLBACK_STATE;

        public double IdleSeconds { get; private set; }

        public event Action<string, string>? ClipChanged;

        public event Action<EzPetVisibilityAction>? VisibilityAction;

        /// <summary>
        /// Fired when a rule requests a stage motion id (or null/empty to stop scripted motion).
        /// </summary>
        public event Action<string?>? MotionRequested;

        public void ApplyPack(EzPetPackDefinition pack, IReadOnlyCollection<string> clips)
        {
            definition = pack;
            availableClips = new HashSet<string>(clips, StringComparer.Ordinal);
            hoverStateName = findGoto("hover") ?? "hover";

            firedComboAts.Clear();
            firedIdleAfter.Clear();
            missStreak = 0;
            IdleSeconds = 0;
            pendingNext = null;
            clipFinished = true;

            MotionRequested?.Invoke(null);

            if (!tryGoto(definition.DefaultState, interrupt: true, restartIfSame: true))
                tryGoto(FALLBACK_STATE, interrupt: true, restartIfSame: true);
        }

        public void ResetPlaySession()
        {
            firedComboAts.Clear();
            missStreak = 0;
        }

        /// <summary>
        /// Clears the AFK idle timer. Prefer <see cref="NotifyUserActivity"/> when hardware input should also leave idle* states.
        /// </summary>
        public void ResetIdleTimer()
        {
            IdleSeconds = 0;
            firedIdleAfter.Clear();
        }

        /// <summary>
        /// Hardware / user activity: reset idle timer and leave idlePlay / idleYawn / idleSleep.
        /// </summary>
        public void NotifyUserActivity()
        {
            ResetIdleTimer();

            if (!isAfkIdleState(CurrentState))
                return;

            if (!tryGoto(definition.DefaultState, interrupt: true, restartIfSame: false))
                tryGoto(FALLBACK_STATE, interrupt: true, restartIfSame: false);
        }

        public void NotifyClipFinished()
        {
            if (clipFinished && string.IsNullOrEmpty(pendingNext))
                return;

            clipFinished = true;

            if (string.IsNullOrEmpty(pendingNext))
                return;

            string next = pendingNext;
            pendingNext = null;

            if (!tryGoto(next, interrupt: true, restartIfSame: false))
                tryGoto(definition.DefaultState, interrupt: true, restartIfSame: false);
        }

        public void UpdateIdle(double elapsedSeconds)
        {
            if (elapsedSeconds < 0)
                return;

            IdleSeconds += elapsedSeconds;

            EzPetRule? chosen = null;

            foreach (var rule in definition.Rules)
            {
                if (!isWhen(rule, "idle") || rule.AfterSeconds == null)
                    continue;

                double after = rule.AfterSeconds.Value;

                if (IdleSeconds < after || firedIdleAfter.Contains(after))
                    continue;

                if (chosen == null || after > chosen.AfterSeconds)
                    chosen = rule;
            }

            if (chosen == null)
                return;

            if (tryApplyRule(chosen, resetIdle: false, restartIfSame: true))
                firedIdleAfter.Add(chosen.AfterSeconds!.Value);
        }

        public bool HandleHover()
        {
            var rule = findRule("hover");
            if (rule == null)
                return false;

            if (CurrentState == rule.Goto)
                return true;

            if (!tryGoto(rule.Goto, rule.Interrupt, restartIfSame: false))
                return false;

            NotifyUserActivity();
            return true;
        }

        public bool HandleHoverEnd()
        {
            if (CurrentState != hoverStateName)
                return false;

            var rule = findRule("hoverEnd");
            string target = rule?.Goto ?? definition.DefaultState;

            if (!tryGoto(target, interrupt: true, restartIfSame: false))
                return false;

            NotifyUserActivity();
            return true;
        }

        public bool HandleClick() => handleNamed("click", resetIdle: true, restartIfSame: true);

        public bool HandleDrag() => handleNamed("drag", resetIdle: true, restartIfSame: true);

        public bool HandleDragEnd()
        {
            var rule = findRule("dragEnd");
            if (rule != null)
                return tryApplyRule(rule, resetIdle: true, restartIfSame: true);

            // Default: leave grabbed / drag state back to pack default.
            if (string.Equals(CurrentState, "grabbed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(CurrentState, findGoto("drag"), StringComparison.OrdinalIgnoreCase))
            {
                MotionRequested?.Invoke(null);
                return tryGoto(definition.DefaultState, interrupt: true, restartIfSame: false)
                       || tryGoto(FALLBACK_STATE, interrupt: true, restartIfSame: false);
            }

            return false;
        }

        public bool HandleGameplayEnter() => handleNamed("gameplayEnter", resetIdle: true, restartIfSame: true);

        public bool HandleFail() => handleNamed("fail", resetIdle: true, restartIfSame: true);

        public bool HandleClear() => handleNamed("clear", resetIdle: true, restartIfSame: true);

        /// <summary>
        /// Results screen: optional rank-filtered rule (<c>when: resultsRank</c>).
        /// </summary>
        public bool HandleResultsRank(string? rankName)
        {
            EzPetRule? any = null;
            EzPetRule? matched = null;

            foreach (var rule in definition.Rules)
            {
                if (!isWhen(rule, "resultsRank"))
                    continue;

                if (string.IsNullOrWhiteSpace(rule.Rank))
                {
                    any = rule;
                    continue;
                }

                if (!string.IsNullOrEmpty(rankName)
                    && string.Equals(rule.Rank, rankName, StringComparison.OrdinalIgnoreCase))
                    matched = rule;
            }

            var chosen = matched ?? any;
            return chosen != null && tryApplyRule(chosen, resetIdle: true, restartIfSame: true);
        }

        /// <summary>
        /// Leave Player: return to default idle and clear play-session combo flags.
        /// </summary>
        public bool HandleGameplayLeave()
        {
            ResetPlaySession();
            ResetIdleTimer();

            if (tryGoto(definition.DefaultState, interrupt: true, restartIfSame: true))
                return true;

            return tryGoto(FALLBACK_STATE, interrupt: true, restartIfSame: true);
        }

        public bool HandleStarRating(double starRating)
        {
            string? state = definition.MatchStarBand(starRating);
            if (state == null)
                return false;

            return tryGoto(state, interrupt: false, restartIfSame: true);
        }

        public bool HandleCombo(int combo)
        {
            EzPetRule? chosen = null;

            foreach (var rule in definition.Rules)
            {
                if (!isWhen(rule, "combo") || rule.At == null)
                    continue;

                int at = rule.At.Value;

                if (combo < at || firedComboAts.Contains(at))
                    continue;

                if (chosen == null || at > chosen.At)
                    chosen = rule;
            }

            if (chosen == null)
                return false;

            if (!tryApplyRule(chosen, resetIdle: true, restartIfSame: true))
                return false;

            firedComboAts.Add(chosen.At!.Value);
            return true;
        }

        public bool HandleMiss()
        {
            missStreak++;

            EzPetRule? every = null;
            EzPetRule? streakMatch = null;

            foreach (var rule in definition.Rules)
            {
                if (!isWhen(rule, "miss"))
                    continue;

                if (rule.Streak == null)
                    every = rule;
                else if (rule.Streak.Value == missStreak)
                    streakMatch = rule;
            }

            var chosen = streakMatch ?? every;
            if (chosen == null)
                return false;

            return tryApplyRule(chosen, resetIdle: true, restartIfSame: true);
        }

        public void NotifyNonMissJudgement() => missStreak = 0;

        private bool handleNamed(string when, bool resetIdle, bool restartIfSame)
        {
            var rule = findRule(when);
            if (rule == null)
                return false;

            return tryApplyRule(rule, resetIdle, restartIfSame);
        }

        private bool tryApplyRule(EzPetRule rule, bool resetIdle, bool restartIfSame)
        {
            var action = rule.ResolveAction();

            if (action == EzPetVisibilityAction.Hide)
            {
                VisibilityAction?.Invoke(EzPetVisibilityAction.Hide);
                requestMotion(rule.Motion);
                if (resetIdle)
                    ResetIdleTimer();
                return true;
            }

            if (action == EzPetVisibilityAction.Show)
            {
                VisibilityAction?.Invoke(EzPetVisibilityAction.Show);

                if (!string.IsNullOrEmpty(rule.Goto))
                    tryGoto(rule.Goto, rule.Interrupt, restartIfSame);

                requestMotion(rule.Motion);

                if (resetIdle)
                    ResetIdleTimer();

                return true;
            }

            if (!tryGoto(rule.Goto, rule.Interrupt, restartIfSame))
                return false;

            requestMotion(rule.Motion);

            if (resetIdle)
                ResetIdleTimer();

            return true;
        }

        private void requestMotion(string? motionId) => MotionRequested?.Invoke(motionId);

        private bool tryGoto(string? stateName, bool interrupt, bool restartIfSame)
        {
            if (string.IsNullOrEmpty(stateName))
                return false;

            if (!definition.States.TryGetValue(stateName, out var state))
                return false;

            string clip = string.IsNullOrEmpty(state.Clip) ? stateName : state.Clip;

            if (!availableClips.Contains(clip))
                return false;

            if (!interrupt && !canEnterWithoutInterrupt())
                return false;

            if (!restartIfSame && CurrentState == stateName)
                return true;

            bool loops = definition.Clips.TryGetValue(clip, out var clipDef) && clipDef.Loop;

            CurrentState = stateName;
            CurrentClip = clip;
            currentClipLoops = loops;
            clipFinished = false;
            pendingNext = loops ? null : state.Next;

            ClipChanged?.Invoke(CurrentState, CurrentClip);
            return true;
        }

        private bool canEnterWithoutInterrupt()
        {
            if (clipFinished)
                return true;

            return currentClipLoops;
        }

        private EzPetRule? findRule(string when)
        {
            foreach (var rule in definition.Rules)
            {
                if (isWhen(rule, when))
                    return rule;
            }

            return null;
        }

        private string? findGoto(string when) => findRule(when)?.Goto;

        private static bool isWhen(EzPetRule rule, string when)
            => string.Equals(rule.When, when, StringComparison.OrdinalIgnoreCase);

        private static bool isAfkIdleState(string state) =>
            string.Equals(state, "idlePlay", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "idleYawn", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "idleSleep", StringComparison.OrdinalIgnoreCase);
    }
}
