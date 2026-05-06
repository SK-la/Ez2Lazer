// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public partial class MalodyDrawableLNBody : DrawableHoldNoteBody
    {
        public new bool HasHoldBreak => false;

        internal override void TriggerResult(bool hit)
        {
            if (AllJudged) return;

            ApplyMaxResult();
        }
    }

    public partial class MalodyDrawableLNTail : DrawableHoldNoteTail
    {
        public override bool DisplayResult => false;

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            // Malody LN: only head is judged.
            // Tail never contributes a visible/scored judgement.
            // Finalize at tail time with IgnoreHit so the hold object can complete.
            if (!userTriggered && timeOffset >= 0)
                ApplyResult(HitResult.IgnoreHit);
        }
    }
}
