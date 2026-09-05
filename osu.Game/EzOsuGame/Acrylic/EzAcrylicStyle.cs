// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Acrylic
{
    /// <summary>
    /// Acrylic glass (N) appearance constants only — tune for hot reload.
    /// Classic chrome (M) is never half-alpha; use <see cref="UI.EzAcrylicOverlayAlpha.BindExclusive"/>.
    /// </summary>
    public static class EzAcrylicStyle
    {
        /// <summary>Cool dark veil over blurred backdrop (wedges / preview).</summary>
        public static readonly Color4 Veil = new Color4(8 / 255f, 10 / 255f, 14 / 255f, 0.40f);

        /// <summary>Full-page mod / sheared overlay veil.</summary>
        public static readonly Color4 ModPageVeil = new Color4(6 / 255f, 8 / 255f, 12 / 255f, 0.50f);

        /// <summary>Carousel panel veil — dark enough that bright song-select BGs do not wash cards.</summary>
        public static readonly Color4 PanelVeil = new Color4(5 / 255f, 7 / 255f, 11 / 255f, 0.72f);

        /// <summary>Footer glass veil — cool dark so blur stays readable (not the same hue as solid Background5).</summary>
        public static readonly Color4 FooterVeil = new Color4(8 / 255f, 10 / 255f, 14 / 255f, 0.50f);

        /// <summary>
        /// Light scheme wash on the frosted footer TintBox. Kept low so blur is not masked
        /// into looking like solid Background5.
        /// </summary>
        public const float FooterSchemeTintAlpha = 0.22f;

        /// <summary>Downscale panel acrylic blur to limit cost with many visible cards.</summary>
        public static readonly Vector2 PanelFrameBufferScale = new Vector2(0.5f);
    }
}
