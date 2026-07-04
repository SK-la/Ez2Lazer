// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Timing;

namespace osu.Game.EzOsuGame.Clocks
{
    /// <summary>
    /// [Ez] 由 <see cref="osu.Game.Screens.Play.MasterGameplayClockContainer"/> 注入的谱面时钟接口。
    ///
    /// FrameStabilityContainer 等下游组件通过 DI 拿到这个接口，决定是否把它作为 referenceClock 使用。
    /// 当 <see cref="osu.Game.Screens.Play.MasterGameplayClockContainer"/> 因 multiplayer / 音频时基而未注入谱面时钟时，
    /// DI 解析会拿到 null。
    /// </summary>
    public interface IEzBeatmapTimeSource : IAdjustableClock, IFrameBasedClock
    {
        /// <summary>
        /// 当前谱面时钟是否被启用。
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// 暂停恢复时的虚拟 lead-in 窗口（毫秒）。设为 0 关闭过渡。
        /// </summary>
        double ResumeLeadInWindowMs { get; set; }

        /// <summary>
        /// 进入暂停时记录的真实谱面时间 T（毫秒）。
        /// </summary>
        double PauseGameplayTime { get; }

        /// <summary>
        /// FSC 是否正在执行恢复暂停后的空白下落过渡。
        /// </summary>
        bool IsInResumeLeadIn { get; }

        /// <summary>
        /// 恢复 lead-in 结束且谱面时钟可继续推进时触发（用于同步启动音频 track）。
        /// </summary>
        event Action? ResumeLeadInCompleted;

        /// <summary>
        /// 进入暂停时由 <see cref="PauseStateTracker"/> 调用，记录暂停位置。
        /// </summary>
        void OnGameplayPaused(double time);

        /// <summary>
        /// 从暂停恢复时由 <see cref="PauseStateTracker"/> 调用，冻结 gameplay 时间并交由 FSC 驱动显示时间。
        /// </summary>
        void BeginResumeLeadIn();

        /// <summary>
        /// 由 FSC 在显示时间追上 <see cref="PauseGameplayTime"/> 后调用。
        /// </summary>
        void EndResumeLeadIn();

        /// <summary>
        /// 调试用：手动覆盖内部时钟当前时间。
        /// </summary>
        void SetCurrentTime(double time);
    }
}
