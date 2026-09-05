// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework.Core;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Resolves <c>Live2DCubismCore.dll</c> from the pets storage before any Cubism P/Invoke.
    /// </summary>
    public static class EzPetCubismNative
    {
        public const string CORE_DIRECTORY = "_cubism";
        public const string CORE_DLL_WINDOWS = "Live2DCubismCore.dll";

        private static bool resolverInstalled;
        private static IntPtr coreHandle;

        public static string? ResolvedCorePath { get; private set; }

        public static bool TryPrepare(Storage petsStorage, out string? error)
        {
            error = null;

            string relative = Path.Combine(CORE_DIRECTORY, CORE_DLL_WINDOWS);

            if (!petsStorage.Exists(relative))
            {
                error = $"Missing {relative}. Download Cubism SDK for Native and copy Live2DCubismCore.dll there.";
                return false;
            }

            string fullPath = petsStorage.GetFullPath(relative);

            if (!File.Exists(fullPath))
            {
                error = $"Core DLL path not found on disk: {fullPath}";
                return false;
            }

            ResolvedCorePath = fullPath;

            if (!NativeLibrary.TryLoad(fullPath, out coreHandle))
            {
                error = $"NativeLibrary.Load failed for {fullPath}";
                return false;
            }

            ensureResolver();
            return true;
        }

        public static bool TryGetExport(string name, out IntPtr address)
        {
            address = IntPtr.Zero;

            if (coreHandle == IntPtr.Zero)
                return false;

            return NativeLibrary.TryGetExport(coreHandle, name, out address);
        }

        private static void ensureResolver()
        {
            if (resolverInstalled)
                return;

            IntPtr resolve(string name, Assembly _, DllImportSearchPath? __)
            {
                if (!string.Equals(name, "Live2DCubismCore", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("Live2DCubismCore", StringComparison.OrdinalIgnoreCase))
                    return IntPtr.Zero;

                return coreHandle;
            }

            NativeLibrary.SetDllImportResolver(typeof(CubismCore).Assembly, resolve);
            NativeLibrary.SetDllImportResolver(typeof(EzPetCubismNative).Assembly, resolve);
            resolverInstalled = true;
        }
    }
}
