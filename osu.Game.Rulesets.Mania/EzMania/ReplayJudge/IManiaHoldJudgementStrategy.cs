// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    public readonly struct HoldTailEvaluationContext
    {
        public double RawOffset { get; init; }

        public double TimeOffsetForJudgement { get; init; }

        public HitWindows HitWindows { get; init; }

        public bool HeadHit { get; init; }

        public bool HoldBreak { get; init; }

        public bool HoldBroken { get; init; }

        public bool WasHoldingBeforeRelease { get; init; }

        public ManiaReplayJudgementState State { get; init; }

        public double EventTime { get; init; }

        public double Bpm { get; init; }

        public bool PillModeEnabled { get; init; }
    }

    public interface IManiaHoldJudgementStrategy
    {
        HitResult EvaluateTail(in HoldTailEvaluationContext context);

        bool CanBeginHoldAt(double time, TailNote tail);

        bool IsHoldBreak(double rawOffset, HitWindows hitWindows);
    }
}
