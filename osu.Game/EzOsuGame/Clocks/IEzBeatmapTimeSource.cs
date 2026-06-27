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
        /// 调试用：手动覆盖内部时钟当前时间。
        /// </summary>
        void SetCurrentTime(double time);
    }
}
