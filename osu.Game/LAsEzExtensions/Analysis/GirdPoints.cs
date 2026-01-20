using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Analysis
{
    /// <summary>
    /// Batch-draw a set of coloured points (as small quads) to avoid creating thousands of Drawables.
    /// </summary>
    public partial class GirdPoints : Drawable
    {
        private readonly List<(Vector2 pos, Color4 colour)> points = new List<(Vector2, Color4)>();
        private readonly float size;

        public GirdPoints(float size = 5f)
        {
            this.size = size;
            RelativeSizeAxes = Axes.None;
        }

        public void SetPoints(IEnumerable<(Vector2 pos, Color4 colour)> newPoints)
        {
            points.Clear();
            points.AddRange(newPoints);
            Invalidate(Invalidation.DrawNode);
        }

        protected override DrawNode CreateDrawNode() => new ScorePointsDrawNode(this, size, points);

        private class ScorePointsDrawNode : DrawNode
        {
            private readonly float size;
            private (Vector2 pos, Color4 colour)[] localPoints;

            public ScorePointsDrawNode(GirdPoints source, float size, List<(Vector2 pos, Color4 colour)> points)
                : base(source)
            {
                this.size = size;
                localPoints = points.ToArray();
            }

            public override void ApplyState()
            {
                base.ApplyState();
                // copy points from source for thread-safety
                var src = (GirdPoints)Source;
                localPoints = src.points.ToArray();
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                if (localPoints.Length == 0)
                    return;

                renderer.PushLocalMatrix(DrawInfo.Matrix);

                float half = size / 2f;

                const int max_quads = 10922; // renderer limit for quad batches
                int total = localPoints.Length;

                for (int offset = 0; offset < total; offset += max_quads)
                {
                    int chunk = Math.Min(max_quads, total - offset);

                    // create temporary batch for this chunk (chunk = max number of quads)
                    using (var localBatch = renderer.CreateQuadBatch<TexturedVertex2D>(chunk, 4))
                    {
                        for (int i = 0; i < chunk; i++)
                        {
                            var p = localPoints[offset + i];
                            var tl = new Vector2(p.pos.X - half, p.pos.Y - half);
                            var tr = new Vector2(p.pos.X + half, p.pos.Y - half);
                            var br = new Vector2(p.pos.X + half, p.pos.Y + half);
                            var bl = new Vector2(p.pos.X - half, p.pos.Y + half);

                            localBatch.Add(new TexturedVertex2D(renderer)
                            {
                                Position = tl,
                                TexturePosition = Vector2.Zero,
                                Colour = p.colour
                            });
                            localBatch.Add(new TexturedVertex2D(renderer)
                            {
                                Position = tr,
                                TexturePosition = new Vector2(1, 0),
                                Colour = p.colour
                            });
                            localBatch.Add(new TexturedVertex2D(renderer)
                            {
                                Position = br,
                                TexturePosition = Vector2.One,
                                Colour = p.colour
                            });
                            localBatch.Add(new TexturedVertex2D(renderer)
                            {
                                Position = bl,
                                TexturePosition = new Vector2(0, 1),
                                Colour = p.colour
                            });
                        }

                        localBatch.Draw();
                    }
                }

                renderer.PopLocalMatrix();
            }
        }
    }
}
