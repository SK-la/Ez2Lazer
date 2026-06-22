// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 单个 ghost 成绩的完整状态快照。对齐官方 <see cref="osu.Game.Online.Spectator.SpectatorState"/> 的设计：
    /// 包含 ScoreInfo 元数据 + 已构建的 timeline + 按时钟驱动更新的 bindable 分数。
    ///
    /// 由 <see cref="EzScoreRaceService"/> 在选歌界面预加载后写入 <see cref="EzScoreRaceService.States"/>；
    /// <see cref="EzScoreRaceTimelineScoreProcessor"/> 订阅 <see cref="EzScoreRaceService.States"/> 的变化，
    /// 按时钟查询 timeline 并驱动 <see cref="TotalScore"/> 等 bindable；
    /// HUD 组件直接订阅 processor 的 bindable，不需要中间层。
    /// </summary>
    public class EzScoreRaceState
    {
        /// <summary>Ghost 成绩的 ScoreInfo，用于 HUD 显示玩家名、日期等元数据。</summary>
        public ScoreInfo ScoreInfo { get; }

        /// <summary>预构建的 timeline，进入游戏后无需 IO，直接按谱面时钟查询。</summary>
        public EzScoreTimeline? Timeline { get; }

        /// <summary>该 ghost 在当前谱面时钟下的实时总分 bindable。</summary>
        public readonly BindableLong TotalScore = new BindableLong { MinValue = 0 };

        /// <summary>该 ghost 在当前谱面时钟下的实时 Accuracy bindable。</summary>
        public readonly BindableDouble Accuracy = new BindableDouble { MinValue = 0, MaxValue = 1 };

        /// <summary>该 ghost 在当前谱面时钟下的实时 Combo bindable。</summary>
        public readonly BindableInt Combo = new BindableInt();

        /// <summary>该 ghost 在当前谱面时钟下的实时 MissCount bindable。</summary>
        public readonly BindableInt MissCount = new BindableInt();

        public EzScoreRaceState(ScoreInfo scoreInfo, EzScoreTimeline? timeline)
        {
            ScoreInfo = scoreInfo;
            Timeline = timeline;
        }
    }
}
