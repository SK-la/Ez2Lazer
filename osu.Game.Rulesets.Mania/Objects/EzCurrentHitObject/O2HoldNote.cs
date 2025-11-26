// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public class O2HoldNote : HoldNote
    {
        public O2HoldNote(HoldNote hold)
        {
            StartTime = hold.StartTime;
            Duration = hold.Duration;
            Column = hold.Column;
            NodeSamples = hold.NodeSamples;
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            AddNested(Head = new O2LNHead
            {
                StartTime = StartTime,
                Column = Column,
                Samples = GetNodeSamples(0)
            });

            AddNested(Tail = new O2LNTail
            {
                StartTime = EndTime,
                Column = Column,
                Samples = GetNodeSamples(NodeSamples?.Count - 1 ?? 1)
            });

            AddNested(Body = new HoldNoteBody
            {
                StartTime = StartTime,
                Column = Column
            });
        }

        public override double MaximumJudgementOffset => base.MaximumJudgementOffset / TailNote.RELEASE_WINDOW_LENIENCE;
    }
}
