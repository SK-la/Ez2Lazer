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
        /// below <paramref name="toolbarHeight"/> and between the fixed left scene rail and the
        /// reserved sidebar width. Relative coordinates scale equally on each axis, so giving both
        /// axes the same size fraction yields an on-screen rect with the window's ratio.
        /// The scene is centred horizontally in the space between the two rails (so it stays put when
        /// the sidebar's reserved width changes only while the space still fits the scene), and centred
        /// vertically in the available area to balance it against the toolbar.
        /// </summary>
        public static RectangleF ComputeSceneRect(Vector2 windowSize, float toolbarHeight, float leftRailWidth, float rightRailWidth)
        {
            if (windowSize.X <= 0 || windowSize.Y <= 0)
                return new RectangleF(0, 0, 1, 1);

            float relativeTop = (toolbarHeight + PADDING) / windowSize.Y;
            float relativeLeft = leftRailWidth / windowSize.X;
            float relativeRight = rightRailWidth / windowSize.X;

            float availableWidth = 1f - relativeLeft - relativeRight;
            float availableHeight = 1f - relativeTop - PADDING / windowSize.Y;

            float size = Math.Max(0, Math.Min(availableWidth, availableHeight));

            float offsetX = relativeLeft + (availableWidth - size) / 2f;
            float offsetY = relativeTop + (availableHeight - size) / 2f;

            return new RectangleF(offsetX, offsetY, size, size);
        }
    }
}
