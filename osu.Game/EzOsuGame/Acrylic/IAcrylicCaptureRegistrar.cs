// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Acrylic
{
    /// <summary>
    /// 按需激活 Acrylic 离屏承载层的注册点。
    /// 由实际使用 Framework <c>AcrylicBackdropDrawable</c> 的组件在需要虚化时 <see cref="AcquireCapture"/>，
    /// 不再需要时 <see cref="ReleaseCapture"/>；引用计数归零时不分配全屏 FBO。
    /// </summary>
    public interface IAcrylicCaptureRegistrar
    {
        /// <summary>
        /// 声明一个活跃 Acrylic 消费者需要离屏承载层。
        /// </summary>
        void AcquireCapture();

        /// <summary>
        /// 释放先前由 <see cref="AcquireCapture"/> 声明的需求。
        /// </summary>
        void ReleaseCapture();
    }
}
