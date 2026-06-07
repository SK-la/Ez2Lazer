// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osuTK;

namespace osu.Game.EzOsuGame.Acrylic
{
    /// <summary>
    /// Shared acquire/release logic for <see cref="AcrylicBackdropDrawable"/> consumers.
    /// </summary>
    internal sealed class EzAcrylicCaptureController
    {
        private readonly IAcrylicCaptureRegistrar? registrar;
        private readonly IRenderer renderer;
        private readonly AcrylicBackdropDrawable acrylicBackdrop;
        private bool captureAcquired;

        public EzAcrylicCaptureController(IAcrylicCaptureRegistrar? registrar, IRenderer renderer, AcrylicBackdropDrawable acrylicBackdrop)
        {
            this.registrar = registrar;
            this.renderer = renderer;
            this.acrylicBackdrop = acrylicBackdrop;
        }

        public void Sync(bool wantsCapture, float blurStrength)
        {
            acrylicBackdrop.BlurSigma = new Vector2(blurStrength);

            bool needsScopeCapture = wantsCapture && !renderer.SupportsBackbufferRegionCopy;

            if (wantsCapture)
            {
                if (needsScopeCapture)
                {
                    if (!captureAcquired && registrar != null)
                    {
                        registrar.AcquireCapture();
                        captureAcquired = true;
                    }

                    acrylicBackdrop.EffectEnabled = captureAcquired;
                }
                else
                {
                    if (captureAcquired && registrar != null)
                    {
                        registrar.ReleaseCapture();
                        captureAcquired = false;
                    }

                    acrylicBackdrop.EffectEnabled = true;
                }
            }
            else
            {
                acrylicBackdrop.EffectEnabled = false;

                if (captureAcquired && registrar != null)
                {
                    registrar.ReleaseCapture();
                    captureAcquired = false;
                }
            }
        }

        public void Dispose()
        {
            acrylicBackdrop.EffectEnabled = false;

            if (captureAcquired && registrar != null)
            {
                registrar.ReleaseCapture();
                captureAcquired = false;
            }
        }
    }
}
