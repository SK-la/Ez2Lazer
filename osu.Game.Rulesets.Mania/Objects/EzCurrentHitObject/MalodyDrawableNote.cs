// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
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
        public static HitWindows HitWindows = new ManiaHitWindows();

        public override bool DisplayResult => false;

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (!HoldNote.Head.IsHit)
            {
                return;
            }

            if (timeOffset > 0 && HoldNote.Head.IsHit)
            {
                ApplyMaxResult();
                return;
            }

            if (timeOffset > 0)
            {
                ApplyMinResult();
                return;
            }

            if (HoldNote.IsHolding.Value)
            {
                return;
            }

            if (HoldNote.Head.IsHit && Math.Abs(timeOffset) < Math.Abs(HitWindows.WindowFor(HitResult.Meh) * TailNote.RELEASE_WINDOW_LENIENCE))
            {
                ApplyMaxResult();
            }
            else
            {
                ApplyMinResult();
            }
        }
    }
}
