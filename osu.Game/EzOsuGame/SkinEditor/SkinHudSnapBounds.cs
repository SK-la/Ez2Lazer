// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osuTK;

namespace osu.Game.EzOsuGame.SkinEditor
{
    internal readonly struct SkinHudSnapBounds
    {
        public readonly float Left;
        public readonly float Right;
        public readonly float Top;
        public readonly float Bottom;

        public SkinHudSnapBounds(float left, float right, float top, float bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        public float HCenter => (Left + Right) * 0.5f;
        public float VCenter => (Top + Bottom) * 0.5f;

        public SkinHudSnapBounds Offset(float dx, float dy) => new SkinHudSnapBounds(Left + dx, Right + dx, Top + dy, Bottom + dy);

        public static SkinHudSnapBounds FromDrawable(Drawable drawable, bool usePixelBounds)
        {
            if (usePixelBounds)
                return fromPixelBounds(drawable);

            return fromEditorContainerBounds(drawable);
        }

        private static SkinHudSnapBounds fromPixelBounds(Drawable drawable)
        {
            var aabb = drawable.Parent!.ToLocalSpace(drawable.ScreenSpaceDrawQuad).AABBFloat;
            return new SkinHudSnapBounds(aabb.Left, aabb.Right, aabb.Top, aabb.Bottom);
        }

        /// <summary>
        /// Bounds of the skin editor interaction container (rotate/scale box), i.e. <see cref="Drawable.DrawRectangle"/>.
        /// Matches <c>SkinBlueprint</c> without its hit-test inflate padding.
        /// </summary>
        private static SkinHudSnapBounds fromEditorContainerBounds(Drawable drawable) =>
            toParentAxisAlignedBounds(drawable, drawable.DrawRectangle);

        private static SkinHudSnapBounds toParentAxisAlignedBounds(Drawable drawable, RectangleF localRect)
        {
            var parent = drawable.Parent!;
            Vector2 topLeft = parent.ToLocalSpace(drawable.ToScreenSpace(localRect.TopLeft));
            Vector2 topRight = parent.ToLocalSpace(drawable.ToScreenSpace(localRect.TopRight));
            Vector2 bottomLeft = parent.ToLocalSpace(drawable.ToScreenSpace(localRect.BottomLeft));
            Vector2 bottomRight = parent.ToLocalSpace(drawable.ToScreenSpace(localRect.BottomRight));

            float minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
            float maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
            float minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
            float maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

            return new SkinHudSnapBounds(minX, maxX, minY, maxY);
        }
    }
}
