// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
        /// MasterGameplayClockContainer 在 multiplayer / 音频时基下不会构造谱面时钟；
        /// 对已经构造的谱面时钟，这个开关也用于临时禁用（暂停 / seek 等）。
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// 暂停恢复时的虚拟 lead-in 窗口（毫秒）。
        /// 恢复后，CurrentTime 在这个窗口内逐步推进（smooth catch-up），不写回 SourceClock。
        /// 设为 0 或负值表示关闭虚拟 lead-in（直接 catch-up）。
        /// </summary>
        double ResumeLeadInWindowMs { get; set; }

        /// <summary>
        /// 当前是否处于「暂停恢复 lead-in」阶段。
        /// </summary>
        bool IsResuming { get; }

        /// <summary>
        /// 当 SourceClock != null 时，记录暂停位置（由 ProcessFrame 自动处理）。
        /// 当 SourceClock == null 时，由容器在 GameplayClock.Stop() 时显式调用。
        /// </summary>
        /// <param name="pausePosition">SourceClock 在暂停瞬间的时间（毫秒）。</param>
        void RecordPause(double pausePosition);

        /// <summary>
        /// 重置 lead-in 状态。SourceClock != null 时由 ProcessFrame 自动触发；
        /// SourceClock == null 时由容器在 seek 等操作时显式调用。
        /// </summary>
        void ResetLeadIn();

        /// <summary>
        /// 调试用：手动覆盖内部时钟当前时间。
        /// </summary>
        void SetCurrentTime(double time);
    }
}
