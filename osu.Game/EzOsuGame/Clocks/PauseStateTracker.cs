// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.Clocks
{
    /// <summary>
    /// [Ez] 暂停状态跟踪器：监听暂停 Bindable 的 paused ↔ resumed 转换，
    /// 并向 <see cref="EzBeatmapTimeSource"/> 报告暂停位置 / 触发恢复 lead-in。
    /// </summary>
    internal sealed class PauseStateTracker : IDisposable
    {
        private readonly EzBeatmapTimeSource owner;
        private readonly IBindable<bool> isPaused;

        private bool wasPaused;

        /// <param name="owner">待驱动的谱面时钟。</param>
        /// <param name="isPaused">暂停状态 Bindable。</param>
        public PauseStateTracker(EzBeatmapTimeSource owner, IBindable<bool> isPaused)
        {
            this.owner = owner;
            this.isPaused = isPaused;
            isPaused.BindValueChanged(e =>
            {
                if (!wasPaused && e.NewValue)
                    owner.OnGameplayPaused(owner.CurrentTime);

                if (wasPaused && !e.NewValue)
                    owner.BeginResumeLeadIn();

                wasPaused = e.NewValue;
            }, true);
        }

        public void Dispose()
        {
            isPaused.UnbindAll();
        }
    }
}
