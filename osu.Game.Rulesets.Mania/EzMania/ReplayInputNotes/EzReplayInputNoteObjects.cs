// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayInputNotes
{
    /// <summary>
    /// Base for the synthetic objects the replay input overlay feeds into its own scrolling playfield.
    /// </summary>
    /// <remarks>
    /// These exist only inside <see cref="EzReplayInputNotePlayfield"/>: they are never part of a beatmap, never judged and
    /// die with the replay player that created them. Being real <see cref="HitObject"/>s is what lets
    /// <see cref="osu.Game.Rulesets.UI.Scrolling.ScrollingHitObjectContainer"/> place them through exactly the same code
    /// path as the beatmap's own notes.
    /// </remarks>
    public abstract class EzReplayInputNoteObject : HitObject
    {
        public EzReplayInputNoteKind Kind { get; init; }

        public HitResult? HeadResult { get; init; }

        public HitResult? NonHeadResult { get; init; }

        /// <summary>Time from which <see cref="NonHeadResult"/> may be revealed; the tail judgement or break happens here.</summary>
        public double NonHeadRevealTime { get; init; }

        public bool BodyBroken { get; init; }

        public override Judgement CreateJudgement() => new IgnoreJudgement();
    }

    /// <summary>A press drawn as a single block, with no held span.</summary>
    public class EzReplayInputTapNote : EzReplayInputNoteObject
    {
    }

    /// <summary>A press held until <see cref="EndTime"/>; the playfield derives the drawn length from it.</summary>
    public class EzReplayInputHoldNote : EzReplayInputNoteObject, IHasDuration
    {
        public double EndTime { get; set; }

        public double Duration
        {
            get => EndTime - StartTime;
            set => EndTime = StartTime + value;
        }
    }
}
