// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using osu.Game.Beatmaps;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace osu.Game.EzOsuGame.Scoring
{
    public static class EzReplayExportEncoder
    {
        public const string EXTENSION = @".ezosr";
        public const string LEGACY_REPLAY_FILENAME = @"replay.osr";

        public static void EncodeEzOsr(Score score, IBeatmap beatmap, Stream output, Stream? highPrecisionReplay = null)
        {
            using var legacyReplay = new MemoryStream();
            new LegacyScoreEncoder(score, beatmap).Encode(legacyReplay, leaveOpen: true);
            legacyReplay.Position = 0;

            if (highPrecisionReplay != null)
            {
                if (highPrecisionReplay.CanSeek)
                    highPrecisionReplay.Position = 0;

                WriteArchive(output, legacyReplay, highPrecisionReplay);
                return;
            }

            using var frames = new MemoryStream();
            EzHighPrecisionReplayFrames.Write(frames, score, beatmap);
            frames.Position = 0;
            WriteArchive(output, legacyReplay, frames);
        }

        internal static void WriteArchive(Stream output, Stream legacyReplay, Stream frames)
        {
            var options = new ZipWriterOptions(CompressionType.Deflate)
            {
                LeaveStreamOpen = true
            };

            using var writer = new ZipWriter(output, options);
            writer.Write(LEGACY_REPLAY_FILENAME, legacyReplay);
            writer.Write(EzHighPrecisionReplayFrames.FILENAME, frames);
        }
    }
}
