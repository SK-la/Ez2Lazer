// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input.Events;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public partial class Ez2AcDrawableLNTail : DrawableHoldNoteTail
    {
        // public override bool DisplayResult => false;
        // public override bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        // {
        //     return UpdateResult(true);
        // }

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
            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyMinResult();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);

            if (result == HitResult.None)
                return;

            // if (timeOffset >= 150 && timeOffset <= -500)
            //     ApplyResult(HitResult.Pool);

            result = GetCappedResult(result);

            ApplyResult(result);
        }
    }
}
