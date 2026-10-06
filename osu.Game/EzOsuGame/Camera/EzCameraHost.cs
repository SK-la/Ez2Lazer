// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using osu.Framework.Bindables;
using osu.Framework.Logging;

namespace osu.Game.EzOsuGame.Camera
{
    /// <summary>
    /// Gate for the camera HUD. While <see cref="Enabled"/> is false, <see cref="TryGetBackend"/> does not call
    /// <see cref="createBackend"/>, so the Windows capture assembly stays unloaded and no device is opened.
    /// </summary>
    public sealed class EzCameraHost : IDisposable
    {
        private readonly Func<IEzCameraBackend>? createBackend;
        private readonly Lock gate = new Lock();
        private IEzCameraBackend? backend;
        private bool failed;
        private bool disposed;

        public EzCameraHost(Bindable<bool> enabled, Func<IEzCameraBackend>? createBackend = null)
        {
            Enabled = enabled;
            this.createBackend = createBackend;
            Enabled.ValueChanged += onEnabledChanged;
        }

        public Bindable<bool> Enabled { get; }

        /// <summary>True when this build has a capture backend. Does not load it.</summary>
        public bool IsPlatformSupported => createBackend != null;

        public IEzCameraBackend? TryGetBackend()
        {
            if (!Enabled.Value || createBackend == null)
                return null;

            IEzCameraBackend? discarded = null;

            lock (gate)
            {
                if (!Enabled.Value || failed || disposed)
                    return null;

                if (backend != null)
                    return backend;

                try
                {
                    backend = createBackend();
                }
                catch (Exception e)
                {
                    failed = true;
                    Logger.Error(e, "Failed to create the Ez camera backend");
                    return null;
                }

                if (backend == null)
                {
                    failed = true;
                    return null;
                }

                if (Enabled.Value && !disposed)
                    return backend;

                discarded = backend;
                backend = null;
                failed = false;
            }

            discarded.Dispose();
            return null;
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                    return;

                disposed = true;
            }

            detachBackend()?.Dispose();
            Enabled.ValueChanged -= onEnabledChanged;
        }

        private void onEnabledChanged(ValueChangedEvent<bool> e)
        {
            if (e.NewValue)
                return;

            detachBackend()?.Dispose();
        }

        private IEzCameraBackend? detachBackend()
        {
            lock (gate)
            {
                failed = false;
                IEzCameraBackend? local = backend;
                backend = null;
                return local;
            }
        }
    }

    /// <summary>
    /// Loads <c>osu.Game.EzCamera.Windows.dll</c> from the game directory. Only invoked after the camera HUD switch is on.
    /// </summary>
    internal static class EzCameraBackendLoader
    {
        private const string assembly_file = "osu.Game.EzCamera.Windows.dll";
        private const string type_name = "osu.Game.EzCamera.Windows.WindowsEzCameraService";
        private static int resolveHooked;

        public static IEzCameraBackend Create()
        {
            string directory = Path.GetDirectoryName(typeof(EzCameraBackendLoader).Assembly.Location) ?? ".";
            string path = Path.Combine(directory, assembly_file);

            if (!File.Exists(path))
                throw new FileNotFoundException("The Ez camera backend is not present in this build.", path);

            if (Interlocked.Exchange(ref resolveHooked, 1) == 0)
            {
                AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
                {
                    string? simpleName = new AssemblyName(args.Name).Name;

                    if (simpleName != "Microsoft.Windows.SDK.NET" && simpleName != "WinRT.Runtime")
                        return null;

                    string candidate = Path.Combine(directory, simpleName + ".dll");
                    return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
                };
            }

            Assembly assembly = Assembly.LoadFrom(path);
            Type type = assembly.GetType(type_name, throwOnError: true)
                        ?? throw new InvalidOperationException($"Could not find {type_name}.");

            return (IEzCameraBackend)(Activator.CreateInstance(type)
                                      ?? throw new InvalidOperationException($"Could not create {type_name}."));
        }
    }
}
