// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Replays;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    public static class ManiaReplayInputParser
    {
        public static List<ManiaReplayInputEvent> Parse(Replay replay)
        {
            var frames = replay.Frames.OfType<ManiaReplayFrame>().OrderBy(f => f.Time).ToList();
            var inputEvents = new List<ManiaReplayInputEvent>(frames.Count * 2);
            var lastActions = new HashSet<ManiaAction>();

            foreach (var frame in frames)
            {
                var current = new HashSet<ManiaAction>(frame.Actions);

                foreach (var action in current)
                {
                    if (lastActions.Contains(action))
                        continue;

                    int column = (int)action;

                    if (column >= 0)
                        inputEvents.Add(new ManiaReplayInputEvent(frame.Time, column, true));
                }

                foreach (var action in lastActions)
                {
                    if (current.Contains(action))
                        continue;

                    int column = (int)action;

                    if (column >= 0)
                        inputEvents.Add(new ManiaReplayInputEvent(frame.Time, column, false));
                }

                lastActions = current;
            }

            if (lastActions.Count > 0)
            {
                double endTime = frames[^1].Time;

                foreach (var action in lastActions)
                {
                    int column = (int)action;

                    if (column >= 0)
                        inputEvents.Add(new ManiaReplayInputEvent(endTime, column, false));
                }
            }

            inputEvents.Sort((a, b) =>
            {
                int timeComparison = a.Time.CompareTo(b.Time);
                if (timeComparison != 0)
                    return timeComparison;

                if (a.IsPress == b.IsPress)
                    return 0;

                return a.IsPress ? -1 : 1;
            });

            return inputEvents;
        }
    }
}
