// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// ReplayFrame 喂入策略：Session / Race 共用同一抽象。
    /// </summary>
    public enum EzReplayFeedMode
    {
        /// <summary>
        /// 一次读完 Frames → 边沿事件后全量仿真（当前 Session / Race 预建 timeline 默认路径）。
        /// </summary>
        BatchAllEvents = 0,

        /// <summary>
        /// 按外部 clock（或游标）推进帧序列；Race 进局时不阻塞等完整 timeline，后台喂入就绪。
        /// </summary>
        StreamByClock = 1,
    }
}
