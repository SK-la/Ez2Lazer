// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.IO;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Parses a BMS chart file from disk without constructing <see cref="BMSWorkingBeatmap"/> (no audio/skin/track IO).
    /// </summary>
    public static class BmsChartFileParser
    {
        public static BMSBeatmap? TryParse(string chartPath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(chartPath))
                return null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var stream = File.OpenRead(chartPath);
                using var reader = new LineBufferedReader(stream);
                return new BMSBeatmapDecoder().Decode(reader) as BMSBeatmap;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }
    }
}
