// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public partial class Ez2AcDrawableLNTail : DrawableHoldNoteTail
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

            double adjustedOffset = Math.Abs(timeOffset);

            if (adjustedOffset > HitObject.HitWindows.WindowFor(HitResult.Meh) &&
                adjustedOffset < HitObject.HitWindows.WindowFor(HitResult.Miss))
                ApplyResult(HitResult.Miss);

            // Logger.Log($"Tail result: {result}, IsHolding: {HoldNote.IsHolding.Value}, HasHoldBreak: {HoldNote.Body.HasHoldBreak}");
            // ApplyResult(static (r, state) =>
            // {
            //     r.Type = state;
            //
            //     if (state == HitResult.Meh || state == HitResult.Miss)
            //         r.IsComboHit = false;
            // }, result);

            if ((timeOffset >= 0 && HoldNote.IsHolding.Value) || (timeOffset <= 20 && HoldNote.Tail.IsHit))
            {
                ApplyResult(HitResult.IgnoreHit);
            }
            else if (timeOffset > 0)
            {
                ApplyMinResult();
            }
        }

        protected override HitResult GetCappedResult(HitResult result)
        {
            bool hasComboBreak = !HoldNote.Head.IsHit || HoldNote.Body.HasHoldBreak;

            if (result > HitResult.Miss && hasComboBreak)
                return HitResult.ComboBreak;

            return result;
        }

        public override bool OnPressed(KeyBindingPressEvent<ManiaAction> e) => false; // Handled by the hold note

        public override void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
        }
    }

    public partial class Ez2AcDrawableNote : DrawableNote
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyResult(HitResult.Miss);

                return;
            }

            if (timeOffset > -HitObject.HitWindows.WindowFor(HitResult.Miss) &&
                timeOffset < -HitObject.HitWindows.WindowFor(HitResult.Poor))
            {
                if (HitObject.HitWindows.IsHitResultAllowed(HitResult.Poor))
                {
                    DispatchNewResult(HitResult.Poor);
                }

                return;
            }

            base.CheckForResult(userTriggered, timeOffset);
        }
    }
}
