// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework.Core;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Resolves the Cubism Core dynamic library from Pets/_cubism before any Cubism P/Invoke.
    /// Accepts Ez RID folders (<c>win-x64/…</c>), Cubism SDK <c>Core/dll</c> layout
    /// (<c>windows/x86_64/…</c>), and legacy flat Windows DLL.
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
        /// Candidate relative paths for the current OS/arch, in search order.
        /// </summary>
        public static IReadOnlyList<string> GetCoreSearchRelativePaths()
        {
            var paths = new List<string>();
            string fileName = GetNativeLibraryFileName();
            Architecture arch = RuntimeInformation.ProcessArchitecture;

            string? rid = ResolveCurrentRid();
            if (rid != null)
                paths.Add(Path.Combine(CORE_DIRECTORY, rid, fileName).Replace('\\', '/'));

            // Cubism SDK Core/dll layout (users often drop the whole tree into _cubism/).
            foreach (string sdkRel in getSdkLayoutRelativeCandidates(arch, fileName))
                paths.Add(Path.Combine(CORE_DIRECTORY, sdkRel).Replace('\\', '/'));

            if (OperatingSystem.IsWindows())
                paths.Add(Path.Combine(CORE_DIRECTORY, CORE_DLL_WINDOWS).Replace('\\', '/'));

            return paths;
        }

        /// <summary>
        /// Locates Core under Pets storage without loading it.
        /// </summary>
        public static string? FindCoreRelativePath(Storage petsStorage)
        {
            foreach (string relative in GetCoreSearchRelativePaths())
            {
                string diskRel = relative.Replace('/', Path.DirectorySeparatorChar);
                if (petsStorage.Exists(diskRel))
                    return relative;
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
                string sdkHint = OperatingSystem.IsWindows()
                    ? "_cubism/windows/x86_64/Live2DCubismCore.dll"
                    : OperatingSystem.IsMacOS()
                        ? "_cubism/macos/libLive2DCubismCore.dylib"
                        : "_cubism/linux/x86_64/libLive2DCubismCore.so";

                error =
                    $"Missing Cubism Core (tried {expected} and SDK layout e.g. {sdkHint}). "
                    + "Copy from Cubism SDK for Native Core/dll — not the static .lib/.a.";
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

        private static IEnumerable<string> getSdkLayoutRelativeCandidates(Architecture arch, string fileName)
        {
            if (OperatingSystem.IsWindows())
            {
                yield return arch switch
                {
                    Architecture.X86 => Path.Combine("windows", "x86", fileName),
                    Architecture.Arm64 => Path.Combine("windows", "arm64", fileName),
                    _ => Path.Combine("windows", "x86_64", fileName),
                };

                yield break;
            }

            if (OperatingSystem.IsLinux())
            {
                yield return arch switch
                {
                    Architecture.Arm64 => Path.Combine("linux", "arm64", fileName),
                    _ => Path.Combine("linux", "x86_64", fileName),
                };

                yield break;
            }

            if (OperatingSystem.IsMacOS())
            {
                // Newer SDKs: macos/arm64 or macos/x86_64; older/flat: macos/<dylib>.
                if (arch == Architecture.Arm64)
                    yield return Path.Combine("macos", "arm64", fileName);
                else
                    yield return Path.Combine("macos", "x86_64", fileName);

                yield return Path.Combine("macos", fileName);
            }
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
