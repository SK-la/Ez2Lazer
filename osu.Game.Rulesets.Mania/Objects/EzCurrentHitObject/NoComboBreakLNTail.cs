// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public class NoComboBreakLNTail : TailNote
    {
        public override Judgement CreateJudgement() => new NoComboBreakTailJudgement();
        protected override HitWindows CreateHitWindows() => HitWindows.Empty;

        public class NoComboBreakTailJudgement : ManiaJudgement
        {
            public override HitResult MaxResult => HitResult.IgnoreHit;
            public override HitResult MinResult => HitResult.ComboBreak;
        }
    }
}
