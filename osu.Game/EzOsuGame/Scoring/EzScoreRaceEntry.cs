// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    public sealed class EzScoreRaceEntry
    {
        public ScoreInfo ScoreInfo { get; }
        public bool Tracked { get; internal set; }

        /// <summary>
        /// true 表示该 entry 已被 Session 标记为 "timeline 构建中"，HUD 订阅 TimelineReady 事件
        /// 后会拿到最终结果，不需要轮询 Timeline 属性。
        /// </summary>
        public bool IsTimelinePending { get; internal set; }

        private EzScoreTimeline? timeline;

        /// <summary>
        /// 已构建好的 timeline。Volatile 读写保证：assignTimeline（主线程）写入后，任何
        /// HUD/Processor 读取（包括理论上跨线程读取）能立即看到最新值。
        /// </summary>
        public EzScoreTimeline? Timeline
        {
            get => Volatile.Read(ref timeline);
            internal set => Volatile.Write(ref timeline, value);
        }

        public EzScoreRaceEntry(ScoreInfo scoreInfo, EzScoreTimeline? timeline = null, bool tracked = false)
        {
            ScoreInfo = scoreInfo;
            this.timeline = timeline;
            Tracked = tracked;
        }
    }
}