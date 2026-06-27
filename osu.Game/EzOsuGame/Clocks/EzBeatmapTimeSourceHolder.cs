// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;

namespace osu.Game.EzOsuGame.Clocks
{
    /// <summary>
    /// [Ez] 把 <see cref="EzBeatmapTimeSource"/> 通过 DI 暴露给子容器的薄容器。
    ///
    /// <see cref="osu.Game.Screens.Play.MasterGameplayClockContainer"/> 在切换为「谱面时基」时会把它作为 internal child，
    /// 下游（FrameStabilityContainer 等）通过 DI 解析 <see cref="IEzBeatmapTimeSource"/> 拿到谱面时钟引用，
    /// 决定是否把它作为 referenceClock。
    /// </summary>
    internal sealed partial class EzBeatmapTimeSourceHolder : Component
    {
        [Cached(typeof(IEzBeatmapTimeSource))]
        public IEzBeatmapTimeSource BeatmapTimeSource { get; }

        public EzBeatmapTimeSourceHolder(IEzBeatmapTimeSource source)
        {
            BeatmapTimeSource = source;
        }
    }
}
