// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.BMS.Objects
{
    /// <summary>
    /// A long note (hold note) in BMS.
    /// </summary>
    public class BMSHoldNote : BMSHitObject, IHasDuration
    {
        public double EndTime
        {
            get => StartTime + Duration;
            set => Duration = value - StartTime;
        }

        private double duration;

        public double Duration
        {
            get => duration;
            set => duration = value;
        }

        /// <summary>
        /// The head of the hold note.
        /// </summary>
        public BMSHoldNoteHead? Head { get; private set; }

        /// <summary>
        /// The tail of the hold note.
        /// </summary>
        public BMSHoldNoteTail? Tail { get; private set; }

        /// <summary>
        /// The body of the hold note.
        /// </summary>
        public BMSHoldNoteBody? Body { get; private set; }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            base.CreateNestedHitObjects(cancellationToken);

            AddNested(Head = new BMSHoldNoteHead
            {
                StartTime = StartTime,
                Column = Column,
                Samples = Samples,
            });

            AddNested(Body = new BMSHoldNoteBody
            {
                StartTime = StartTime,
                Column = Column,
                Duration = Duration,
            });

            AddNested(Tail = new BMSHoldNoteTail
            {
                StartTime = EndTime,
                Column = Column,
            });
        }

        public override Judgement CreateJudgement() => new IgnoreJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;
    }

    /// <summary>
    /// The head of a BMS hold note.
    /// </summary>
    public class BMSHoldNoteHead : BMSNote
    {
    }

    /// <summary>
    /// The body of a BMS hold note.
    /// </summary>
    public class BMSHoldNoteBody : BMSHitObject, IHasDuration
    {
        public double EndTime
        {
            get => StartTime + Duration;
            set => Duration = value - StartTime;
        }

        public double Duration { get; set; }

        public override Judgement CreateJudgement() => new IgnoreJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;
    }

    /// <summary>
    /// The tail of a BMS hold note.
    /// </summary>
    public class BMSHoldNoteTail : BMSNote
    {
        public override Judgement CreateJudgement() => new Judgement();
    }
}
