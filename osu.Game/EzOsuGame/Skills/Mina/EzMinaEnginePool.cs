// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;

namespace osu.Game.EzOsuGame.Skills.Mina
{
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
        private static readonly ThreadLocal<EzMinaEngine> threadEngine = new ThreadLocal<EzMinaEngine>(static () => new EzMinaEngine());

        /// <summary>The calling thread's engine, created on first use.</summary>
        public static EzMinaEngine Rent()
        {
            EzMinaEngine? engine = threadEngine.Value;

            if (engine == null)
            {
                engine = new EzMinaEngine();
                threadEngine.Value = engine;
            }

            return engine;
        }

        /// <summary>Drops the calling thread's engine. Only for tests and shutdown of a worker thread.</summary>
        public static void Release()
        {
            if (!threadEngine.IsValueCreated)
                return;

            threadEngine.Value?.Dispose();
            threadEngine.Value = null!;
        }
    }
}
