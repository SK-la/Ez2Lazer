// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    public enum MalodyJudge
    {
        None,
        Perfect,
        Miss,
    }

    public sealed class MalodyHitModeJudgement : IManiaHitModeJudgement
    {
        public static MalodyHitModeJudgement Instance { get; } = new MalodyHitModeJudgement();

        private readonly LazerNoteJudgementReplica lazerNote = LazerNoteJudgementReplica.Instance;

        public static HitResult MapTo(MalodyJudge judge) => judge switch
        {
            MalodyJudge.Perfect => HitResult.Perfect,
            MalodyJudge.Miss => HitResult.Miss,
            _ => HitResult.None,
        };

        public ManiaNoteJudgementOutcome EvaluateAutoMiss(double timeOffset, HitWindows hitWindows) => lazerNote.EvaluateAutoMiss(timeOffset, hitWindows);

        public ManiaNoteJudgementOutcome EvaluatePress(double timeOffset, HitWindows hitWindows) => lazerNote.EvaluatePress(timeOffset, hitWindows);

        /// <summary>
        /// Malody LN：tail 仅以 <see cref="HitResult.IgnoreHit"/> 完成，不计入成绩。
        /// </summary>
        public HitResult EvaluateTail(in HoldTailEvaluationContext context) => HitResult.IgnoreHit;

        public MalodyJudge EvaluateTailJudge(in HoldTailEvaluationContext context) => MalodyJudge.None;

        public bool CanBeginHoldAt(double time, TailNote tail) => LazerHoldJudgementReplica.Instance.CanBeginHoldAt(time, tail);

        public bool IsHoldBreak(double rawOffset, HitWindows hitWindows) => false;

        public static bool IsMalodyMode(EzEnumHitMode hitMode)
            => hitMode is EzEnumHitMode.Malody_E or EzEnumHitMode.Malody_B;
    }
}
