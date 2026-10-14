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

        /// <summary>
        /// Session O2：true 时按 press-time BPM 走扩展 ResultFor；Drawable 为 false 并先同步 ManiaHitWindows BPM。
        /// </summary>
        public bool UsePressTimeBpmForJudgement { get; init; }
    }

    public interface IManiaHoldJudgementStrategy
    {
        /// <summary>
        /// 一条 LN 中途每次松手是否各计一次断连（stable 语义：每次提前松手重置 combo）。
        /// 为 <c>false</c> 时整条 LN 最多产出一次 Body 断连，后续松手不再补发。
        /// </summary>
        bool AllowsRepeatedHoldBreak { get; }

        HitResult EvaluateTail(in HoldTailEvaluationContext context);

        bool CanBeginHoldAt(double time, TailNote tail);

        bool IsHoldBreak(double rawOffset, HitWindows hitWindows);
    }
}
