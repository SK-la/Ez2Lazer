// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Primitives;
using osuTK;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Computes where the Ez skin editor plugs its live scene inside the window. The scene must keep
    /// the window's aspect ratio so the preview looks like the real game rather than a stretched one.
    /// </summary>
    internal static class EzSkinEditorSceneSizing
    {
        /// <summary>Gap kept between the scene and the editor chrome.</summary>
        public const float PADDING = 10;

        /// <summary>
        /// Returns the scene rectangle in relative window coordinates (0..1 on both axes). The scene
        /// is the largest square sharing the window's aspect ratio that fits inside the area left
        /// below <paramref name="toolbarHeight"/> and left of <paramref name="railWidth"/>. Relative
        /// coordinates scale equally on each axis, so giving both axes the same size fraction yields
        /// an on-screen rect with the window's ratio.
        /// The scene is anchored to the left edge but centred vertically in the available area. Left
        /// anchoring keeps it still when toggling the sidebar's pin (which changes the reserved width);
        /// vertical centring balances it against the toolbar.
        /// </summary>
        public static RectangleF ComputeSceneRect(Vector2 windowSize, float toolbarHeight, float railWidth)
        {
            if (windowSize.X <= 0 || windowSize.Y <= 0)
                return new RectangleF(0, 0, 1, 1);

            float relativeTop = (toolbarHeight + PADDING) / windowSize.Y;
            float relativeRail = railWidth / windowSize.X;

            float availableWidth = 1f - relativeRail;
            float availableHeight = 1f - relativeTop - PADDING / windowSize.Y;

            float size = Math.Max(0, Math.Min(availableWidth, availableHeight));
            float offsetY = relativeTop + (availableHeight - size) / 2f;

            return new RectangleF(0, offsetY, size, size);
        }
    }
}
