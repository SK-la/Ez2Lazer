// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osuTK;

namespace osu.Game.EzOsuGame.Acrylic
{
    /// <summary>
    /// Shared acquire/release logic for <see cref="AcrylicBackdropDrawable"/> consumers.
    /// D3D11, Metal, Vulkan, and desktop OpenGL sample a shared backbuffer blur and do not acquire the fullscreen capture.
    /// Deferred renderers still acquire <see cref="IAcrylicCaptureRegistrar"/>.
    /// </summary>
    internal sealed class EzAcrylicCaptureController
    {
        private readonly IAcrylicCaptureRegistrar? registrar;
        private readonly AcrylicBackdropDrawable acrylicBackdrop;
        private readonly IRenderer? renderer;
        private bool captureAcquired;

        public EzAcrylicCaptureController(IAcrylicCaptureRegistrar? registrar, AcrylicBackdropDrawable acrylicBackdrop, IRenderer? renderer = null)
        {
            this.registrar = registrar;
            this.acrylicBackdrop = acrylicBackdrop;
            this.renderer = renderer;
        }

        public void Sync(bool wantsCapture, float blurStrength)
        {
            acrylicBackdrop.BlurSigma = new Vector2(blurStrength);

            if (!wantsCapture)
            {
                acrylicBackdrop.EffectEnabled = false;
                releaseCapture();
                return;
            }

            // Deferred renderers never report backbuffer-region copy support and keep acquiring the capture.
            if (renderer?.SupportsBackbufferRegionCopy == true)
            {
                releaseCapture();
                acrylicBackdrop.EffectEnabled = true;
                return;
            }

            if (!captureAcquired && registrar != null)
            {
                registrar.AcquireCapture();
                captureAcquired = true;
            }

            acrylicBackdrop.EffectEnabled = captureAcquired || registrar == null;
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
            acrylicBackdrop.EffectEnabled = false;
            releaseCapture();
        }
    }
}
