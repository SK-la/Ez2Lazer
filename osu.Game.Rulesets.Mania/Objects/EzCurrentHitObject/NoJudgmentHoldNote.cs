// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public class NoJudgmentNote : Note
    {
        public NoJudgmentNote(Note note)
        {
            StartTime = note.StartTime;
            Column = note.Column;
            Samples = note.Samples;
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
        }
    }

    public class CustomLNHead : HeadNote
    {
    }

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

    public class NoMissLNBody : HoldNoteBody
    {
        public override Judgement CreateJudgement() => new NoMissBodyJudgement();
        protected override HitWindows CreateHitWindows() => HitWindows.Empty;

        public class NoMissBodyJudgement : ManiaJudgement
        {
            public override HitResult MaxResult => HitResult.IgnoreHit;
            public override HitResult MinResult => HitResult.IgnoreMiss;
        }
    }

    public class NoJudgmentHoldNote : HoldNote
    {
        public NoJudgmentHoldNote(HoldNote hold)
        {
            StartTime = hold.StartTime;
            Duration = hold.Duration;
            Column = hold.Column;
            NodeSamples = hold.NodeSamples;
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            AddNested(Head = new CustomLNHead
            {
                StartTime = StartTime,
                Column = Column,
                Samples = GetNodeSamples(0),
            });

            AddNested(Tail = new NoComboBreakLNTail
            {
                StartTime = EndTime,
                Column = Column,
                Samples = GetNodeSamples((NodeSamples?.Count - 1) ?? 1),
            });

            AddNested(Body = new NoMissLNBody
            {
                StartTime = StartTime,
                Column = Column,
            });
        }
    }

    public partial class NoTailDrawableHoldNoteTail : DrawableHoldNoteTail
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            // apply perfect once the tail is reached
            if (HoldNote.Head.IsHit && timeOffset >= 0)
                ApplyResult(GetCappedResult(HitResult.Perfect));
            else
                base.CheckForResult(userTriggered, timeOffset);
        }
    }
}

