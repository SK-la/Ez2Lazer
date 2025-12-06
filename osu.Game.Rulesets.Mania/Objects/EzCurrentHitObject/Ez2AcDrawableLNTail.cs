// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public partial class Ez2AcDrawableLNTail : DrawableHoldNoteTail
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (HoldNote.IsHolding.Value && timeOffset >= 0)
                ApplyResult(HitResult.Perfect);
            else
                base.CheckForResult(userTriggered, timeOffset);
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
                ApplyResult(HitResult.Pool);
            }

            base.CheckForResult(userTriggered, timeOffset);
        }
    }
}
