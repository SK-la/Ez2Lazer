// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Scoring
{
    public partial class PoolHitObject : ManiaHitObject
    {
        public PoolHitObject(double startTime, int column)
        {
            StartTime = startTime;
            Column = column;
        }

        public override Judgement CreateJudgement() => new ManiaJudgement();

        protected override HitWindows CreateHitWindows() => new PoolHitWindows();
    }

    public class PoolHitWindows : HitWindows
    {
        public override bool IsHitResultAllowed(HitResult result) => result == HitResult.Pool;

        public override double WindowFor(HitResult result) => result == HitResult.Pool ? 0 : double.PositiveInfinity;

        public override void SetDifficulty(double difficulty) { }
    }
}
