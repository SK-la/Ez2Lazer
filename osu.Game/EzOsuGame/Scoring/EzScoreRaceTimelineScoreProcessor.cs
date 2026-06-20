// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 在 <see cref="osu.Game.Screens.Play.GameplayClockContainer"/> 子树内按谱面时钟查询 ghost timeline，驱动 bindable 分数（对齐观战 <see cref="osu.Game.Online.Spectator.SpectatorScoreProcessor"/>）。
    /// </summary>
    public partial class EzScoreRaceTimelineScoreProcessor : Component
    {
        public readonly BindableLong TotalScore = new BindableLong { MinValue = 0 };

        public readonly BindableDouble Accuracy = new BindableDouble { MinValue = 0, MaxValue = 1 };

        public readonly BindableInt Combo = new BindableInt();

        public readonly BindableInt MissCount = new BindableInt();

        private EzScoreTimeline? timeline;

        private GameplayClockContainer? gameplayClockContainer;

        /// <summary>
        /// 设置外部 <see cref="GameplayClockContainer"/> 引用，优先于 Component.Clock 查询当前时间。
        /// </summary>
        public void SetGameplayClock(GameplayClockContainer? container)
        {
            gameplayClockContainer = container;
        }

        public void SetTimeline(EzScoreTimeline? timeline)
        {
            this.timeline = timeline;

            if (timeline != null)
            {
                Logger.Log(
                    $"[EzScore] SetTimeline: score has {timeline.FinalTotalScore} final total, snapshots count available",
                    level: LogLevel.Debug,
                    name: Ez2ConfigManager.LOGGER_NAME);
            }
            else
            {
                Logger.Log(
                    "[EzScore] SetTimeline: timeline set to null",
                    level: LogLevel.Debug,
                    name: Ez2ConfigManager.LOGGER_NAME);
            }

            if (IsLoaded)
                UpdateScore();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Logger.Log(
                $"[EzScore] TimelineScoreProcessor.LoadComplete: clock={(gameplayClockContainer != null ? "GameplayClock" : Clock != null ? "Component.Clock" : "null")}",
                level: LogLevel.Debug,
                name: Ez2ConfigManager.LOGGER_NAME);
            UpdateScore();
        }

        public void UpdateScore()
        {
            if (timeline == null)
            {
                TotalScore.Value = 0;
                Accuracy.Value = 0;
                Combo.Value = 0;
                MissCount.Value = 0;
                return;
            }

            double currentTime;

            if (gameplayClockContainer != null)
                currentTime = gameplayClockContainer.CurrentTime;
            else if (Clock != null)
                currentTime = Clock.CurrentTime;
            else
            {
                Logger.Log(
                    "[EzScore] UpdateScore: both GameplayClock and Component.Clock are null, cannot query timeline",
                    level: LogLevel.Debug,
                    name: Ez2ConfigManager.LOGGER_NAME);
                return;
            }

            var snapshot = timeline.QueryAtTime(currentTime);

            TotalScore.Value = snapshot.TotalScore;
            Accuracy.Value = snapshot.Accuracy;
            Combo.Value = snapshot.HighestCombo;
            MissCount.Value = snapshot.MissCount;
        }

        protected override void Update()
        {
            base.Update();
            UpdateScore();
        }
    }
}
