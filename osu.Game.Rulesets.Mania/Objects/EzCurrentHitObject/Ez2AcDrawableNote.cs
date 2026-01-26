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
            if (!HoldNote.Head.IsHit)
            {
                return;
            }

            if ((timeOffset >= 0 && HoldNote.IsHolding.Value) || (timeOffset <= 20 && HoldNote.Tail.IsHit))
            {
                ApplyMaxResult();
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
            // if (userTriggered && HitObject.HitWindows is ManiaHitWindows ezWindows)
            // {
            //     double missWindow = double.Abs(ezWindows.WindowFor(HitResult.Miss));
            //     double poolEarlyWindow = missWindow + 500;
            //     double poolLateWindow = missWindow + 150;
            //
            //     // 提前按下（timeOffset < 0）且在提前 Pool 窗口内
            //     if ((timeOffset < 0 && missWindow <= poolEarlyWindow) ||
            //         (timeOffset > 0 && timeOffset <= poolLateWindow))
            //         ApplyResult(HitResult.Pool);
            // }

            if (userTriggered && (timeOffset < -500 || timeOffset > 200))
            {
                DispatchNewResult(HitResult.Poor);
                return;
            }

            base.CheckForResult(userTriggered, timeOffset);
        }
    }
}
