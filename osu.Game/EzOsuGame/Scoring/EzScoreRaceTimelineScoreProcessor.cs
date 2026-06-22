// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
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
        /// 传入 null 或 Rebind() 时，<see cref="Update"/> 会自动检测并回落到 <see cref="IComponent.Clock"/>。
        /// </summary>
        public void SetGameplayClock(GameplayClockContainer? container)
        {
            gameplayClockContainer = container;
            UpdateScore();
        }

        /// <summary>
        /// 解除当前 GameplayClockContainer 引用，强制回落 Component.Clock。
        /// 用于 Player 切换 / 皮肤编辑器场景切换时主动清理，防止指向已 dispose 的旧 clock。
        /// </summary>
        public void Rebind()
        {
            gameplayClockContainer = null;
            UpdateScore();
        }

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

            double currentTime = resolveCurrentTime();

            if (double.IsNaN(currentTime))
                return;

            var snapshot = timeline.QueryAtTime(currentTime);

            TotalScore.Value = snapshot.TotalScore;
            Accuracy.Value = snapshot.Accuracy;
            Combo.Value = snapshot.HighestCombo;
            MissCount.Value = snapshot.MissCount;
        }

        /// <summary>
        /// 优先使用 <see cref="GameplayClockContainer.CurrentTime"/>；若被 dispose 或未注入则回落到 Component.Clock。
        /// </summary>
        private double resolveCurrentTime()
        {
            if (gameplayClockContainer != null)
            {
                try
                {
                    // Drawable 的 IsDisposed 是 internal；通过访问 CurrentTime 触发 ObjectDisposedException 来检测。
                    return gameplayClockContainer.CurrentTime;
                }
                catch (ObjectDisposedException)
                {
                    gameplayClockContainer = null;
                }
                catch (InvalidOperationException)
                {
                    // 当前 GameplayClockContainer 处于未加载完成 / 已卸载等状态，强制回落。
                    gameplayClockContainer = null;
                }
            }

            return Clock.CurrentTime;
        }

        protected override void Update()
        {
            base.Update();
            UpdateScore();
        }
    }
}
