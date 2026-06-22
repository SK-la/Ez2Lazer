// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 全局 ghost 状态字典接口。对齐官方 <see cref="osu.Game.Online.Spectator.SpectatorClient.WatchedUserStates"/> 的设计：
    /// <see cref="EzScoreRaceService"/> 在选歌界面预加载后写入 <c>Guid → EzScoreRaceState</c>，
    /// HUD Processor 订阅此字典变化，自动获取 timeline 并按时钟驱动 bindable。
    ///
    /// Key = <c>ScoreInfo.ID</c>（Guid）。
    /// </summary>
    public interface IEzScoreRaceStateLookup
    {
        /// <summary>
        /// 当前可用 ghost 状态的字典。Processor 绑定此字典，当值添加/移除时自动创建/销毁 processor。
        /// </summary>
        IBindableDictionary<string, EzScoreRaceState> States { get; }
    }
}
