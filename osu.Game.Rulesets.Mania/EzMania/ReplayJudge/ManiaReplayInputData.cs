// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// Replay 帧序列解析后的单个输入边沿事件（press / release）。
    /// 由 <see cref="ManiaReplayFrameEdgeParser"/> 产出，<see cref="ManiaReplaySessionSimulator"/> 消费。
    /// </summary>
    internal readonly struct ManiaReplayInputEvent
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

    /// <summary>
    /// 单次 replay 解析产出的预计算数据，供判定 Session 共享。
    /// 避免 <see cref="ManiaReplaySessionSimulator"/> 与 <see cref="ManiaReplaySession"/> 各自重复解析。
    /// </summary>
    /// <param name="SortedFrameTimes">
    /// replay 帧钟（含帧偏置），升序。stable 的被动判定挂在引擎帧循环上（如 classic 长条到点判尾取
    /// <c>帧钟 &gt;= EndTime + HitWindow100</c>），只有边沿时刻不足以还原，故与边沿并列保留。
    /// </param>
    internal sealed record ManiaReplayInputData(
        List<ManiaReplayInputEvent> SortedEvents,
        Dictionary<int, List<double>> PressTimesByColumn,
        List<double> SortedFrameTimes);
}
