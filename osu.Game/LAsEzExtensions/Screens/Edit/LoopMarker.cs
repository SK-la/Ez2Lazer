// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Overlays;
using osuTK;
using osuTK.Input;

namespace osu.Game.LAsEzExtensions.Screens.Edit
{
    public partial class LoopMarker : CompositeDrawable
    {
        public new double Time { get; set; }

        public event Action<double>? TimeChanged;

        public Func<float, double>? TimeAtX { get; set; }

        private readonly bool isStart;

        private readonly VerticalTriangles triangles;

        public LoopMarker(bool isStart)
        {
            this.isStart = isStart;

            RelativeSizeAxes = Axes.Y;
            Width = 8;
            Masking = true;
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Y,
                    Width = 1.4f,
                    EdgeSmoothness = new Vector2(1, 0)
                },
                triangles = new VerticalTriangles
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    EdgeSmoothness = Vector2.One
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            Colour = isStart ? colours.Colour3 : colours.Colour4; // Green for A, Red for B
        }

        public bool IsDragged { get; private set; }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            var localPos = ToLocalSpace(e.ScreenSpaceMousePosition);
            if (localPos.Y >= DrawHeight / 2) return false; // only upper half

            if (e.Button == MouseButton.Left)
            {
                // Handle drag to set time
                return true;
            }

            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            base.OnMouseUp(e);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            var localPos = ToLocalSpace(e.ScreenSpaceMousePosition);
            if (localPos.Y >= DrawHeight / 2) return false;

            IsDragged = true;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            double newX = X + e.Delta.X;
            X = (float)newX;
            double newTime = TimeAtX?.Invoke((float)newX) ?? 0;
            Time = newTime;
            TimeChanged?.Invoke(newTime);
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            IsDragged = false;
        }

        /// <summary>
        /// Triangles drawn at the top and bottom of <see cref="LoopMarker"/>.
        /// </summary>
        /// <remarks>
        /// Since framework-side triangles don't support antialiasing we are using custom implementation involving rotated smoothened boxes to avoid
        /// mismatch in antialiasing between top and bottom triangles when drawable moves across the screen.
        /// To "trim" boxes we must enable masking at the top level.
        /// </remarks>
        private partial class VerticalTriangles : Sprite
        {
            [BackgroundDependencyLoader]
            private void load(IRenderer renderer)
            {
                Texture = renderer.WhitePixel;
            }

            protected override DrawNode CreateDrawNode() => new VerticalTrianglesDrawNode(this);

            private class VerticalTrianglesDrawNode : SpriteDrawNode
            {
                public new VerticalTriangles Source => (VerticalTriangles)base.Source;

                public VerticalTrianglesDrawNode(VerticalTriangles source)
                    : base(source)
                {
                }

                private float triangleScreenSpaceHeight;

                public override void ApplyState()
                {
                    base.ApplyState();

                    triangleScreenSpaceHeight = ScreenSpaceDrawQuad.Width * 0.8f; // TriangleHeightRatio = 0.8f
                }

                protected override void Blit(IRenderer renderer)
                {
                    if (triangleScreenSpaceHeight == 0 || DrawRectangle.Width == 0 || DrawRectangle.Height == 0)
                        return;

                    Vector2 inflation = new Vector2(InflationAmount.X / DrawRectangle.Width, InflationAmount.Y / (DrawRectangle.Width * 0.8f));

                    Quad topTriangle = new Quad
                    (
                        ScreenSpaceDrawQuad.TopLeft,
                        ScreenSpaceDrawQuad.TopLeft + new Vector2(ScreenSpaceDrawQuad.Width * 0.5f, -triangleScreenSpaceHeight),
                        ScreenSpaceDrawQuad.TopLeft + new Vector2(ScreenSpaceDrawQuad.Width * 0.5f, triangleScreenSpaceHeight),
                        ScreenSpaceDrawQuad.TopRight
                    );

                    Quad bottomTriangle = new Quad
                    (
                        ScreenSpaceDrawQuad.BottomLeft,
                        ScreenSpaceDrawQuad.BottomLeft + new Vector2(ScreenSpaceDrawQuad.Width * 0.5f, -triangleScreenSpaceHeight),
                        ScreenSpaceDrawQuad.BottomLeft + new Vector2(ScreenSpaceDrawQuad.Width * 0.5f, triangleScreenSpaceHeight),
                        ScreenSpaceDrawQuad.BottomRight
                    );

                    renderer.DrawQuad(Texture, topTriangle, DrawColourInfo.Colour, inflationPercentage: inflation);
                    renderer.DrawQuad(Texture, bottomTriangle, DrawColourInfo.Colour, inflationPercentage: inflation);
                }

                protected override bool CanDrawOpaqueInterior => false;
            }
        }
    }
}
