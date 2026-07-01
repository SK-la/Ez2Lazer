// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    // TODO: 需要重新审视Replay结构体，是否必须使用，有无现成的载体复用，而不是在此定义一个新的结构体。
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
