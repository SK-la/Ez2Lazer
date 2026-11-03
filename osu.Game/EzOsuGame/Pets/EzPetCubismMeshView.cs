// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using Live2DCSharpSDK.Framework.Rendering;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Draws Cubism art-meshes as textured triangle lists (no clipping masks yet).
    /// </summary>
    public partial class EzPetCubismMeshView : Drawable, ITexturedShaderDrawable
    {
        private EzPetCubismSession? session;
        private Storage? petsStorage;
        private Texture[] textures = [];
        private EzPetCubismFrameSnapshot? latestFrame;
        private IRenderer? renderer;
        private bool captureSuspended;

        public IShader TextureShader { get; private set; } = null!;

        public void SuspendCapture()
        {
            captureSuspended = true;
            latestFrame = null;
            Invalidate(Invalidation.DrawNode);
        }

        public void ResumeCapture() => captureSuspended = false;

        public void Bind(EzPetCubismSession cubismSession, Storage pets)
        {
            session = cubismSession;
            petsStorage = pets;

            if (renderer != null)
                reloadTextures();
        }

        public void Clear()
        {
            session = null;
            disposeTextures();
            latestFrame = null;
            Invalidate(Invalidation.DrawNode);
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders, IRenderer renderer)
        {
            this.renderer = renderer;
            TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);

            if (session != null && petsStorage != null)
                reloadTextures();
        }

        protected override void Update()
        {
            base.Update();

            if (captureSuspended || session?.IsReady != true)
            {
                latestFrame = null;
                return;
            }

            latestFrame = session.CaptureFrame();
            Invalidate(Invalidation.DrawNode);
        }

        protected override DrawNode CreateDrawNode() => new CubismMeshDrawNode(this);

        protected override void Dispose(bool isDisposing)
        {
            disposeTextures();
            base.Dispose(isDisposing);
        }

        private void reloadTextures()
        {
            disposeTextures();

            if (renderer == null || session == null || petsStorage == null)
                return;

            var loaded = new List<Texture>();

            foreach (string relative in session.TextureRelativePaths)
            {
                try
                {
                    string path = relative.Replace('/', Path.DirectorySeparatorChar);

                    if (!petsStorage.Exists(path))
                    {
                        Logger.Log($"Ez pet Cubism: missing texture '{relative}'", LoggingTarget.Runtime, LogLevel.Error);
                        loaded.Add(renderer.WhitePixel);
                        continue;
                    }

                    using var stream = petsStorage.GetStream(path);

                    if (stream == null)
                    {
                        loaded.Add(renderer.WhitePixel);
                        continue;
                    }

                    // CreateTexture must match upload size — (1,1) would only keep a single pixel (faint solid colour).
                    var upload = new TextureUpload(stream);
                    var texture = renderer.CreateTexture(upload.Width, upload.Height);
                    texture.SetData(upload);
                    loaded.Add(texture);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Ez pet Cubism: failed loading texture '{relative}'");
                    loaded.Add(renderer.WhitePixel);
                }
            }

            textures = loaded.ToArray();
        }

        private void disposeTextures()
        {
            foreach (var t in textures)
            {
                if (!ReferenceEquals(t, renderer?.WhitePixel))
                    t.Dispose();
            }

            textures = [];
        }

        private class CubismMeshDrawNode : TexturedShaderDrawNode
        {
            protected new EzPetCubismMeshView Source => (EzPetCubismMeshView)base.Source;

            private EzPetCubismFrameSnapshot? frame;
            private Texture[] textures = [];
            private Vector2 drawSize;
            private IVertexBatch<TexturedVertex2D>? batch;

            public CubismMeshDrawNode(EzPetCubismMeshView source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();
                frame = Source.latestFrame;
                textures = Source.textures;
                drawSize = Source.DrawSize;
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                // Custom verts ignored inherited Alpha before — pet Hide() left Live2D fully visible.
                if (DrawColourInfo.Colour.MaxAlpha < 0.001f)
                    return;

                if (frame == null || frame.Parts.Length == 0 || textures.Length == 0)
                    return;

                BindTextureShader(renderer);

                float canvasW = frame.CanvasWidth;
                float canvasH = frame.CanvasHeight;
                float scale = Math.Min(drawSize.X / canvasW, drawSize.Y / canvasH);
                float offsetX = (drawSize.X - canvasW * scale) * 0.5f;
                float offsetY = (drawSize.Y - canvasH * scale) * 0.5f;

                batch ??= renderer.CreateLinearBatch<TexturedVertex2D>(IRenderer.MAX_VERTICES, 2, PrimitiveTopology.Triangles);

                Color4 drawCol = DrawColourInfo.Colour.TopLeft.SRGB;

                foreach (var part in frame.Parts)
                {
                    if (part.TextureIndex < 0 || part.TextureIndex >= textures.Length)
                        continue;

                    var texture = textures[part.TextureIndex];
                    if (texture.Available != true)
                        continue;

                    renderer.SetBlend(toBlending(part.BlendMode));

                    if (!renderer.BindTexture(texture))
                        continue;

                    var texRect = texture.GetTextureRect();
                    float a = Math.Clamp(part.Opacity, 0f, 1f) * drawCol.A;
                    var colour = new Color4(drawCol.R, drawCol.G, drawCol.B, a);

                    for (int i = 0; i + 2 < part.Indices.Length; i += 3)
                    {
                        addVertex(renderer, part, part.Indices[i], texRect, colour, canvasW, canvasH, scale, offsetX, offsetY);
                        addVertex(renderer, part, part.Indices[i + 1], texRect, colour, canvasW, canvasH, scale, offsetX, offsetY);
                        addVertex(renderer, part, part.Indices[i + 2], texRect, colour, canvasW, canvasH, scale, offsetX, offsetY);
                    }
                }

                UnbindTextureShader(renderer);
            }

            private void addVertex(
                IRenderer renderer,
                EzPetCubismMeshPart part,
                ushort index,
                RectangleF texRect,
                Color4 colour,
                float canvasW,
                float canvasH,
                float scale,
                float offsetX,
                float offsetY)
            {
                if (index >= part.Positions.Length)
                    return;

                var p = part.Positions[index];
                var uv = part.UVs[index];

                // Cubism Core: origin centre, Y-up, units from GetCanvasWidth/Height.
                float localX = offsetX + (p.X + canvasW * 0.5f) * scale;
                float localY = offsetY + (-p.Y + canvasH * 0.5f) * scale;

                batch!.Add(new TexturedVertex2D(renderer)
                {
                    Position = Vector2Extensions.Transform(new Vector2(localX, localY), DrawInfo.Matrix),
                    Colour = colour,
                    TexturePosition = new Vector2(
                        texRect.Left + texRect.Width * uv.X,
                        texRect.Top + texRect.Height * (1f - uv.Y)),
                    TextureRect = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom),
                    BlendRange = Vector2.Zero,
                });
            }

            private static BlendingParameters toBlending(CubismBlendMode mode) => mode switch
            {
                CubismBlendMode.Additive => BlendingParameters.Additive,
                _ => BlendingParameters.Mixture,
            };

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                batch?.Dispose();
            }
        }
    }
}
