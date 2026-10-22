// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Rendering;

namespace osu.Game.EzOsuGame.Material
{
    /// <summary>
    /// Shared acquire/release logic for <see cref="EzMaterialContainer"/>.
    /// D3D11, Metal, Vulkan, and desktop OpenGL sample a shared backbuffer blur and do not acquire the fullscreen capture.
    /// Deferred renderers still acquire <see cref="IMaterialCaptureRegistrar"/>.
    /// </summary>
    internal sealed class EzMaterialCaptureController
    {
        private readonly IMaterialCaptureRegistrar? registrar;
        private readonly IRenderer? renderer;
        private bool captureAcquired;

        public EzMaterialCaptureController(IMaterialCaptureRegistrar? registrar, IRenderer? renderer = null)
        {
            this.registrar = registrar;
            this.renderer = renderer;
        }

        /// <summary>
        /// 材质是否可以安全地采样下层内容：能拷后缓冲、无需承载层，或承载层已就绪。
        /// </summary>
        public bool CanDraw => renderer?.SupportsBackbufferRegionCopy == true || registrar == null || captureAcquired;

        public void Sync(bool wantsCapture)
        {
            if (!wantsCapture)
            {
                releaseCapture();
                return;
            }

            // Deferred renderers never report backbuffer-region copy support and keep acquiring the capture.
            if (renderer?.SupportsBackbufferRegionCopy == true)
            {
                releaseCapture();
                return;
            }

            if (!captureAcquired && registrar != null)
            {
                registrar.AcquireCapture();
                captureAcquired = true;
            }
        }

        private void releaseCapture()
        {
            if (!captureAcquired || registrar == null)
                return;

            registrar.ReleaseCapture();
            captureAcquired = false;
        }

        public void Dispose()
        {
            releaseCapture();
        }
    }
}
