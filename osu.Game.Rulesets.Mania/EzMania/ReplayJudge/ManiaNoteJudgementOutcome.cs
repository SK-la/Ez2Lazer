// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    public enum ManiaNoteJudgementOutcomeKind
    {
        None,
        Apply,
        DispatchExtra,
    }

    public readonly struct ManiaNoteJudgementOutcome
    {
        public ManiaNoteJudgementOutcomeKind Kind { get; }

        public HitResult Result { get; }

        public static ManiaNoteJudgementOutcome Ignore => new ManiaNoteJudgementOutcome(ManiaNoteJudgementOutcomeKind.None, HitResult.None);

        public static ManiaNoteJudgementOutcome ApplyResult(HitResult result) => new ManiaNoteJudgementOutcome(ManiaNoteJudgementOutcomeKind.Apply, result);

        public static ManiaNoteJudgementOutcome DispatchExtraResult(HitResult result) => new ManiaNoteJudgementOutcome(ManiaNoteJudgementOutcomeKind.DispatchExtra, result);

        private ManiaNoteJudgementOutcome(ManiaNoteJudgementOutcomeKind kind, HitResult result)
        {
            Kind = kind;
            Result = result;
        }
    }
}
