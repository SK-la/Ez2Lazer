// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public partial class DrawableLNTailForNoRelease : DrawableHoldNoteTail
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            // apply perfect once the tail is reached
            if (HoldNote.Head.IsHit && timeOffset >= 0)
                ApplyResult(GetCappedResult(HitResult.Perfect));
            else
                base.CheckForResult(userTriggered, timeOffset);
        }
    }
}
