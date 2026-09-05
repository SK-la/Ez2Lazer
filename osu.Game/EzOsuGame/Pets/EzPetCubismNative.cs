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
    /// Resolves the Cubism Core dynamic library from Pets/_cubism before any Cubism P/Invoke.
    /// Layout: <c>_cubism/&lt;rid&gt;/&lt;nativeName&gt;</c> (e.g. win-x64/Live2DCubismCore.dll).
    /// Also accepts legacy flat <c>_cubism/Live2DCubismCore.dll</c> on Windows.
    /// </summary>
    public static class EzPetCubismNative
    {
        public const string CORE_DIRECTORY = "_cubism";
        public const string CORE_DLL_WINDOWS = "Live2DCubismCore.dll";
        public const string CORE_SO_LINUX = "libLive2DCubismCore.so";
        public const string CORE_DYLIB_MACOS = "libLive2DCubismCore.dylib";

        private static bool resolverInstalled;
        private static IntPtr coreHandle;

        public static string? ResolvedCorePath { get; private set; }

        /// <summary>
        /// RID folder under <see cref="CORE_DIRECTORY"/> for the running process, or null if unsupported.
        /// </summary>
        public static string? ResolveCurrentRid()
        {
            Architecture arch = RuntimeInformation.ProcessArchitecture;

            if (OperatingSystem.IsWindows())
            {
                return arch switch
                {
                    Architecture.X64 => "win-x64",
                    Architecture.X86 => "win-x86",
                    Architecture.Arm64 => "win-arm64",
                    _ => null,
                };
            }

            if (OperatingSystem.IsLinux())
            {
                return arch switch
                {
                    Architecture.X64 => "linux-x64",
                    Architecture.Arm64 => "linux-arm64",
                    _ => null,
                };
            }

            if (OperatingSystem.IsMacOS())
            {
                return arch switch
                {
                    Architecture.X64 => "osx-x64",
                    Architecture.Arm64 => "osx-arm64",
                    _ => null,
                };
            }

            return null;
        }

        /// <summary>
        /// Native Core file name for the current OS (dll / so / dylib).
        /// </summary>
        public static string GetNativeLibraryFileName()
        {
            if (OperatingSystem.IsWindows())
                return CORE_DLL_WINDOWS;

            if (OperatingSystem.IsMacOS())
                return CORE_DYLIB_MACOS;

            return CORE_SO_LINUX;
        }

        /// <summary>
        /// Preferred relative path under Pets storage for the current platform
        /// (e.g. <c>_cubism/win-x64/Live2DCubismCore.dll</c>).
        /// </summary>
        public static string GetExpectedCoreRelativePath()
        {
            string? rid = ResolveCurrentRid();
            string fileName = GetNativeLibraryFileName();

            if (rid == null)
                return Path.Combine(CORE_DIRECTORY, fileName).Replace('\\', '/');

            return Path.Combine(CORE_DIRECTORY, rid, fileName).Replace('\\', '/');
        }

        /// <summary>
        /// Locates Core under Pets storage without loading it. Prefers RID path, then legacy flat Windows DLL.
        /// </summary>
        public static string? FindCoreRelativePath(Storage petsStorage)
        {
            string expected = GetExpectedCoreRelativePath().Replace('/', Path.DirectorySeparatorChar);

            if (petsStorage.Exists(expected))
                return expected.Replace('\\', '/');

            // Legacy: _cubism/Live2DCubismCore.dll (Windows only).
            if (OperatingSystem.IsWindows())
            {
                string flat = Path.Combine(CORE_DIRECTORY, CORE_DLL_WINDOWS);
                if (petsStorage.Exists(flat))
                    return flat.Replace('\\', '/');
            }

            return null;
        }

        public static bool HasCubismCoreOnDisk(Storage petsStorage)
            => FindCoreRelativePath(petsStorage) != null;

        public static bool TryPrepare(Storage petsStorage, out string? error)
        {
            error = null;

            string? relative = FindCoreRelativePath(petsStorage);

            if (relative == null)
            {
                string expected = GetExpectedCoreRelativePath();
                error = $"Missing Cubism Core at {expected}. Copy the matching dynamic library from Cubism SDK for Native (Core/dll), not the static .lib/.a.";
                return false;
            }

            string fullPath = petsStorage.GetFullPath(relative.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(fullPath))
            {
                error = $"Core library path not found on disk: {fullPath}";
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
