// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public partial class ManiaPreviewBatchDrawable : Drawable
    {
        private readonly List<PreviewQuad> quads = new List<PreviewQuad>();

        private Texture texture = null!;
        private IShader shader = null!;

        public ManiaPreviewBatchDrawable()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer, ShaderManager shaders)
        {
            texture = renderer.WhitePixel;
            shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
        }

        public void SetQuads(IEnumerable<PreviewQuad> geometry)
        {
            quads.Clear();
            quads.AddRange(geometry);
            Invalidate(Invalidation.DrawNode);
        }

        protected override DrawNode CreateDrawNode() => new ManiaPreviewBatchDrawNode(this);

        private class ManiaPreviewBatchDrawNode : DrawNode
        {
            protected new ManiaPreviewBatchDrawable Source => (ManiaPreviewBatchDrawable)base.Source;

            private readonly List<PreviewQuad> localQuads = new List<PreviewQuad>();
            private IVertexBatch<TexturedVertex2D>? quadBatch;
            private Texture texture = null!;
            private IShader shader = null!;

            public ManiaPreviewBatchDrawNode(ManiaPreviewBatchDrawable source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();
                texture = Source.texture;
                shader = Source.shader;

                localQuads.Clear();
                localQuads.AddRange(Source.quads);
            }

            protected override void Draw(IRenderer renderer)
            {
                if (localQuads.Count == 0)
                    return;

                shader.Bind();
                if (!renderer.BindTexture(texture))
                    return;

                const int max_quads_per_batch = 10922;

                renderer.PushLocalMatrix(DrawInfo.Matrix);
                quadBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(Math.Min(localQuads.Count, max_quads_per_batch), 4);

                RectangleF textureRect = texture.GetTextureRect();
                Vector4 textureRectangle = new Vector4(0, 0, 1, 1);
                Vector2 blendRange = Vector2.One;

                for (int offset = 0; offset < localQuads.Count; offset += max_quads_per_batch)
                {
                    int chunk = Math.Min(max_quads_per_batch, localQuads.Count - offset);

                    if (quadBatch.Size < chunk && quadBatch.Size != IRenderer.MAX_QUADS)
                        quadBatch = renderer.CreateQuadBatch<TexturedVertex2D>(Math.Min(quadBatch.Size * 2, max_quads_per_batch), 4);

                    var add = quadBatch.AddAction;

                    for (int i = 0; i < chunk; i++)
                    {
                        PreviewQuad quad = localQuads[offset + i];

                        Vector2 tl = new Vector2(quad.X, quad.Y);
                        Vector2 tr = new Vector2(quad.X + quad.Width, quad.Y);
                        Vector2 br = new Vector2(quad.X + quad.Width, quad.Y + quad.Height);
                        Vector2 bl = new Vector2(quad.X, quad.Y + quad.Height);

                        add(new TexturedVertex2D(renderer)
                        {
                            Position = tl,
                            TexturePosition = textureRect.TopLeft,
                            TextureRect = textureRectangle,
                            BlendRange = blendRange,
                            Colour = quad.Colour
                        });
                        add(new TexturedVertex2D(renderer)
                        {
                            Position = tr,
                            TexturePosition = textureRect.TopRight,
                            TextureRect = textureRectangle,
                            BlendRange = blendRange,
                            Colour = quad.Colour
                        });
                        add(new TexturedVertex2D(renderer)
                        {
                            Position = br,
                            TexturePosition = textureRect.BottomRight,
                            TextureRect = textureRectangle,
                            BlendRange = blendRange,
                            Colour = quad.Colour
                        });
                        add(new TexturedVertex2D(renderer)
                        {
                            Position = bl,
                            TexturePosition = textureRect.BottomLeft,
                            TextureRect = textureRectangle,
                            BlendRange = blendRange,
                            Colour = quad.Colour
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
