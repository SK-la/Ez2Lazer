// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    public readonly struct ManiaReplayInputEvent
    {
        public double Time { get; }

        public int Column { get; }

        public bool IsPress { get; }

        public ManiaReplayInputEvent(double time, int column, bool isPress)
        {
            Time = time;
            Column = column;
            IsPress = isPress;
        }
    }
}
