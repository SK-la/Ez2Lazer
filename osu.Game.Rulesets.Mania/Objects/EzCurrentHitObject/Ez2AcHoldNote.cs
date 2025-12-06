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
            public override HitResult MaxResult => HitResult.Perfect;
            public override HitResult MinResult => HitResult.IgnoreMiss;
        }
    }

    public class Ez2AcLNTail : TailNote
    {
        public override Judgement CreateJudgement() => new Ez2AcTailJudgement();
        protected override HitWindows CreateHitWindows() => new EzCustomHitWindows();

        private class Ez2AcTailJudgement : ManiaJudgement
        {
            public override HitResult MaxResult => HitResult.Perfect;
            public override HitResult MinResult => HitResult.ComboBreak;
        }
    }

    public class Ez2AcNote : Note
    {
        public Ez2AcNote(Note note)
        {
            StartTime = note.StartTime;
            Column = note.Column;
            Samples = note.Samples;
        }

        public override Judgement CreateJudgement() => new Ez2AcJudgement();
        protected override HitWindows CreateHitWindows() => new EzCustomHitWindows();

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
        }
    }

    public class Ez2AcJudgement : ManiaJudgement
    {
        public override HitResult MaxResult => HitResult.Perfect;
        public override HitResult MinResult => HitResult.Pool;

        protected override double HealthIncreaseFor(HitResult result)
        {
            switch (result)
            {
                // case HitResult.Pool:
                //     return -DEFAULT_MAX_HEALTH_INCREASE * 5;

                case HitResult.Miss:
                    return -DEFAULT_MAX_HEALTH_INCREASE * 3;

                case HitResult.Meh:
                    return -DEFAULT_MAX_HEALTH_INCREASE * 2;

                case HitResult.Ok:
                    return -DEFAULT_MAX_HEALTH_INCREASE * 1;

                case HitResult.Good:
                    return DEFAULT_MAX_HEALTH_INCREASE * 0.1;

                case HitResult.Great:
                    return DEFAULT_MAX_HEALTH_INCREASE * 0.8;

                case HitResult.Perfect:
                    return DEFAULT_MAX_HEALTH_INCREASE;

                default:
                    return base.HealthIncreaseFor(result);
            }
        }
    }
}
