// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 单次 replay 解析产出的预计算数据，供判定 Session 共享。
    /// 避免 <see cref="ManiaReplaySessionSimulator"/> 与 <see cref="ManiaReplaySession"/> 各自重复解析。
    /// </summary>
    internal sealed record ManiaReplayInputData(
        List<ManiaReplayInputEvent> SortedEvents,
        Dictionary<int, List<double>> PressTimesByColumn);
}
