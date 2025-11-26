// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
            if (HoldNote.Head.IsHit && timeOffset >= 0)
            {
                if (HoldNote.IsHolding.Value)
                {
                    ApplyResult(HitResult.Perfect);
                }
                else
                {
                    ApplyResult(HitResult.Miss);
                }
            }
        }
    }
}
