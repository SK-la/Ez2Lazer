using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
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

        private Texture texture = null!;
        private IShader shader = null!;

        public GirdPoints(float size = 2f)
        {
            this.size = size;
            RelativeSizeAxes = Axes.None;
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer, ShaderManager shaders)
        {
            texture = renderer.WhitePixel;
            shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
        }

        public void SetPoints(IEnumerable<(Vector2 pos, Color4 colour)> newPoints)
        {
            points.Clear();
            points.AddRange(newPoints);

            Invalidate(Invalidation.DrawNode);
            Invalidate(Invalidation.DrawInfo);

            Schedule(() => Invalidate(Invalidation.DrawNode));
        }

        protected override DrawNode CreateDrawNode() => new ScorePointsDrawNode(this);

        private class ScorePointsDrawNode : DrawNode
        {
            protected new GirdPoints Source => (GirdPoints)base.Source;

            private Texture texture = null!;
            private IShader shader = null!;
            private float size;

            private (Vector2 pos, Color4 colour)[] localPoints = Array.Empty<(Vector2, Color4)>();
            private int localCount;

            private IVertexBatch<TexturedVertex2D>? quadBatch;

            public ScorePointsDrawNode(GirdPoints source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                texture = Source.texture;
                shader = Source.shader;
                size = Source.size;

                localCount = Source.points.Count;

                if (localPoints.Length < localCount)
                    localPoints = new (Vector2 pos, Color4 colour)[localCount];

                Source.points.CopyTo(localPoints, 0);
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                if (localCount == 0)
                    return;

                shader.Bind();

                if (!renderer.BindTexture(texture))
                    return;

                const int max_quads = 10922; // renderer limit for quad batches
                int total = localCount;

                renderer.PushLocalMatrix(DrawInfo.Matrix);

                quadBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(Math.Min(total, max_quads), 4);
                RectangleF textureRect = texture.GetTextureRect();
                Vector4 textureRectangle = new Vector4(0, 0, 1, 1);
                Vector2 blendRange = Vector2.One;

                for (int offset = 0; offset < total; offset += max_quads)
                {
                    int chunk = Math.Min(max_quads, total - offset);
                    if (quadBatch.Size < chunk && quadBatch.Size != IRenderer.MAX_QUADS)
                        quadBatch = renderer.CreateQuadBatch<TexturedVertex2D>(Math.Min(quadBatch.Size * 2, max_quads), 4);

                    var add = quadBatch.AddAction;

                    for (int i = 0; i < chunk; i++)
                    {
                        var p = localPoints[offset + i];
                        float half = size / 2f;

                        Vector2 tl = new Vector2(p.pos.X - half, p.pos.Y - half);
                        Vector2 tr = new Vector2(p.pos.X + half, p.pos.Y - half);
                        Vector2 br = new Vector2(p.pos.X + half, p.pos.Y + half);
                        Vector2 bl = new Vector2(p.pos.X - half, p.pos.Y + half);

                        add(new TexturedVertex2D(renderer)
                        {
                            Position = tl,
                            TexturePosition = textureRect.TopLeft,
                            TextureRect = textureRectangle,
                            BlendRange = blendRange,
                            Colour = p.colour
                        });
                        add(new TexturedVertex2D(renderer)
                        {
                            Position = tr,
                            TexturePosition = textureRect.TopRight,
                            TextureRect = textureRectangle,
                            BlendRange = blendRange,
                            Colour = p.colour
                        });
                        add(new TexturedVertex2D(renderer)
                        {
                            Position = br,
                            TexturePosition = textureRect.BottomRight,
                            TextureRect = textureRectangle,
                            BlendRange = blendRange,
                            Colour = p.colour
                        });
                        add(new TexturedVertex2D(renderer)
                        {
                            Position = bl,
                            TexturePosition = textureRect.BottomLeft,
                            TextureRect = textureRectangle,
                            BlendRange = blendRange,
                            Colour = p.colour
                        });
                    }

                    quadBatch.Draw();
                }

                renderer.PopLocalMatrix();

                shader.Unbind();
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                quadBatch?.Dispose();
                quadBatch = null;
            }
        }
    }
}
