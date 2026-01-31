// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.StateChanges;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.BMS.Replays
{
    public class BMSAutoGenerator : AutoGenerator<BMSReplayFrame>
    {
        public BMSAutoGenerator(IBeatmap beatmap)
            : base(beatmap)
        {
        }

        protected override void GenerateFrames()
        {
            if (Beatmap.HitObjects.Count == 0)
                return;

            var currentActions = new List<BMSAction>();

            foreach (var hitObject in Beatmap.HitObjects.OfType<BMSHitObject>())
            {
                var action = GetActionForColumn(hitObject.Column);

                // Press
                currentActions.Add(action);
                Frames.Add(new BMSReplayFrame(hitObject.StartTime, currentActions.ToList()));

                // Release
                double releaseTime = hitObject is BMSHoldNote holdNote
                    ? holdNote.EndTime
                    : hitObject.StartTime + 50;

                currentActions.Remove(action);
                Frames.Add(new BMSReplayFrame(releaseTime, currentActions.ToList()));
            }
        }

        private static BMSAction GetActionForColumn(int column)
        {
            return column switch
            {
                0 => BMSAction.Scratch1,
                1 => BMSAction.Key1,
                2 => BMSAction.Key2,
                3 => BMSAction.Key3,
                4 => BMSAction.Key4,
                5 => BMSAction.Key5,
                6 => BMSAction.Key6,
                7 => BMSAction.Key7,
                _ => BMSAction.Key1
            };
        }
    }

    public class BMSReplayFrame : ReplayFrame
    {
        public List<BMSAction> Actions { get; }

        public BMSReplayFrame(double time, List<BMSAction> actions)
            : base(time)
        {
            Actions = actions;
        }
    }

    public class BMSFramedReplayInputHandler : FramedReplayInputHandler<BMSReplayFrame>
    {
        public BMSFramedReplayInputHandler(Replay replay)
            : base(replay)
        {
        }

        protected override bool IsImportant(BMSReplayFrame frame) => frame.Actions.Any();

        protected override void CollectReplayInputs(List<IInput> inputs)
        {
            inputs.Add(new ReplayState<BMSAction> { PressedActions = CurrentFrame?.Actions ?? new List<BMSAction>() });
        }
    }
}
