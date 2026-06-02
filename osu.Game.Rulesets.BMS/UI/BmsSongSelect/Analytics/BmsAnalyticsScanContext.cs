// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics
{
    /// <summary>
    /// Process-wide gate for offline analytics: suppresses preview/decoding noise and forwards cancellation into the BMS decoder.
    /// </summary>
    internal static class BmsAnalyticsScanContext
    {
        public static bool IsRunning { get; private set; }

        public static bool SuppressDecoderVerboseLogging { get; private set; }

        public static CancellationToken ActiveCancellation { get; private set; }

        public static IDisposable Enter(CancellationToken cancellationToken)
        {
            IsRunning = true;
            SuppressDecoderVerboseLogging = true;
            ActiveCancellation = cancellationToken;
            return new Scope();
        }

        private sealed class Scope : IDisposable
        {
            public void Dispose()
            {
                IsRunning = false;
                SuppressDecoderVerboseLogging = false;
                ActiveCancellation = CancellationToken.None;
            }
        }
    }
}
