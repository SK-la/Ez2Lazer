// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public class Ez2AcHoldNote : HoldNote
    {
        public Ez2AcHoldNote(HoldNote hold)
        {
            StartTime = hold.StartTime;
            Duration = hold.Duration;
            Column = hold.Column;
            NodeSamples = hold.NodeSamples;
        }

        protected override HitWindows CreateHitWindows() => new EzCustomHitWindows();

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            AddNested(Head = new Ez2AcLNHead
            {
                StartTime = StartTime,
                Column = Column,
                Samples = GetNodeSamples(0),
            });

            AddNested(Tail = new Ez2AcLNTail
            {
                StartTime = EndTime,
                Column = Column,
                Samples = GetNodeSamples((NodeSamples?.Count - 1) ?? 1),
            });

            AddNested(Body = new NoMissLNBody
            {
                StartTime = StartTime,
                Column = Column
            });
            // double interval = new BeatInterval().GetCurrentQuarterBeatInterval();
            //
            // // 按1/4节拍添加身体判定节点
            // for (double time = StartTime + interval; time < EndTime; time += interval)
            // {
            //     AddNested(new NoMissLNBody
            //     {
            //         StartTime = time,
            //         Column = Column
            //     });
            // }
        }
    }

    public class Ez2AcLNHead : HeadNote
    {
        public override Judgement CreateJudgement() => new Ez2AcHeadJudgement();
        protected override HitWindows CreateHitWindows() => new EzCustomHitWindows();

        private class Ez2AcHeadJudgement : ManiaJudgement
        {
            // public override HitResult MinResult => HitResult.IgnoreHit;
        }
    }

    public class Ez2AcLNTail : TailNote
    {
        protected override HitWindows CreateHitWindows() => new EzCustomHitWindows();
    }

    public class Ez2AcNote : Note
    {
        public override Judgement CreateJudgement() => new Ez2AcNoteJudgement();
        protected override HitWindows CreateHitWindows() => new EzCustomHitWindows();

        private class Ez2AcNoteJudgement : ManiaJudgement
        {
            // public override HitResult MinResult => HitResult.Pool;
        }
    }
}
