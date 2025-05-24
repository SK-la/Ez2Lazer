// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects
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

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            AddNested(Head = new CustomLNHead
            {
                StartTime = StartTime,
                Column = Column,
                Samples = GetNodeSamples(0),
            });

            AddNested(Tail = new Ez2AcHoldNoteTail
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

    public class Ez2AcHoldNoteTail : TailNote
    {
        public override Judgement CreateJudgement() => new Ez2AcTailJudgement();
        protected override HitWindows CreateHitWindows() => HitWindows.Empty;

        private class Ez2AcTailJudgement : ManiaJudgement
        {
            public override HitResult MaxResult => HitResult.IgnoreHit;
            public override HitResult MinResult => HitResult.IgnoreMiss;
        }
    }

    public partial class Ez2AcDrawableHoldNoteBody : DrawableHoldNoteBody
    {
        internal new void TriggerResult(bool hit)
        {
            if (AllJudged) return;

            ApplyMaxResult();
            // ApplyResult(HitResult.Perfect);
        }
    }

    public partial class Ez2AcDrawableHoldNoteTail : DrawableHoldNoteTail
    {
        public static HitWindows HitWindows = new ManiaHitWindows();

        // public override bool DisplayResult => false;
        // public override bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        // {
        //     return UpdateResult(true);
        // }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (HoldNote.IsHolding.Value && timeOffset >= 0)
            {
                ApplyResult(GetCappedResult(HitResult.Perfect));
                return;
            }

            if (!HoldNote.IsHolding.Value && timeOffset < 0)
            {
                ApplyResult(GetCappedResult(HitResult.ComboBreak));
                return;
            }

            if (HoldNote.Head.IsHit && Math.Abs(timeOffset) < Math.Abs(HitWindows.WindowFor(HitResult.Meh) * TailNote.RELEASE_WINDOW_LENIENCE))
            {
                ApplyMaxResult();
            }
            else
            {
                ApplyMinResult();
            }
        }
    }
}
