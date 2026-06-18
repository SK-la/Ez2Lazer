// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 在 <see cref="GameplayClockContainer"/> 子树内按谱面时钟查询 ghost timeline，驱动 bindable 分数（对齐观战 <see cref="osu.Game.Online.Spectator.SpectatorScoreProcessor"/>）。
    /// </summary>
    public partial class EzScoreRaceTimelineScoreProcessor : Component
    {
        public readonly BindableLong TotalScore = new BindableLong { MinValue = 0 };

        public readonly BindableDouble Accuracy = new BindableDouble { MinValue = 0, MaxValue = 1 };

        public readonly BindableInt Combo = new BindableInt();

        public readonly BindableInt MissCount = new BindableInt();

        private EzScoreTimeline? timeline;

        public void SetTimeline(EzScoreTimeline? timeline)
        {
            this.timeline = timeline;

            if (IsLoaded)
                UpdateScore();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
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

            if (Clock == null)
                return;

            var snapshot = timeline.QueryAtTime(Clock.CurrentTime);

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
