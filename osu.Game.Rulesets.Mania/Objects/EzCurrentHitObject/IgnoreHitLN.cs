// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    // 注意注册顺序必须是：头 尾 身体
    public class IgnoreHitLN : HoldNote
    {
        public IgnoreHitLN(HoldNote hold)
        {
            StartTime = hold.StartTime;
            Duration = hold.Duration;
            Column = hold.Column;
            NodeSamples = hold.NodeSamples;
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            AddNested(Head = new HeadNote
            {
                StartTime = StartTime,
                Column = Column,
                Samples = GetNodeSamples(0),
            });

            AddNested(Tail = new IgnoreHitLNTail
            {
                StartTime = EndTime,
                Column = Column,
                Samples = GetNodeSamples((NodeSamples?.Count - 1) ?? 1),
            });

            AddNested(Body = new HoldNoteBody
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
}
