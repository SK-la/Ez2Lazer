// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics
{
    /// <summary>
    /// Process-wide gate for offline analytics: suppresses preview/decoding noise and forwards cancellation into the BMS decoder.
    /// </summary>
    internal static class BmsAnalyticsScanContext
    {
        private static int activeCount;

        public static bool IsRunning => Volatile.Read(ref activeCount) > 0;

        public static bool SuppressDecoderVerboseLogging { get; private set; }

        public static CancellationToken ActiveCancellation { get; private set; }

        public static bool TryEnter(CancellationToken cancellationToken, out IDisposable? scope)
        {
            if (Interlocked.CompareExchange(ref activeCount, 1, 0) != 0)
            {
                scope = null;
                return false;
            }

            SuppressDecoderVerboseLogging = true;
            ActiveCancellation = cancellationToken;
            scope = new Scope();
            return true;
        }

        private sealed class Scope : IDisposable
        {
            private int disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                    return;

                SuppressDecoderVerboseLogging = false;
                ActiveCancellation = CancellationToken.None;
                Volatile.Write(ref activeCount, 0);
            }
        }
    }
}
