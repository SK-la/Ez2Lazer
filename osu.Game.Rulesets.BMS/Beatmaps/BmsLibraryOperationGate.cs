// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Process-wide single-flight gate for scan + Realm sync. A new begin cancels the previous operation.
    /// </summary>
    public sealed class BmsLibraryOperationGate
    {
        public static BmsLibraryOperationGate Shared { get; } = new BmsLibraryOperationGate();

        private readonly object gateLock = new object();
        private OperationHandle? activeHandle;
        private long activeOperationId;
        private int busy;

        public bool IsBusy => Volatile.Read(ref busy) != 0;

        public long CurrentOperationId
        {
            get
            {
                lock (gateLock)
                    return activeOperationId;
            }
        }

        public OperationHandle Begin(CancellationToken externalToken = default)
        {
            lock (gateLock)
            {
                activeHandle?.Supersede();

                var linked = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
                activeOperationId++;
                Volatile.Write(ref busy, 1);
                activeHandle = new OperationHandle(this, activeOperationId, linked);
                return activeHandle;
            }
        }

        public void CancelCurrent()
        {
            lock (gateLock)
                activeHandle?.Cancel();
        }

        private void complete(OperationHandle handle)
        {
            lock (gateLock)
            {
                if (ReferenceEquals(activeHandle, handle))
                {
                    activeHandle = null;
                    Volatile.Write(ref busy, 0);
                }
            }
        }

        public sealed class OperationHandle : IDisposable
        {
            private readonly BmsLibraryOperationGate gate;
            private readonly CancellationTokenSource cts;
            private int disposed;
            private int superseded;

            internal OperationHandle(BmsLibraryOperationGate gate, long id, CancellationTokenSource cts)
            {
                this.gate = gate;
                this.cts = cts;
                Id = id;
            }

            public long Id { get; }

            public CancellationToken Token => disposed != 0 || superseded != 0
                ? new CancellationToken(canceled: true)
                : cts.Token;

            public void Cancel()
            {
                if (disposed != 0 || superseded != 0)
                    return;

                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // ignored
                }
            }

            internal void Supersede()
            {
                if (Interlocked.Exchange(ref superseded, 1) != 0)
                    return;

                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // ignored
                }

                cts.Dispose();
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                    return;

                gate.complete(this);

                if (Volatile.Read(ref superseded) == 0)
                    cts.Dispose();
            }
        }
    }
}
