// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Rendering;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.UserInterface;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.UserInterface
{
    public partial class TriangleBorderLineGraph : LineGraph
    {
        private float thickness = 0.15f;

        /// <summary>
        /// The thickness of the triangle border effect.
        /// </summary>
        public float Thickness
        {
            get => thickness;
            set
            {
                if (thickness == value) return;

                thickness = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        private float texelSize = 0.005f;

        /// <summary>
        /// The texel size for the border effect.
        /// </summary>
        public float TexelSize
        {
            get => texelSize;
            set
            {
                if (texelSize == value) return;

                texelSize = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        /// <summary>
        /// The colour of the main line.
        /// </summary>
        public new Color4 LineColour
        {
            get => base.LineColour;
            set => base.LineColour = value;
        }

        /// <summary>
        /// The base colour of the triangle border effect, similar to TrianglesV2.
        /// This affects the overall colour of the line segments and supports gradients.
        /// </summary>
        public new ColourInfo Colour
        {
            get => base.Colour;
            set => base.Colour = value;
        }

        /// <summary>
        /// The colour of the border (for compatibility, not used in shader version).
        /// </summary>
        public new Color4 BorderColour { get; set; }

        public TriangleBorderLineGraph()
        {
            // Set up blending for the triangle effect
            Blending = BlendingParameters.Additive;
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            // Modify the path's shader to use TriangleBorder for the triangle effect
            var pathField = typeof(LineGraph).GetField("path", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (pathField != null)
            {
                if (pathField.GetValue(this) is Path path)
                {
                    // Replace the path with our custom TriangleBorderPath
                    var triangleBorderPath = new TriangleBorderPath(thickness, texelSize)
                    {
                        AutoSizeAxes = path.AutoSizeAxes,
                        RelativeSizeAxes = path.RelativeSizeAxes,
                        PathRadius = path.PathRadius
                    };

                    // Copy vertices
                    triangleBorderPath.ClearVertices();
                    foreach (var vertex in path.Vertices)
                        triangleBorderPath.AddVertex(vertex);

                    // Replace in the masking container
                    var maskingContainerField = typeof(LineGraph).GetField("maskingContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (maskingContainerField != null)
                    {
                        if (maskingContainerField.GetValue(this) is Container<Path> maskingContainer)
                        {
                            maskingContainer.Child = triangleBorderPath;
                            pathField.SetValue(this, triangleBorderPath);
                        }
                    }
                }
            }
        }
    }

    public partial class TriangleBorderPath : Path
    {
        private readonly float thickness;
        private readonly float texelSize;

        public TriangleBorderPath(float thickness, float texelSize)
        {
            this.thickness = thickness;
            this.texelSize = texelSize;
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            // Use reflection to set the TriangleBorder shader
            var shaderField = typeof(Path).GetField("TextureShader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (shaderField != null)
            {
                var triangleBorderShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "TriangleBorder");
                shaderField.SetValue(this, triangleBorderShader);
            }
        }

        protected override DrawNode CreateDrawNode() => new TriangleBorderPathDrawNode(this);

        private class TriangleBorderPathDrawNode : DrawNode
        {
            protected new TriangleBorderPath Source => (TriangleBorderPath)base.Source;

            private IUniformBuffer<TriangleBorderData>? borderDataBuffer;
            private IShader? shader;

            public TriangleBorderPathDrawNode(TriangleBorderPath source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();
                shader = Source.TextureShader;
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                // Set up TriangleBorder uniform data and bind it
                if (shader != null)
                {
                    borderDataBuffer ??= renderer.CreateUniformBuffer<TriangleBorderData>();
                    borderDataBuffer.Data = borderDataBuffer.Data with
                    {
                        Thickness = Source.thickness,
                        TexelSize = Source.texelSize
                    };

                    shader.BindUniformBlock("m_BorderData", borderDataBuffer);
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                borderDataBuffer?.Dispose();
            }
        }
    }
}
