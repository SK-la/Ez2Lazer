// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Material
{
    /// <summary>
    /// 标记可声明 Acrylic 离屏承载需求的 HUD / UI 组件。
    /// 各组件通过自身 SettingSource 管理是否调用 <see cref="IMaterialCaptureRegistrar"/>。
    /// </summary>
    public interface IMaterialBackdropConsumer
    {
        /// <summary>
        /// 当前是否应向 <see cref="IMaterialCaptureRegistrar"/> 声明承载层需求。
        /// </summary>
        bool WantsMaterialCapture { get; }

        /// <summary>
        /// 在绑定项变化或加载完成时同步承载层引用计数与 Acrylic 绘制状态。
        /// </summary>
        void SyncMaterialCaptureState();
    }
}
