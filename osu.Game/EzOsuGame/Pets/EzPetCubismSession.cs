// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Motion;
using Live2DCSharpSDK.Framework.Physics;
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
        private CubismPhysics? physics;
        private readonly CubismMotionManager motionManager = new CubismMotionManager();
        private readonly Dictionary<string, CubismMotion> motions = new Dictionary<string, CubismMotion>(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> eyeBlinkParameterIds =
        [
            CubismDefaultParameterId.ParamEyeLOpen,
            CubismDefaultParameterId.ParamEyeROpen,
        ];

        private readonly List<string> lipSyncParameterIds = [];
        private string? activeMotionKey;
        private bool hasMotionLibrary;
        private float userTime;
        private float nextBlinkAt = 2f;
        private float blinkPhase; // 0 idle, >0 closing/opening progress
        private float reactionRemaining;
        private float reactionAngleX;
        private float reactionMouth;
        private static bool frameworkStarted;
        private static bool loggedCanvasOnce;

        private readonly EzPetCubismExpressionStack expressionStack = new EzPetCubismExpressionStack(EzPetCubismExpressionLibrary.CreateDefaults());
        private EzPetLive2DDefinition? live2DDefinition;
        private bool lipSyncEnabled;
        private float lipSyncMinOpen = 0.15f;
        private float lipSyncAmplitude;

        public bool IsReady { get; private set; }

        public string? Status { get; private set; }

        public int DrawableCount { get; private set; }

        public float BreathValue { get; private set; }

        /// <summary>
        /// 0–1 drawable bounce from jump-like expressions (applied by the pet layer).
        /// </summary>
        public float VisualBounce => expressionStack.VisualBounce;

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
                created.physics = tryLoadPhysics(petsStorage, modelEntryRelativePath);
                created.breath = createDefaultBreath();
                created.loadMotions(petsStorage, modelEntryRelativePath);
                created.moc.Model.SaveParameters();
                created.IsReady = true;
                created.Status =
                    $"Core OK · {Path.GetFileName(mocRelative)} · drawables={created.DrawableCount} · tex={created.TextureRelativePaths.Count}"
                    + (created.physics != null ? " · physics" : string.Empty)
                    + (created.hasMotionLibrary ? $" · motions={created.motions.Count}" : string.Empty);
                session = created;
                Logger.Log($"Ez pet Cubism: {created.Status}", LoggingTarget.Runtime);
                created.startMotion("idle", MotionPriority.PriorityIdle, loop: true);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Logger.Error(ex, "Ez pet Cubism session failed");
                return false;
            }
        }

        public void ConfigurePack(EzPetLive2DDefinition? definition)
        {
            live2DDefinition = definition;
            var recipes = EzPetCubismExpressionLibrary.Merge(
                EzPetCubismExpressionLibrary.CreateDefaults(),
                definition?.Expressions);
            expressionStack.SetRecipes(recipes);

            if (definition?.LipSync != null && definition.LipSync.MinOpen > 0)
                lipSyncMinOpen = Math.Clamp(definition.LipSync.MinOpen, 0.01f, 0.95f);
        }

        public void SetLipSync(bool enabled, float amplitude01)
        {
            lipSyncEnabled = enabled;
            lipSyncAmplitude = Math.Clamp(amplitude01, 0f, 1f);
        }

        public void NotifyState(string state, string clip)
        {
            LastState = state;
            LastClip = clip;

            string key = !string.IsNullOrWhiteSpace(clip) ? clip : state;
            activateExpressionsForClip(key, state);
            tryStartMotionForClip(key, state);
        }

        private void activateExpressionsForClip(string clip, string state)
        {
            IReadOnlyList<string> ids = resolveExpressionIds(clip);

            if (ids.Count == 0 && !string.Equals(clip, state, StringComparison.OrdinalIgnoreCase))
                ids = resolveExpressionIds(state);

            expressionStack.Activate(ids);
        }

        private IReadOnlyList<string> resolveExpressionIds(string clipOrState)
        {
            if (live2DDefinition?.ClipExpressions != null
                && live2DDefinition.ClipExpressions.TryGetValue(clipOrState, out var listed)
                && listed is { Count: > 0 })
            {
                return listed;
            }

            return EzPetCubismExpressionLibrary.DefaultExpressionsForClip(clipOrState);
        }

        private void tryStartMotionForClip(string clip, string state)
        {
            string? motionKey = null;

            if (live2DDefinition?.ClipMotions != null
                && live2DDefinition.ClipMotions.TryGetValue(clip, out string? mapped)
                && !string.IsNullOrWhiteSpace(mapped))
            {
                motionKey = mapped;
            }

            motionKey ??= clip;

            bool idle = string.Equals(clip, "idle", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(state, "idle", StringComparison.OrdinalIgnoreCase);

            if (idle)
            {
                startMotion("idle", MotionPriority.PriorityIdle, loop: true);
                return;
            }

            bool loop = string.Equals(clip, "grabbed", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(state, "grabbed", StringComparison.OrdinalIgnoreCase);

            if (startMotion(motionKey, loop ? MotionPriority.PriorityForce : MotionPriority.PriorityForce, loop))
                return;

            if (!string.Equals(motionKey, clip, StringComparison.OrdinalIgnoreCase)
                && startMotion(clip, MotionPriority.PriorityForce, loop))
                return;

            // Fallback poke/grabbed reactions when no motion3 and expressions may also be empty.
            if (string.Equals(clip, "poke", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "poke", StringComparison.OrdinalIgnoreCase))
            {
                if (!startMotion("nod", MotionPriority.PriorityForce, loop: false)
                    && !startMotion("shake", MotionPriority.PriorityForce, loop: false)
                    && !startMotion("tap", MotionPriority.PriorityForce, loop: false))
                {
                    reactionRemaining = 0.55f;
                    reactionAngleX = (Random.Shared.NextSingle() * 2f - 1f) * 18f;
                    reactionMouth = 0.7f;
                }
            }
            else if (string.Equals(clip, "grabbed", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(state, "grabbed", StringComparison.OrdinalIgnoreCase))
            {
                if (!startMotion("shake", MotionPriority.PriorityForce, loop: true)
                    && !startMotion("grabbed", MotionPriority.PriorityForce, loop: true))
                {
                    reactionRemaining = 0.35f;
                    reactionAngleX = 12f;
                    reactionMouth = 0.35f;
                }
            }
        }

        public void Update(double elapsedSeconds)
        {
            if (!IsReady || moc == null)
                return;

            float dt = (float)Math.Max(0, Math.Min(elapsedSeconds, 0.1));
            userTime += dt;
            var model = moc.Model;

            model.LoadParameters();

            bool motionUpdated = false;

            if (hasMotionLibrary)
            {
                if (motionManager.IsFinished())
                    startMotion("idle", MotionPriority.PriorityIdle, loop: true);

                try
                {
                    motionUpdated = motionManager.UpdateMotion(model, dt);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Ez pet Cubism: UpdateMotion failed");
                    motionManager.StopAllMotions();
                    activeMotionKey = null;
                }
            }

            // Procedural idle sway when no motion library is driving the body.
            if (!hasMotionLibrary)
            {
                float tilt = MathF.Sin(userTime * 0.55f) * 10f;
                model.AddParameterValue(CubismDefaultParameterId.ParamAngleZ, tilt);
                model.AddParameterValue(CubismDefaultParameterId.ParamBodyAngleZ, tilt * 0.25f);
            }

            if (reactionRemaining > 0)
            {
                float t = Math.Clamp(reactionRemaining / 0.55f, 0f, 1f);
                model.AddParameterValue(CubismDefaultParameterId.ParamAngleZ, reactionAngleX * t);
                reactionRemaining = Math.Max(0, reactionRemaining - dt);
            }

            expressionStack.Update(dt);
            expressionStack.Apply(model);

            // Keep facing forward: kill yaw/pitch drift from breath / reactions.
            model.SetParameterValue(CubismDefaultParameterId.ParamAngleX, 0f);
            model.SetParameterValue(CubismDefaultParameterId.ParamBodyAngleX, 0f);

            applyMouth(model);

            model.SaveParameters();

            if (!motionUpdated)
                updateEyeBlink(model, dt);

            breath?.UpdateParameters(model, dt);

            model.SetParameterValue(CubismDefaultParameterId.ParamAngleX, 0f);
            model.SetParameterValue(CubismDefaultParameterId.ParamBodyAngleX, 0f);
            applyMouth(model);

            physics?.Evaluate(model, dt);
            model.Update();

            try
            {
                int index = model.GetParameterIndex(CubismDefaultParameterId.ParamBreath);
                BreathValue = index >= 0 ? model.GetParameterValue(index) : 0.5f + 0.5f * MathF.Sin(userTime * 2f);
            }
            catch
            {
                BreathValue = 0.5f + 0.5f * MathF.Sin(userTime * 2f);
            }
        }

        private void applyMouth(CubismModel model)
        {
            if (lipSyncEnabled)
            {
                // Half the previous swing so music follow is subtler above the floor.
                float open = Math.Max(lipSyncMinOpen, lipSyncMinOpen + (1f - lipSyncMinOpen) * lipSyncAmplitude * 0.5f);
                model.SetParameterValue(CubismDefaultParameterId.ParamMouthOpenY, open);
                return;
            }

            // Default half-open mouth when not lip-syncing.
            model.SetParameterValue(CubismDefaultParameterId.ParamMouthOpenY, 0.5f);
        }

        private bool startMotion(string key, MotionPriority priority, bool loop)
        {
            if (!motions.TryGetValue(key, out var motion))
                return false;

            // Avoid restarting the same looping idle every frame when finished-check restarts it.
            if (loop
                && string.Equals(activeMotionKey, key, StringComparison.OrdinalIgnoreCase)
                && !motionManager.IsFinished()
                && motionManager.CurrentPriority == priority)
            {
                return true;
            }

            motion.IsLoop = loop;
            motionManager.StartMotionPriority(motion, priority);

            if (!string.Equals(activeMotionKey, key, StringComparison.OrdinalIgnoreCase))
                Logger.Log($"Ez pet Cubism: start motion '{key}' (loop={loop})", LoggingTarget.Runtime);

            activeMotionKey = key;
            return true;
        }

        private void loadMotions(Storage petsStorage, string modelEntryRelativePath)
        {
            motions.Clear();
            hasMotionLibrary = false;

            string normalised = modelEntryRelativePath.Replace('\\', '/');
            string dir = Path.GetDirectoryName(normalised.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;

            try
            {
                foreach (string relative in petsStorage.GetFiles(dir, "*.motion3.json"))
                {
                    string fileName = Path.GetFileName(relative);
                    string key = extractMotionKey(fileName);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    string fullPath = petsStorage.GetFullPath(relative);
                    if (!File.Exists(fullPath))
                        continue;

                    var motion = new CubismMotion(fullPath)
                    {
                        IsLoop = string.Equals(key, "idle", StringComparison.OrdinalIgnoreCase)
                    };
                    // CubismMotion.DoUpdateParameters assumes these lists are non-null.
                    motion.SetEffectIds(eyeBlinkParameterIds, lipSyncParameterIds);
                    motions[key] = motion;
                    Logger.Log($"Ez pet Cubism: loaded motion '{key}' from {fileName}", LoggingTarget.Runtime);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Ez pet Cubism: failed scanning motion3 files");
            }

            hasMotionLibrary = motions.Count > 0;
        }

        private static string extractMotionKey(string fileName)
        {
            // miku-edit.idle.motion3.json → idle ; idle.motion3.json → idle
            const string suffix = ".motion3.json";
            if (!fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            string stem = fileName[..^suffix.Length];
            int dot = stem.LastIndexOf('.');
            return dot >= 0 && dot < stem.Length - 1 ? stem[(dot + 1)..] : stem;
        }

        private void updateEyeBlink(CubismModel model, float dt)
        {
            const float close_seconds = 0.08f;
            const float open_seconds = 0.12f;

            if (blinkPhase <= 0)
            {
                if (userTime < nextBlinkAt)
                {
                    model.SetParameterValue(CubismDefaultParameterId.ParamEyeLOpen, 1f);
                    model.SetParameterValue(CubismDefaultParameterId.ParamEyeROpen, 1f);
                    return;
                }

                blinkPhase = close_seconds + open_seconds;
                nextBlinkAt = userTime + 2.5f + Random.Shared.NextSingle() * 3.5f;
            }

            float remaining = blinkPhase;
            blinkPhase = Math.Max(0, blinkPhase - dt);

            float open;

            if (remaining > open_seconds)
            {
                // closing
                float t = 1f - (remaining - open_seconds) / close_seconds;
                open = 1f - Math.Clamp(t, 0f, 1f);
            }
            else
            {
                // opening
                open = 1f - Math.Clamp(remaining / open_seconds, 0f, 1f);
            }

            model.SetParameterValue(CubismDefaultParameterId.ParamEyeLOpen, open);
            model.SetParameterValue(CubismDefaultParameterId.ParamEyeROpen, open);
        }

        private static CubismBreath createDefaultBreath() => new CubismBreath
        {
            Parameters =
            [
                // Forward-facing side tilt only (\ /), not left/right yaw.
                new BreathParameterData
                {
                    ParameterId = CubismDefaultParameterId.ParamAngleZ,
                    Offset = 0f,
                    Peak = 10f,
                    Cycle = 5.5345f,
                    Weight = 0.5f,
                },
                new BreathParameterData
                {
                    ParameterId = CubismDefaultParameterId.ParamBodyAngleZ,
                    Offset = 0f,
                    Peak = 3f,
                    Cycle = 15.5345f,
                    Weight = 0.5f,
                },
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

        private static CubismPhysics? tryLoadPhysics(Storage petsStorage, string modelEntryRelativePath)
        {
            string? physicsRel = resolveSiblingFromModel3(petsStorage, modelEntryRelativePath, "Physics");
            if (physicsRel == null)
                return null;

            string relative = physicsRel.Replace('/', Path.DirectorySeparatorChar);
            if (!petsStorage.Exists(relative))
                return null;

            try
            {
                // Live2DCSharpSDK CubismPhysics(string) opens a filesystem path (same as CubismMotion), not JSON text.
                string fullPath = petsStorage.GetFullPath(relative);
                if (!File.Exists(fullPath))
                    return null;

                var physics = new CubismPhysics(fullPath);
                Logger.Log($"Ez pet Cubism: loaded physics '{physicsRel}'", LoggingTarget.Runtime);
                return physics;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Ez pet Cubism: failed loading physics '{physicsRel}'");
                return null;
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

                int[] order = new int[count];
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
                    ushort* indicesPtr = model.GetDrawableVertexIndices(di);

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

                    ushort[] indices = new ushort[indexCount];
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
            physics = null;
            motions.Clear();
            hasMotionLibrary = false;
            activeMotionKey = null;
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
                    string rel = t.ToString();
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

        private static string? resolveSiblingFromModel3(Storage petsStorage, string modelEntryRelativePath, string fileReferenceKey)
        {
            string normalised = modelEntryRelativePath.Replace('\\', '/');
            if (!normalised.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase))
                return null;

            string dir = Path.GetDirectoryName(normalised.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;

            try
            {
                using var stream = petsStorage.GetStream(normalised.Replace('/', Path.DirectorySeparatorChar));
                if (stream == null)
                    return null;

                using var reader = new StreamReader(stream);
                var root = JObject.Parse(reader.ReadToEnd());
                string? rel = root["FileReferences"]?[fileReferenceKey]?.ToString();
                if (string.IsNullOrWhiteSpace(rel))
                    return null;

                return Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar)).Replace('\\', '/');
            }
            catch
            {
                return null;
            }
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
