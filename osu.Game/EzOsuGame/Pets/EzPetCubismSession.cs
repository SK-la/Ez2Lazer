// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Model;
using osu.Framework.Logging;
using osu.Framework.Platform;
using LogLevel = Live2DCSharpSDK.Framework.LogLevel;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Minimal Cubism session: load moc3, drive breath, expose drawable count.
    /// Full mesh rendering into osu-framework DrawNode is a follow-up; this proves Core + model load for testing.
    /// </summary>
    public sealed class EzPetCubismSession : IDisposable
    {
        private CubismMoc? moc;
        private CubismBreath? breath;
        private float userTime;
        private static bool frameworkStarted;

        public bool IsReady { get; private set; }

        public string? Status { get; private set; }

        public int DrawableCount { get; private set; }

        public float BreathValue { get; private set; }

        public string? LastState { get; private set; }

        public string? LastClip { get; private set; }

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
                created.Status = $"Core OK · {Path.GetFileName(mocRelative)} · drawables={created.DrawableCount}";
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

            var ok = CubismFramework.StartUp(new EzCubismAllocator(), new Option
            {
                LogFunction = msg => Logger.Log($"[Cubism] {msg}", LoggingTarget.Runtime),
                LoggingLevel = LogLevel.Warning,
            });

            if (!ok)
                throw new InvalidOperationException("CubismFramework.StartUp failed");

            frameworkStarted = true;
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

                // Convention: Foo.model3.json → Foo.moc3 beside it
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
