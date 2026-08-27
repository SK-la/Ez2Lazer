// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osuTK;

namespace osu.Game.EzOsuGame.Acrylic
{
    /// <summary>
    /// Shared acquire/release logic for <see cref="AcrylicBackdropDrawable"/> consumers.
    /// Always uses <see cref="IAcrylicCaptureRegistrar"/> so footer / overlays share the same
    /// BufferedContainer path as song-select wedges (backbuffer region copy is unreliable for late-drawn chrome).
    /// </summary>
    internal sealed class EzAcrylicCaptureController
    {
        private readonly IAcrylicCaptureRegistrar? registrar;
        private readonly AcrylicBackdropDrawable acrylicBackdrop;
        private bool captureAcquired;

        public EzAcrylicCaptureController(IAcrylicCaptureRegistrar? registrar, AcrylicBackdropDrawable acrylicBackdrop)
        {
            this.registrar = registrar;
            this.acrylicBackdrop = acrylicBackdrop;
        }

        public void Sync(bool wantsCapture, float blurStrength)
        {
            acrylicBackdrop.BlurSigma = new Vector2(blurStrength);

            if (wantsCapture)
            {
                if (!captureAcquired && registrar != null)
                {
                    registrar.AcquireCapture();
                    captureAcquired = true;
                }

                acrylicBackdrop.EffectEnabled = captureAcquired || registrar == null;
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
