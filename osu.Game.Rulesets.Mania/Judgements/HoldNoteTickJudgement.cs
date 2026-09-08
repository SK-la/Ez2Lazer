// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Judgements
{
    /// <summary>
    /// EZ2AC LN tick：成功用 <see cref="HitResult.SliderTailHit"/>（涨 combo、非 IsBasic、可与 IgnoreMiss 配对）；
    /// 未按住用 <see cref="HitResult.IgnoreMiss"/>（完结物件、不断 combo、不计档）。
    /// 不用 LargeTickHit/Miss：后者 Miss 会断 combo，且 IgnoreMiss 无法作为 LargeTick 的合法结果。
    /// </summary>
    public class HoldNoteTickJudgement : ManiaJudgement
    {
        public override HitResult MaxResult => HitResult.SliderTailHit;

        public override HitResult MinResult => HitResult.IgnoreMiss;
    }
}
