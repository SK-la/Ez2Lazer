// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework.Core;
using osu.Framework.Logging;
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
        private static string? resolvedCorePath;

        public static string? ResolvedCorePath => resolvedCorePath;

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

            resolvedCorePath = fullPath;
            ensureResolver();
            return true;
        }

        private static void ensureResolver()
        {
            if (resolverInstalled)
                return;

            NativeLibrary.SetDllImportResolver(typeof(CubismCore).Assembly, (name, _, _) =>
            {
                if (!string.Equals(name, "Live2DCubismCore", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("Live2DCubismCore", StringComparison.OrdinalIgnoreCase))
                    return IntPtr.Zero;

                if (string.IsNullOrEmpty(resolvedCorePath))
                    return IntPtr.Zero;

                if (NativeLibrary.TryLoad(resolvedCorePath, out var handle))
                    return handle;

                Logger.Log($"Ez pet: failed to NativeLibrary.Load('{resolvedCorePath}')", LoggingTarget.Runtime, LogLevel.Error);
                return IntPtr.Zero;
            });

            resolverInstalled = true;
        }
    }
}
