// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading;
using Wasmtime;

namespace osu.Game.EzOsuGame.Skills.Mina
{
    /// <summary>
    /// Process-wide compiled MinaCalc n-key module. Compilation is expensive and the module is
    /// immutable, so it is shared by every <see cref="EzMinaEngine"/>; only the
    /// <see cref="Store"/> / <see cref="Instance"/> are per-engine.
    /// </summary>
    internal static class EzNKeyWasmModule
    {
        public const string ENGINE_VERSION = "0.74.0";

        /// <summary>Manifest name of the embedded wasm (see <c>Resources/EzSkills/NOTICE.md</c>).</summary>
        public const string WASM_RESOURCE_NAME = "osu.Game.Resources.EzSkills.minaclac-74.0.wasm";

        public const string MODULE_NAME = "minaclac-74.0";

        public const string MEMORY_EXPORT = "memory";
        public const string CTOR_EXPORT = "__wasm_call_ctors";
        public const string MALLOC_EXPORT = "malloc";
        public const string FREE_EXPORT = "free";
        public const string COMPUTE_EXPORT = "minacalc_compute";

        private const string wasi_module = "wasi_snapshot_preview1";
        private const string env_module = "env";

        private const int wasm_page_size = 65536;

        private static readonly Lock shared_lock = new Lock();
        private static SharedState? shared;

        public static SharedState Shared
        {
            get
            {
                SharedState? current = Volatile.Read(ref shared);

                if (current != null)
                    return current;

                lock (shared_lock)
                    return shared ??= create();
            }
        }

        private static SharedState create()
        {
            var engine = new Engine();

            try
            {
                var module = Module.FromBytes(engine, MODULE_NAME, readWasmBytes());
                var linker = new Linker(engine);
                defineImports(linker);

                return new SharedState(engine, module, linker);
            }
            catch
            {
                engine.Dispose();
                throw;
            }
        }

        private static byte[] readWasmBytes()
        {
            var assembly = typeof(EzNKeyWasmModule).Assembly;

            using Stream? stream = assembly.GetManifestResourceStream(WASM_RESOURCE_NAME);

            if (stream == null)
            {
                throw new EzMinaEngineException(
                    $"Embedded MinaCalc wasm '{WASM_RESOURCE_NAME}' was not found. Resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
            }

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        private static void defineImports(Linker linker)
        {
            // Emscripten registers the same table under both module names; MinaCalc's build only
            // ever touches stdio when it wants to print, which the release build does not.
            ValueKind[] stdioParams = [ValueKind.Int32, ValueKind.Int32, ValueKind.Int32, ValueKind.Int32];
            ValueKind[] int32Result = [ValueKind.Int32];
            ValueKind[] twoPointers = [ValueKind.Int32, ValueKind.Int32];
            ValueKind[] fourPointers = [ValueKind.Int32, ValueKind.Int32, ValueKind.Int32, ValueKind.Int32];

            linker.DefineFunction(wasi_module, "fd_write", simpleStdioStub, stdioParams, int32Result);
            linker.DefineFunction(wasi_module, "fd_read", simpleStdioStub, stdioParams, int32Result);
            linker.DefineFunction(wasi_module, "fd_close", simpleStdioStub, [ValueKind.Int32], int32Result);
            linker.DefineFunction(wasi_module, "fd_seek", fdSeekStub, [ValueKind.Int32, ValueKind.Int64, ValueKind.Int32, ValueKind.Int32], int32Result);
            linker.DefineFunction(wasi_module, "environ_sizes_get", environmentSizesStub, twoPointers, int32Result);
            linker.DefineFunction(wasi_module, "environ_get", simpleStdioStub, twoPointers, int32Result);
            linker.DefineFunction(env_module, "_abort_js", abortStub, [], []);
            linker.DefineFunction(env_module, "_tzset_js", timezoneStub, fourPointers, []);
            linker.DefineFunction(env_module, "emscripten_resize_heap", resizeHeapStub, [ValueKind.Int32], int32Result);
        }

        /// <summary>Reports an empty environment so emscripten's env helpers are satisfied.</summary>
        private static void environmentSizesStub(Caller caller, ReadOnlySpan<ValueBox> args, Span<ValueBox> results)
        {
            Memory? wasmMemory = caller.GetMemory(MEMORY_EXPORT);

            if (wasmMemory != null)
            {
                wasmMemory.WriteInt32(args[0].AsInt32(), 0);
                wasmMemory.WriteInt32(args[1].AsInt32(), 0);
            }

            results[0] = 0;
        }

        /// <summary>
        /// Succeeds without moving bytes. MinaCalc's release build never writes to stdio; if it
        /// ever does, reporting zero bytes written is preferable to tripping an emscripten trap.
        /// </summary>
        private static void simpleStdioStub(Caller caller, ReadOnlySpan<ValueBox> args, Span<ValueBox> results)
        {
            // The stdio helpers take a trailing count-out pointer; zero it when present.
            if (args.Length >= 4)
                caller.GetMemory(MEMORY_EXPORT)?.WriteInt32(args[^1].AsInt32(), 0);

            results[0] = 0;
        }

        /// <summary>
        /// Succeeds without moving the file cursor. The trailing parameter is a 64-bit
        /// offset-out pointer, so it is zeroed as a full <c>i64</c>.
        /// </summary>
        private static void fdSeekStub(Caller caller, ReadOnlySpan<ValueBox> args, Span<ValueBox> results)
        {
            caller.GetMemory(MEMORY_EXPORT)?.WriteInt64(args[^1].AsInt32(), 0);
            results[0] = 0;
        }

        private static void abortStub(Caller caller, ReadOnlySpan<ValueBox> args, Span<ValueBox> results)
            => throw new InvalidOperationException("MinaCalc n-key module called abort().");

        private static void timezoneStub(Caller caller, ReadOnlySpan<ValueBox> args, Span<ValueBox> results)
        {
            // MinaCalc does not touch the timezone; leave emscripten's buffers alone.
        }

        /// <summary>Grows linear memory on demand the way emscripten expects.</summary>
        private static void resizeHeapStub(Caller caller, ReadOnlySpan<ValueBox> args, Span<ValueBox> results)
        {
            long requested = (uint)args[0].AsInt32();
            Memory? wasmMemory = caller.GetMemory(MEMORY_EXPORT);

            if (wasmMemory == null)
            {
                results[0] = 0;
                return;
            }

            long currentPages = wasmMemory.GetSize();
            long neededPages = (requested + wasm_page_size - 1) / wasm_page_size;

            if (neededPages <= currentPages)
            {
                results[0] = 1;
                return;
            }

            long previousPages = wasmMemory.Grow(neededPages - currentPages);
            results[0] = previousPages < 0 ? 0 : 1;
        }

        public sealed class SharedState : IDisposable
        {
            public Engine Engine { get; }

            public Module Module { get; }

            public Linker Linker { get; }

            public SharedState(Engine engine, Module module, Linker linker)
            {
                Engine = engine;
                Module = module;
                Linker = linker;
            }

            public void Dispose()
            {
                Linker.Dispose();
                Module.Dispose();
                Engine.Dispose();
            }
        }
    }
}
