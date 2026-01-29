// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public partial class BMSDrawableNote : DrawableNote
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyResult(HitResult.Miss);

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);
            double adjustedOffset = Math.Abs(timeOffset);

            if (adjustedOffset > HitObject.HitWindows.WindowFor(HitResult.Meh) &&
                adjustedOffset < HitObject.HitWindows.WindowFor(HitResult.Miss))
                ApplyResult(HitResult.Miss);

            if (adjustedOffset > HitObject.HitWindows.WindowFor(HitResult.Miss) &&
                adjustedOffset < HitObject.HitWindows.WindowFor(HitResult.Poor))
                result = HitResult.Poor;

            if (result == HitResult.None)
                result = HitResult.Poor;

            if (result == HitResult.Poor)
            {
                if (!HitObject.HitWindows.IsHitResultAllowed(HitResult.Poor))
                    return;

                // Dispatch transient poor result to processors without ending lifecycle.
                DispatchNewResult(HitResult.Poor);
                return;
            }

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                if (state == HitResult.Meh || state == HitResult.Miss)
                    r.IsComboHit = false;
            }, result);
        }

        // public override bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        // {
        //     if (e.Action != Action.Value)
        //         return false;
        //
        //     if (CheckHittable?.Invoke(this, Time.Current) == false)
        //         return UpdateResult(false);
        //
        //     return UpdateResult(true);
        // }
    }

    public partial class BMSDrawableHoldNoteHead : DrawableHoldNoteHead
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyResult(HitResult.Miss);

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);
            double adjustedOffset = Math.Abs(timeOffset);

            if (adjustedOffset > HitObject.HitWindows.WindowFor(HitResult.Meh) &&
                adjustedOffset < HitObject.HitWindows.WindowFor(HitResult.Miss))
                ApplyResult(HitResult.Miss);

            if (adjustedOffset > HitObject.HitWindows.WindowFor(HitResult.Miss) &&
                adjustedOffset < HitObject.HitWindows.WindowFor(HitResult.Poor))
                result = HitResult.None;

            if (result == HitResult.None)
                result = HitResult.Poor;

            if (result == HitResult.Poor)
            {
                if (!HitObject.HitWindows.IsHitResultAllowed(HitResult.Poor))
                    return;

                DispatchNewResult(HitResult.Poor);
                return;
            }

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                if (state == HitResult.Meh || state == HitResult.Miss)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class BMSDrawableHoldNoteTail : DrawableHoldNoteTail
    {
        public override bool DisplayResult => false;

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyResult(HitResult.Miss);

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);
            double adjustedOffset = Math.Abs(timeOffset);

            if (adjustedOffset > HitObject.HitWindows.WindowFor(HitResult.Meh) &&
                adjustedOffset < HitObject.HitWindows.WindowFor(HitResult.Miss))
                ApplyResult(HitResult.Miss);

            if (adjustedOffset > HitObject.HitWindows.WindowFor(HitResult.Miss) &&
                adjustedOffset < HitObject.HitWindows.WindowFor(HitResult.Poor))
                result = HitResult.Poor;

            if (result == HitResult.None)
                return;

            if (HoldNote.Body.HasHoldBreak)
                result = HitResult.Poor;

            if (HoldNote.IsHolding.Value && adjustedOffset > HitObject.HitWindows.WindowFor(HitResult.Meh))
                result = HitResult.Poor;

            // If the computed result is Poor, mark it temporarily but don't apply a final result.
            // Also populate RawTime and dispatch OnNewResult so processors (score/health) can count it.
            if (result == HitResult.Poor)
            {
                if (!HitObject.HitWindows.IsHitResultAllowed(HitResult.Poor))
                    return;

                DispatchNewResult(HitResult.Poor);
                return;
            }

            // Logger.Log($"Tail result: {result}, IsHolding: {HoldNote.IsHolding.Value}, HasHoldBreak: {HoldNote.Body.HasHoldBreak}");
            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                if (state == HitResult.Meh || state == HitResult.Miss)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class BMSDrawableHoldNote : DrawableHoldNote
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (Tail.AllJudged)
            {
                if (Tail.IsHit)
                {
                    bool breakComboFromTailMeh = Tail.Result.Type == HitResult.Meh;

                    ApplyResult(static (r, breakCombo) =>
                    {
                        r.Type = r.Judgement.MaxResult;

                        if (breakCombo)
                            r.IsComboHit = false;
                    }, breakComboFromTailMeh);
                }
                else
                    MissForcefully();

                // Make sure that the hold note is fully judged by giving the body a judgement.
                if (!Body.AllJudged)
                    Body.TriggerResult(Tail.IsHit);

                // Important that this is always called when a result is applied.
                Result.ReportHoldState(Time.Current, false);
            }
        }

        // private Func<DrawableHitObject, double, bool>? originalCheckHittable;

        // protected override void Update()
        // {
        //     base.Update();
        //
        //     // 如果 Body 已断连且尚未到达尾点，强制锁定 IsHolding 状态。
        //     if (Body != null && Tail != null && Body.HasHoldBreak && Time.Current < Tail.HitObject.StartTime)
        //     {
        //         if (IsHolding is Bindable<bool> b)
        //             b.Value = false;
        //     }
        //     else
        //     {
        //         // 当不处于锁定期，恢复可能被替换的 CheckHittable
        //         if (originalCheckHittable != null)
        //         {
        //             CheckHittable = originalCheckHittable;
        //             originalCheckHittable = null;
        //         }
        //     }
        //
        //     // 在锁定期，确保输入不可命中
        //     if (Body != null && Tail != null && Body.HasHoldBreak && Time.Current < Tail.HitObject.StartTime)
        //     {
        //         originalCheckHittable ??= CheckHittable;
        //
        //         CheckHittable = (d, t) => false;
        //     }
        // }
    }
}
