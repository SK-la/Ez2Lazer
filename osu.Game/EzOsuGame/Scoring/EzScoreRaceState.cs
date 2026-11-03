// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 单个 ghost 成绩的元数据 + 可选 timeline。
    /// HUD 通过 <see cref="EzScoreRaceTimelineScoreProcessor"/> 读取 timeline，不直接使用本类型上的 bindable。
    /// </summary>
    public class EzScoreRaceState
    {
        /// <summary>Ghost 成绩的 ScoreInfo，用于 HUD 显示玩家名、日期等元数据。</summary>
        public ScoreInfo ScoreInfo { get; }

        /// <summary>预构建的 timeline；进局 build 完成前为 null，就绪后由 <see cref="EzScoreRaceService"/> 写入。</summary>
        public EzScoreTimeline? Timeline { get; set; }

        public EzScoreRaceState(ScoreInfo scoreInfo, EzScoreTimeline? timeline)
        {
            ScoreInfo = scoreInfo;
            Timeline = timeline;
        }
    }
}
