// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Timing;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 单个 ghost 的分数处理器。对齐官方 <see cref="osu.Game.Online.Spectator.SpectatorScoreProcessor"/> 的模式：
    ///
    /// - 接收外部注入的 <see cref="IClock"/>（来自 <see cref="GameplayClockContainer"/>），
    ///   暂停时该时钟停止前进，<see cref="UpdateScore"/> 自动得到旧值而不推进 ghost
    /// - 由 HUD 在自己的 <c>Update()</c> 里统一驱动所有 processor 的 <see cref="UpdateScore"/>
    ///   （对齐官方 <see cref="osu.Game.Screens.Play.Leaderboards.MultiSpectatorLeaderboardProvider"/> 的实现）
    /// </summary>
    public partial class EzScoreRaceTimelineScoreProcessor : Component
    {
        /// <summary>该 processor 绑定的 ghost 状态。</summary>
        public EzScoreRaceState? State { get; private set; }

        public readonly BindableLong TotalScore = new BindableLong { MinValue = 0 };
        public readonly BindableDouble Accuracy = new BindableDouble { MinValue = 0, MaxValue = 1 };
        public readonly BindableInt Combo = new BindableInt();
        public readonly BindableInt MissCount = new BindableInt();

        /// <summary>
        /// 暂停感知时钟。由 HUD 注入 <see cref="GameplayClockContainer"/>，
        /// 暂停时 <c>CurrentTime</c> 不再前进，<see cref="UpdateScore"/> 自然停止推进 ghost。
        /// </summary>
        public IClock? ReferenceClock { get; set; }

        /// <summary>
        /// 绑定到一个 ghost state。
        /// </summary>
        public void BindTo(EzScoreRaceState state)
        {
            State = state;
            UpdateScore();
        }

        /// <summary>
        /// 重置 processor，断开 ghost state 绑定。
        /// </summary>
        public void Reset()
        {
            State = null;
            zeroBindables();
        }

        public void UpdateScore()
        {
            var state = State;

            if (state?.Timeline == null || ReferenceClock == null)
            {
                zeroBindables();
                return;
            }

            double currentTime = ReferenceClock.CurrentTime;

            if (double.IsNaN(currentTime))
            {
                zeroBindables();
                return;
            }

            var snapshot = state.Timeline.QueryAtTime(currentTime);

            // 对齐官方 SpectatorScoreProcessor：直接赋值。
            // 框架 Bindable<T>.Value setter 内置 EqualityComparer 去重，
            // 值不变时不会触发 ValueChanged 事件链，无需手动条件判断。
            TotalScore.Value = snapshot.TotalScore;
            Accuracy.Value = snapshot.Accuracy;
            Combo.Value = snapshot.HighestCombo;
            MissCount.Value = snapshot.MissCount;
        }

        private void zeroBindables()
        {
            TotalScore.Value = 0;
            Accuracy.Value = 0;
            Combo.Value = 0;
            MissCount.Value = 0;
        }
    }
}
