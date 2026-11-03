// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Osu.Replays
{
    /// <summary>
    /// 生成自动播放回放需要不合理数量的帧时抛出。
    /// </summary>
    /// <remarks>
    /// 生成器按整段时长采样滑条，滑条持续数天的谱面会要数十亿帧。回放涨到那么大时会在运行时内耗尽内存，
    /// 以 execution-engine 失败而非可捕获异常结束进程，会话随之丢失。改为报告问题可让游戏继续运行，
    /// 并指出可规避该问题的 mod。
    /// </remarks>
    public class AutoplayFrameLimitExceededException : InvalidOperationException
    {
        public AutoplayFrameLimitExceededException(double estimatedFrameCount, int limit)
            : base($"Generating autoplay for this beatmap needs roughly {estimatedFrameCount:N0} replay frames, above the "
                   + $"{limit:N0} frame limit. Its sliders last far longer than the beatmap, so play it with the Aspire "
                   + "mod's autoplay instead of the Autoplay mod.")
        {
        }
    }
}
