// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading;
using Wasmtime;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Raised when the n-key MinaCalc engine fails to rate a chart (host trap, aborted
    /// compute, missing native runtime). The engine instance is discarded before this is
    /// thrown, so callers may keep using the same <see cref="EzNKeyMsdEngine"/>.
    /// </summary>
    public sealed class EzMsdEngineException : Exception
    {
        public EzMsdEngineException(string message)
            : base(message)
        {
        }

        public EzMsdEngineException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// The default 4-18K mania MSD/SSR engine: the mania-hub n-key MinaCalc build
    /// (<c>minaclac-74.0.wasm</c>, see <c>Resources/EzSkills/NOTICE.md</c>) hosted in-process
    /// through Wasmtime.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One instance = one Wasmtime <see cref="Store"/>/<see cref="Instance"/>. The compiled
    /// <see cref="Module"/> is process-wide and shared, so instance creation is cheap but not
    /// free — reuse an engine across a batch of charts and keep it off multiple threads.
    /// </para>
    /// <para>
    /// The module carries mutable globals and is not re-entrant. A trap (e.g. a chart with a
    /// chord wider than 16 columns) leaves the instance mid-compute, so it is discarded and
    /// transparently rebuilt on the next call. This mirrors mania-hub's module eviction.
    /// </para>
    /// </remarks>
    public sealed class EzNKeyMsdEngine : IEzMsdEngine
    {
        /// <summary>Lowest keymode the n-key MinaCalc build accepts.</summary>
        public const int MIN_KEY_COUNT = 4;

        /// <summary>Highest keymode the n-key MinaCalc build accepts.</summary>
        public const int MAX_KEY_COUNT = 18;

        /// <summary>Goal the engine treats as the chart-side baseline (MSD).</summary>
        public const float MSD_GOAL = 0.93f;

        /// <summary>Lowest SSR goal the engine is asked for (matches the previous facade clamp).</summary>
        public const float MIN_SSR_GOAL = 0.8f;

        /// <summary>Highest SSR goal the engine is asked for (matches the previous facade clamp).</summary>
        public const float MAX_SSR_GOAL = 0.9975f;

        /// <summary>
        /// MinaCalc needs an interval between rows; charts with fewer rows than this
        /// are reported as unrateable instead of being fed to the engine.
        /// </summary>
        public const int MIN_RATEABLE_ROWS = 2;

        /// <summary>True when the n-key engine rates <paramref name="keyCount"/>.</summary>
        public static bool IsSupportedKeyCount(int keyCount) => keyCount is >= MIN_KEY_COUNT and <= MAX_KEY_COUNT;

        public int MinKeyCount => MIN_KEY_COUNT;

        public int MaxKeyCount => MAX_KEY_COUNT;

        public string EngineVersion => EzNKeyWasmModule.ENGINE_VERSION;

        public bool SupportsKeyCount(int keyCount) => IsSupportedKeyCount(keyCount);

        private readonly object syncRoot = new();

        private Store? store;
        private Instance? instance;
        private Memory? memory;
        private Func<int, int>? malloc;
        private Action<int>? free;
        private Func<int, float, float, int, int, int, int, int>? compute;

        private bool disposed;

        public EzSkillsetVector CalculateMsd(ReadOnlySpan<EzCalcNote> notes, int keyCount, float rate = 1f)
            => calculate(notes, keyCount, rate, MSD_GOAL);

        public EzSkillsetVector CalculateSsr(ReadOnlySpan<EzCalcNote> notes, int keyCount, float rate, float goal)
            => calculate(notes, keyCount, rate, Math.Clamp(goal, MIN_SSR_GOAL, MAX_SSR_GOAL));

        private EzSkillsetVector calculate(ReadOnlySpan<EzCalcNote> notes, int keyCount, float rate, float goal)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (!SupportsKeyCount(keyCount) || notes.Length < MIN_RATEABLE_ROWS)
                return default;

            // Non-finite or non-positive rate would silently produce garbage; keep the caller's
            // contract (a valid rate) but never hand the engine an unusable value.
            if (!float.IsFinite(rate) || rate <= 0)
                rate = 1f;

            float[] raw;

            try
            {
                raw = computeOnce(notes, keyCount, rate, goal);
            }
            catch (Exception e)
            {
                throw new EzMsdEngineException($"MinaCalc n-key compute failed (keys={keyCount}, rows={notes.Length}): {e.Message}", e);
            }

            if (isFloorOutput(raw))
            {
                // A healthy chart essentially never returns MinaCalc's resting floor; a poisoned
                // instance returns it for everything. Retry once on a fresh instance: a genuine
                // floor reproduces, a poisoned one comes back real.
                InvalidateInstance();

                try
                {
                    raw = computeOnce(notes, keyCount, rate, goal);
                }
                catch (Exception e)
                {
                    throw new EzMsdEngineException($"MinaCalc n-key compute failed on retry (keys={keyCount}, rows={notes.Length}): {e.Message}", e);
                }
            }

            return EzSkillsetVector.FromRaw(raw);
        }

        /// <summary>
        /// MinaCalc's resting floor: every non-Stamina skillset identical. Returns false for the
        /// zero vector and for genuinely different axes.
        /// </summary>
        private static bool isFloorOutput(ReadOnlySpan<float> raw)
        {
            if (raw.Length < EzSkillsetVector.RawLength)
                return false;

            float stream = raw[1];

            return stream > 0
                   && stream == raw[7] // Technical
                   && stream == raw[6] // Chordjack
                   && stream == raw[2] // Jumpstream
                   && stream == raw[3] // Handstream
                   && stream == raw[5]; // JackSpeed
        }

        private float[] computeOnce(ReadOnlySpan<EzCalcNote> notes, int keyCount, float rate, float goal)
        {
            lock (syncRoot)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                ensureInstance();

                try
                {
                    return computeOnInstance(notes, keyCount, rate, goal);
                }
                catch
                {
                    // A trap (or a failed compute) leaves the instance mid-compute: its globals and
                    // allocator state may be inconsistent. Drop it so the next call rebuilds.
                    invalidateInstanceLocked();
                    throw;
                }
            }
        }

        private float[] computeOnInstance(ReadOnlySpan<EzCalcNote> notes, int keyCount, float rate, float goal)
        {
            Memory wasmMemory = memory!;
            int count = notes.Length;

            int ptrMasks = malloc!(count * sizeof(uint));
            int ptrTimes = malloc!(count * sizeof(float));
            int ptrOut = malloc!(EzSkillsetVector.RawLength * sizeof(float));

            if (ptrMasks == 0 || ptrTimes == 0 || ptrOut == 0)
                throw new EzMsdEngineException("MinaCalc n-key malloc returned a null pointer.");

            Span<uint> maskSpan = wasmMemory.GetSpan<uint>(ptrMasks, count);
            Span<float> timeSpan = wasmMemory.GetSpan<float>(ptrTimes, count);

            for (int i = 0; i < count; i++)
            {
                maskSpan[i] = notes[i].Notes;
                timeSpan[i] = notes[i].RowTime;
            }

            wasmMemory.GetSpan<float>(ptrOut, EzSkillsetVector.RawLength).Clear();

            int rc = compute!(keyCount, rate, goal, ptrMasks, ptrTimes, count, ptrOut);

            if (rc == 0)
                throw new EzMsdEngineException("MinaCalc n-key compute reported failure.");

            var raw = new float[EzSkillsetVector.RawLength];
            wasmMemory.GetSpan<float>(ptrOut, EzSkillsetVector.RawLength).CopyTo(raw);

            Action<int> release = free!;
            release(ptrMasks);
            release(ptrTimes);
            release(ptrOut);

            return raw;
        }

        private void ensureInstance()
        {
            if (instance != null)
                return;

            EzNKeyWasmModule.SharedState shared = EzNKeyWasmModule.Shared;
            var newStore = new Store(shared.Engine);

            try
            {
                var newInstance = shared.Linker.Instantiate(newStore, shared.Module);

                newInstance.GetAction(EzNKeyWasmModule.CTOR_EXPORT)?.Invoke();

                var newMemory = newInstance.GetMemory(EzNKeyWasmModule.MEMORY_EXPORT)
                                ?? throw new EzMsdEngineException("MinaCalc n-key module did not export 'memory'.");

                var newMalloc = newInstance.GetFunction<int, int>(EzNKeyWasmModule.MALLOC_EXPORT)
                                ?? throw new EzMsdEngineException("MinaCalc n-key module did not export 'malloc'.");

                var newFree = newInstance.GetAction<int>(EzNKeyWasmModule.FREE_EXPORT)
                              ?? throw new EzMsdEngineException("MinaCalc n-key module did not export 'free'.");

                var newCompute = newInstance.GetFunction<int, float, float, int, int, int, int, int>(EzNKeyWasmModule.COMPUTE_EXPORT)
                                 ?? throw new EzMsdEngineException("MinaCalc n-key module did not export 'minacalc_compute'.");

                memory = newMemory;
                malloc = newMalloc;
                free = newFree;
                compute = newCompute;
                store = newStore;
                instance = newInstance;
            }
            catch
            {
                newStore.Dispose();
                throw;
            }
        }

        /// <summary>Drops the current instance; the next compute builds a fresh one.</summary>
        private void InvalidateInstance()
        {
            lock (syncRoot)
                invalidateInstanceLocked();
        }

        /// <summary>Lock-held variant of <see cref="InvalidateInstance"/>.</summary>
        private void invalidateInstanceLocked()
        {
            store?.Dispose();

            store = null;
            instance = null;
            memory = null;
            malloc = null;
            free = null;
            compute = null;
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                    return;

                disposed = true;
                invalidateInstanceLocked();
            }
        }
    }

    /// <summary>
    /// Process-wide compiled MinaCalc n-key module. Compilation is expensive and the module is
    /// immutable, so it is shared by every <see cref="EzNKeyMsdEngine"/>; only the
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

        private static readonly object shared_lock = new();
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
                throw new EzMsdEngineException(
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
