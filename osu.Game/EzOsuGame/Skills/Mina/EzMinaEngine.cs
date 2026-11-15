// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using Wasmtime;

namespace osu.Game.EzOsuGame.Skills.Mina
{
    /// <summary>
    /// Raised when the n-key MinaCalc engine fails to rate a chart (host trap, aborted
    /// compute, missing native runtime). The engine instance is discarded before this is
    /// thrown, so callers may keep using the same <see cref="EzMinaEngine"/>.
    /// </summary>
    public sealed class EzMinaEngineException : Exception
    {
        public EzMinaEngineException(string message)
            : base(message)
        {
        }

        public EzMinaEngineException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// The default 4-18K mania MinaCalc engine (chart Mina + per-play <see cref="CalculatePlayerMinaSkillAxis"/>):
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
    public sealed class EzMinaEngine : IEzMinaEngine
    {
        /// <summary>
        /// Revision stamped on every Mina row, chart side and player side alike.
        /// </summary>
        /// <remarks>
        /// Bump when the chain this engine owns changes: note conversion, the goal it is asked for, or the clamp it
        /// applies. One number covers both sides because they are one algorithm reading the same note array.
        /// <see cref="EngineVersion"/> is a different thing - it names the vendored module build.
        /// </remarks>
        public const int VERSION = 20240101;

        /// <summary>Lowest keymode the n-key MinaCalc build accepts.</summary>
        public const int MIN_KEY_COUNT = 4;

        /// <summary>Highest keymode the n-key MinaCalc build accepts.</summary>
        public const int MAX_KEY_COUNT = 18;

        /// <summary>Goal the engine treats as the chart-side baseline (Mina).</summary>
        public const float MSD_GOAL = 0.93f;

        /// <summary>Lowest accuracy goal passed to <see cref="CalculatePlayerMinaSkillAxis"/> (matches the previous facade clamp).</summary>
        public const float MIN_RATING_GOAL = 0.8f;

        /// <summary>Highest accuracy goal passed to <see cref="CalculatePlayerMinaSkillAxis"/> (matches the previous facade clamp).</summary>
        public const float MAX_RATING_GOAL = 0.9975f;

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

        private readonly Lock syncRoot = new Lock();

        private Store? store;
        private Instance? instance;
        private Memory? memory;
        private Func<int, int>? malloc;
        private Action<int>? free;
        private Func<int, float, float, int, int, int, int, int>? compute;

        private bool disposed;

        public EzMinaSkillAxisVector CalculateMinaSkillAxis(ReadOnlySpan<EzCalcNote> notes, int keyCount, float rate = 1f)
            => calculate(notes, keyCount, rate, MSD_GOAL);

        public EzMinaSkillAxisVector CalculatePlayerMinaSkillAxis(ReadOnlySpan<EzCalcNote> notes, int keyCount, float rate, float goal)
            => calculate(notes, keyCount, rate, Math.Clamp(goal, MIN_RATING_GOAL, MAX_RATING_GOAL));

        private EzMinaSkillAxisVector calculate(ReadOnlySpan<EzCalcNote> notes, int keyCount, float rate, float goal)
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
                throw new EzMinaEngineException($"MinaCalc n-key compute failed (keys={keyCount}, rows={notes.Length}): {e.Message}", e);
            }

            if (isFloorOutput(raw))
            {
                // A healthy chart essentially never returns MinaCalc's resting floor; a poisoned
                // instance returns it for everything. Retry once on a fresh instance: a genuine
                // floor reproduces, a poisoned one comes back real.
                invalidateInstance();

                try
                {
                    raw = computeOnce(notes, keyCount, rate, goal);
                }
                catch (Exception e)
                {
                    throw new EzMinaEngineException($"MinaCalc n-key compute failed on retry (keys={keyCount}, rows={notes.Length}): {e.Message}", e);
                }
            }

            return EzMinaSkillAxisVector.FromRaw(raw);
        }

        /// <summary>
        /// MinaCalc's resting floor: every non-Stamina skillset identical. Returns false for the
        /// zero vector and for genuinely different axes.
        /// </summary>
        private static bool isFloorOutput(ReadOnlySpan<float> raw)
        {
            if (raw.Length < EzMinaSkillAxisVector.RAW_LENGTH)
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
            int ptrOut = malloc!(EzMinaSkillAxisVector.RAW_LENGTH * sizeof(float));

            if (ptrMasks == 0 || ptrTimes == 0 || ptrOut == 0)
                throw new EzMinaEngineException("MinaCalc n-key malloc returned a null pointer.");

            Span<uint> maskSpan = wasmMemory.GetSpan<uint>(ptrMasks, count);
            Span<float> timeSpan = wasmMemory.GetSpan<float>(ptrTimes, count);

            for (int i = 0; i < count; i++)
            {
                maskSpan[i] = notes[i].Notes;
                timeSpan[i] = notes[i].RowTime;
            }

            wasmMemory.GetSpan<float>(ptrOut, EzMinaSkillAxisVector.RAW_LENGTH).Clear();

            int rc = compute!(keyCount, rate, goal, ptrMasks, ptrTimes, count, ptrOut);

            if (rc == 0)
                throw new EzMinaEngineException("MinaCalc n-key compute reported failure.");

            float[] raw = new float[EzMinaSkillAxisVector.RAW_LENGTH];
            wasmMemory.GetSpan<float>(ptrOut, EzMinaSkillAxisVector.RAW_LENGTH).CopyTo(raw);

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
                                ?? throw new EzMinaEngineException("MinaCalc n-key module did not export 'memory'.");

                var newMalloc = newInstance.GetFunction<int, int>(EzNKeyWasmModule.MALLOC_EXPORT)
                                ?? throw new EzMinaEngineException("MinaCalc n-key module did not export 'malloc'.");

                var newFree = newInstance.GetAction<int>(EzNKeyWasmModule.FREE_EXPORT)
                              ?? throw new EzMinaEngineException("MinaCalc n-key module did not export 'free'.");

                var newCompute = newInstance.GetFunction<int, float, float, int, int, int, int, int>(EzNKeyWasmModule.COMPUTE_EXPORT)
                                 ?? throw new EzMinaEngineException("MinaCalc n-key module did not export 'minacalc_compute'.");

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
        private void invalidateInstance()
        {
            lock (syncRoot)
                invalidateInstanceLocked();
        }

        /// <summary>Lock-held variant of <see cref="invalidateInstance"/>.</summary>
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
    /// One <see cref="EzMinaEngine"/> per calling thread, reused across the charts that thread rates.
    /// </summary>
    /// <remarks>
    /// The wasm module is shared process-wide, but a Store/Instance pair is not: it carries mutable globals and must
    /// not be entered from two threads at once. Creating one per chart made every rating pay for an instance build
    /// even though the analysis worker rates charts back to back on the same thread. A trapped engine discards its
    /// instance and rebuilds on the next call, so a failed chart does not poison the pooled engine.
    /// </remarks>
    public static class EzMinaEnginePool
    {
        private static readonly ThreadLocal<EzMinaEngine> thread_engine = new ThreadLocal<EzMinaEngine>(static () => new EzMinaEngine());

        /// <summary>The calling thread's engine, created on first use.</summary>
        public static EzMinaEngine Rent()
        {
            EzMinaEngine? engine = thread_engine.Value;

            if (engine == null)
            {
                engine = new EzMinaEngine();
                thread_engine.Value = engine;
            }

            return engine;
        }

        /// <summary>Drops the calling thread's engine. Only for tests and shutdown of a worker thread.</summary>
        public static void Release()
        {
            if (!thread_engine.IsValueCreated)
                return;

            thread_engine.Value?.Dispose();
            thread_engine.Value = null!;
        }
    }
}
