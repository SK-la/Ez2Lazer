// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 全局 ghost 状态字典接口。
    /// <see cref="EzScoreRaceService"/> 在选歌阶段发布元数据、进局后增量写入 timeline。
    /// </summary>
    public interface IEzScoreRaceStateLookup
    {
        IBindableDictionary<string, EzScoreRaceState> States { get; }
    }
}
