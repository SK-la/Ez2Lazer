// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.UserInterface
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
            var pathField = typeof(LineGraph).GetField("path", BindingFlags.NonPublic | BindingFlags.Instance);

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
                    var maskingContainerField = typeof(LineGraph).GetField("maskingContainer", BindingFlags.NonPublic | BindingFlags.Instance);

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

    public partial class TriangleBorderPath : SmoothPath
    {
        public float BorderThickness { get; set; }

        public float BorderTexelSize { get; set; }

        public TriangleBorderPath(float thickness, float texelSize)
        {
            BorderThickness = thickness;
            BorderTexelSize = texelSize;
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            var shaderField = typeof(Path).GetField("TextureShader", BindingFlags.NonPublic | BindingFlags.Instance);

            if (shaderField != null)
            {
                var triangleBorderShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "TriangleBorder");
                shaderField.SetValue(this, triangleBorderShader);
            }
        }

        protected override DrawNode CreateDrawNode()
        {
            var pathDrawNodeType = typeof(Path).GetNestedType("PathDrawNode", BindingFlags.NonPublic)!;
            var child = (DrawNode)Activator.CreateInstance(pathDrawNodeType, this)!;
            var sharedData = (BufferedDrawNodeSharedData)typeof(Path).GetField("sharedData", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(this)!;

            return new TriangleBorderBufferedDrawNode(this, child, sharedData);
        }

        private class TriangleBorderBufferedDrawNode : BufferedDrawNode
        {
            protected new TriangleBorderPath Source => (TriangleBorderPath)base.Source;

            private long pathInvalidationID = -1;
            private Texture texture = null!;
            private Vector4 textureRect;
            private IUniformBuffer<PathTextureParameters>? parametersBuffer;
            private IUniformBuffer<TriangleBorderData>? borderDataBuffer;

            public TriangleBorderBufferedDrawNode(TriangleBorderPath source, DrawNode child, BufferedDrawNodeSharedData sharedData)
                : base(source, child, sharedData)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();
                pathInvalidationID = Source.PathInvalidationID;
                texture = Source.Texture;

                var rect = texture.GetTextureRect();
                textureRect = new Vector4(rect.Left, rect.Top, rect.Width, rect.Height);
            }

            protected override void BindUniformResources(IShader shader, IRenderer renderer)
            {
                base.BindUniformResources(shader, renderer);

                parametersBuffer ??= renderer.CreateUniformBuffer<PathTextureParameters>();
                parametersBuffer.Data = new PathTextureParameters
                {
                    TexRect1 = textureRect,
                };
                shader.BindUniformBlock("m_PathTextureParameters", parametersBuffer);

                texture.Bind(1);

                borderDataBuffer ??= renderer.CreateUniformBuffer<TriangleBorderData>();
                borderDataBuffer.Data = borderDataBuffer.Data with
                {
                    Thickness = Source.BorderThickness,
                    TexelSize = Source.BorderTexelSize,
                };
                shader.BindUniformBlock("m_BorderData", borderDataBuffer);
            }

            protected override long GetDrawVersion() => pathInvalidationID;

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                borderDataBuffer?.Dispose();
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private record struct PathTextureParameters
            {
                public UniformVector4 TexRect1;
            }
        }
    }
}
