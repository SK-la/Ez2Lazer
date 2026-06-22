// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 单个 ghost 的分数处理器。对齐官方 <see cref="osu.Game.Online.Spectator.SpectatorScoreProcessor"/> 的模式：
    ///
    /// - 在构造时"绑定"到一个 <see cref="EzScoreRaceState"/>（对应官方的一个 spectated user）
    /// - 在 <see cref="Update()"/> 中使用 <see cref="Component.Clock"/> 查询当前时间，
    ///   暂停时 <see cref="Component.Clock"/> 自动停止前进，无需额外检查
    /// - 当 ghost state 被加入/移除字典时，由 HUD 层面创建/销毁此 processor
    /// - 按时钟查询 <c>State.Timeline</c>，驱动 <see cref="TotalScore"/> 等 bindable
    /// - HUD 组件直接订阅 processor bindable，不需要中间层
    /// </summary>
    public partial class EzScoreRaceTimelineScoreProcessor : CompositeDrawable
    {
        /// <summary>该 processor 绑定的 ghost 状态。</summary>
        public EzScoreRaceState? State { get; private set; }

        public readonly BindableLong TotalScore = new BindableLong { MinValue = 0 };
        public readonly BindableDouble Accuracy = new BindableDouble { MinValue = 0, MaxValue = 1 };
        public readonly BindableInt Combo = new BindableInt();
        public readonly BindableInt MissCount = new BindableInt();

        /// <summary>
        /// 绑定到一个 ghost state。
        /// </summary>
        public void BindTo(EzScoreRaceState state)
        {
            State = state;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
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

            if (state == null)
            {
                zeroBindables();
                return;
            }

            if (state.Timeline == null)
            {
                zeroBindables();
                return;
            }

            double currentTime = Clock?.CurrentTime ?? 0;

            if (double.IsNaN(currentTime))
                return;

            var snapshot = state.Timeline.QueryAtTime(currentTime);

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

        protected override void Update()
        {
            base.Update();
            UpdateScore();
        }
    }
}
