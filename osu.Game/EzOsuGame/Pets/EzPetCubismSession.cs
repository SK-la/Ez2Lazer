// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Model;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osuTK;
using LogLevel = Live2DCSharpSDK.Framework.LogLevel;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Cubism Core session: load moc3 + texture paths, drive breath, snapshot meshes for DrawNode.
    /// Clipping masks are skipped in the MVP renderer.
    /// </summary>
    public sealed class EzPetCubismSession : IDisposable
    {
        private CubismMoc? moc;
        private CubismBreath? breath;
        private float userTime;
        private static bool frameworkStarted;
        private static bool loggedCanvasOnce;

        public bool IsReady { get; private set; }

        public string? Status { get; private set; }

        public int DrawableCount { get; private set; }

        public float BreathValue { get; private set; }

        public string? LastState { get; private set; }

        public string? LastClip { get; private set; }

        /// <summary>
        /// Paths relative to pets storage for each model3 texture slot.
        /// </summary>
        public IReadOnlyList<string> TextureRelativePaths { get; private set; } = Array.Empty<string>();

        public static bool TryCreate(Storage petsStorage, string? modelEntryRelativePath, out EzPetCubismSession? session, out string? error)
        {
            session = null;
            error = null;

            if (string.IsNullOrWhiteSpace(modelEntryRelativePath))
            {
                error = "no model entry path";
                return false;
            }

            if (!EzPetCubismNative.TryPrepare(petsStorage, out error))
                return false;

            string? mocRelative = resolveMocRelative(petsStorage, modelEntryRelativePath);

            if (mocRelative == null || !petsStorage.Exists(mocRelative))
            {
                error = $"could not locate .moc3 next to {modelEntryRelativePath}";
                return false;
            }

            try
            {
                ensureFramework();

                using var stream = petsStorage.GetStream(mocRelative);

                if (stream == null)
                {
                    error = $"failed to open {mocRelative}";
                    return false;
                }

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                byte[] bytes = ms.ToArray();

                var created = new EzPetCubismSession();
                created.moc = new CubismMoc(bytes, shouldCheckMocConsistency: true);
                created.DrawableCount = created.moc.Model.GetDrawableCount();
                created.TextureRelativePaths = resolveTexturePaths(petsStorage, modelEntryRelativePath);
                created.breath = new CubismBreath
                {
                    Parameters =
                    [
                        new BreathParameterData
                        {
                            ParameterId = CubismDefaultParameterId.ParamBreath,
                            Offset = 0.5f,
                            Peak = 0.5f,
                            Cycle = 3.2345f,
                            Weight = 0.5f,
                        },
                    ],
                };
                created.IsReady = true;
                created.Status =
                    $"Core OK · {Path.GetFileName(mocRelative)} · drawables={created.DrawableCount} · tex={created.TextureRelativePaths.Count}";
                session = created;
                Logger.Log($"Ez pet Cubism: {created.Status}", LoggingTarget.Runtime);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Logger.Error(ex, "Ez pet Cubism session failed");
                return false;
            }
        }

        public void NotifyState(string state, string clip)
        {
            LastState = state;
            LastClip = clip;
        }

        public void Update(double elapsedSeconds)
        {
            if (!IsReady || moc == null)
                return;

            userTime += (float)Math.Max(0, elapsedSeconds);
            breath?.UpdateParameters(moc.Model, (float)elapsedSeconds);
            moc.Model.Update();

            try
            {
                int index = moc.Model.GetParameterIndex(CubismDefaultParameterId.ParamBreath);
                if (index >= 0)
                    BreathValue = moc.Model.GetParameterValue(index);
                else
                    BreathValue = 0.5f + 0.5f * MathF.Sin(userTime * 2f);
            }
            catch
            {
                BreathValue = 0.5f + 0.5f * MathF.Sin(userTime * 2f);
            }
        }

        public unsafe EzPetCubismFrameSnapshot? CaptureFrame()
        {
            if (!IsReady || moc == null)
                return null;

            try
            {
                var model = moc.Model;
                int count = model.GetDrawableCount();
                if (count <= 0)
                    return null;

                float canvasW = Math.Max(0.001f, model.GetCanvasWidth());
                float canvasH = Math.Max(0.001f, model.GetCanvasHeight());
                float canvasWPx = Math.Max(1f, model.GetCanvasWidthPixel());
                float canvasHPx = Math.Max(1f, model.GetCanvasHeightPixel());

                var order = new int[count];
                for (int i = 0; i < count; i++)
                    order[i] = i;

                // Cubism 5.3+ renamed csmGetDrawableRenderOrders → csmGetRenderOrders.
                // Fall back to draw-orders / index order so older/newer Core both work.
                int* sortKeys = EzPetCubismCoreCompat.TryGetDrawableSortOrders(model.Model);
                if (sortKeys != null)
                    Array.Sort(order, (a, b) => sortKeys[a].CompareTo(sortKeys[b]));

                var parts = new List<EzPetCubismMeshPart>(count);
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                int visibleVerts = 0;

                for (int o = 0; o < count; o++)
                {
                    int di = order[o];

                    if (!model.GetDrawableDynamicFlagIsVisible(di))
                        continue;

                    float opacity = model.GetDrawableOpacity(di);
                    if (opacity <= 0.001f)
                        continue;

                    int vertexCount = model.GetDrawableVertexCount(di);
                    int indexCount = model.GetDrawableVertexIndexCount(di);
                    if (vertexCount <= 0 || indexCount < 3)
                        continue;

                    var positionsPtr = model.GetDrawableVertexPositions(di);
                    var uvsPtr = model.GetDrawableVertexUvs(di);
                    var indicesPtr = model.GetDrawableVertexIndices(di);

                    var positions = new Vector2[vertexCount];
                    var uvs = new Vector2[vertexCount];

                    for (int v = 0; v < vertexCount; v++)
                    {
                        var p = positionsPtr[v];
                        var uv = uvsPtr[v];
                        positions[v] = new Vector2(p.X, p.Y);
                        uvs[v] = new Vector2(uv.X, uv.Y);

                        minX = Math.Min(minX, p.X);
                        maxX = Math.Max(maxX, p.X);
                        minY = Math.Min(minY, p.Y);
                        maxY = Math.Max(maxY, p.Y);
                        visibleVerts++;
                    }

                    var indices = new ushort[indexCount];
                    for (int i = 0; i < indexCount; i++)
                        indices[i] = indicesPtr[i];

                    parts.Add(new EzPetCubismMeshPart
                    {
                        TextureIndex = model.GetDrawableTextureIndex(di),
                        Opacity = opacity,
                        BlendMode = model.GetDrawableBlendMode(di),
                        Positions = positions,
                        UVs = uvs,
                        Indices = indices,
                    });
                }

                if (!loggedCanvasOnce)
                {
                    loggedCanvasOnce = true;
                    Logger.Log(
                        $"Ez pet Cubism canvas: units={canvasW:0.###}x{canvasH:0.###} px={canvasWPx:0}x{canvasHPx:0} ppu={model.GetPixelsPerUnit():0.###} verts≈({minX:0.##},{minY:0.##})-({maxX:0.##},{maxY:0.##}) n={visibleVerts} parts={parts.Count}",
                        LoggingTarget.Runtime);
                }

                return new EzPetCubismFrameSnapshot
                {
                    CanvasWidth = canvasW,
                    CanvasHeight = canvasH,
                    Parts = parts.ToArray(),
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Ez pet Cubism: CaptureFrame failed");
                return null;
            }
        }

        public void Dispose()
        {
            breath = null;
            moc?.Dispose();
            moc = null;
            IsReady = false;
        }

        private static void ensureFramework()
        {
            if (frameworkStarted)
                return;

            bool ok = CubismFramework.StartUp(new EzCubismAllocator(), new Option
            {
                LogFunction = msg => Logger.Log($"[Cubism] {msg}", LoggingTarget.Runtime),
                LoggingLevel = LogLevel.Warning,
            });

            if (!ok)
                throw new InvalidOperationException("CubismFramework.StartUp failed");

            frameworkStarted = true;
        }

        private static IReadOnlyList<string> resolveTexturePaths(Storage petsStorage, string modelEntryRelativePath)
        {
            string normalised = modelEntryRelativePath.Replace('\\', '/');
            string dir = Path.GetDirectoryName(normalised.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;
            var result = new List<string>();

            if (!normalised.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase))
                return result;

            try
            {
                using var stream = petsStorage.GetStream(normalised.Replace('/', Path.DirectorySeparatorChar));
                if (stream == null)
                    return result;

                using var reader = new StreamReader(stream);
                var root = JObject.Parse(reader.ReadToEnd());
                var textures = root["FileReferences"]?["Textures"] as JArray;
                if (textures == null)
                    return result;

                foreach (var t in textures)
                {
                    string? rel = t.ToString();
                    if (string.IsNullOrWhiteSpace(rel))
                        continue;

                    string combined = Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar));
                    result.Add(combined.Replace('\\', '/'));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Ez pet Cubism: failed reading texture list from model3.json");
            }

            return result;
        }

        private static string? resolveMocRelative(Storage petsStorage, string modelEntryRelativePath)
        {
            string normalised = modelEntryRelativePath.Replace('\\', '/');

            if (normalised.EndsWith(".moc3", StringComparison.OrdinalIgnoreCase))
                return normalised.Replace('/', Path.DirectorySeparatorChar);

            if (normalised.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase))
            {
                string dir = Path.GetDirectoryName(normalised.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;

                try
                {
                    foreach (string file in petsStorage.GetFiles(dir, "*.moc3"))
                        return file.Replace('\\', '/');
                }
                catch
                {
                    // ignore
                }

                string sibling = Path.ChangeExtension(normalised.Replace('/', Path.DirectorySeparatorChar), ".moc3");
                if (petsStorage.Exists(sibling))
                    return sibling.Replace('\\', '/');
            }

            return null;
        }

        private sealed class EzCubismAllocator : ICubismAllocator
        {
            public IntPtr Allocate(int size) => Marshal.AllocHGlobal(size);

            public void Deallocate(IntPtr memory) => Marshal.FreeHGlobal(memory);

            public unsafe IntPtr AllocateAligned(int size, int alignment)
            {
                IntPtr offset = alignment - 1 + sizeof(void*);
                IntPtr allocation = Allocate((int)(size + offset));
                IntPtr alignedAddress = allocation + sizeof(void*);
                IntPtr shift = alignedAddress % alignment;

                if (shift != 0)
                    alignedAddress += alignment - shift;

                var preamble = (void**)alignedAddress;
                preamble[-1] = (void*)allocation;
                return alignedAddress;
            }

            public unsafe void DeallocateAligned(IntPtr alignedMemory)
            {
                var preamble = (void**)alignedMemory;
                Deallocate(new IntPtr(preamble[-1]));
            }
        }
    }
}
