// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Judgements
{
    /// <summary>
    /// Malody LN tail：仅以 <see cref="HitResult.IgnoreHit"/> 完成，不计入成绩。
    /// </summary>
    public class MalodyTailJudgement : ManiaJudgement
    {
        public override HitResult MaxResult => HitResult.IgnoreHit;

        public override HitResult MinResult => HitResult.IgnoreMiss;
    }
}
