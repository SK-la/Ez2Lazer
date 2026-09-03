// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// Tracks a fire-and-forget UI background job so screens can cancel on exit and ignore stale callbacks.
    /// </summary>
    public sealed class BmsUiBackgroundOperation : IDisposable
    {
        private readonly CancellationTokenSource cts;
        private int disposed;

        public BmsUiBackgroundOperation(CancellationToken externalToken = default)
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            Id = Interlocked.Increment(ref nextID);
        }

        private static long nextID;

        public long Id { get; }

        public CancellationToken Token => cts.Token;

        public bool IsCancelled => disposed != 0 || cts.IsCancellationRequested;

        public void Cancel()
        {
            if (disposed != 0)
                return;

            cts.Cancel();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            cts.Cancel();
            cts.Dispose();
        }
    }
}
