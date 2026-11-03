// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Replays;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Replays.Types;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Standalone high-precision replay file (<c>replay.ezframes</c>) used as the internal source of truth
    /// for Session / Race / Timeline. User export still produces Round <c>.osr</c> on demand.
    /// </summary>
    public static class EzHighPrecisionReplayFrames
    {
        public const string FILENAME = @"replay.ezframes";
        public const string EXTENSION = @".ezframes";
        public const int VERSION = 1;

        private static readonly byte[] magic = Encoding.ASCII.GetBytes(@"EZFR");

        public static void Write(Stream stream, Score score, IBeatmap? beatmap)
        {
            byte[] payload = createPayload(score, beatmap);

            using var bw = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            bw.Write(magic);
            bw.Write(VERSION);
            bw.Write(payload.Length);
            bw.Write(payload);
        }

        public static bool TryRead(Stream stream, out List<LegacyReplayFrame> frames)
        {
            frames = new List<LegacyReplayFrame>();

            try
            {
                using var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

                byte[] header = br.ReadBytes(magic.Length);
                if (header.Length != magic.Length || !headerMatches(header))
                    return false;

                int version = br.ReadInt32();
                if (version != VERSION)
                    return false;

                int len = br.ReadInt32();
                if (len <= 0)
                    return false;

                byte[] payload = br.ReadBytes(len);
                if (payload.Length != len)
                    return false;

                frames = parsePayload(payload);
                return true;
            }
            catch
            {
                frames = new List<LegacyReplayFrame>();
                return false;
            }
        }

        /// <summary>
        /// Convert absolute-time legacy frames into ruleset frames (same contract as <c>LegacyScoreDecoder</c>).
        /// </summary>
        public static void PopulateReplay(Replay replay, IEnumerable<LegacyReplayFrame> legacyFrames, Ruleset ruleset, IBeatmap beatmap)
        {
            ReplayFrame? currentFrame = null;

            foreach (var legacyFrame in legacyFrames)
            {
                if (currentFrame != null && legacyFrame.Time < currentFrame.Time)
                    continue;

                replay.Frames.Add(currentFrame = convertFrame(legacyFrame, currentFrame, ruleset, beatmap));
            }
        }

        private static bool headerMatches(ReadOnlySpan<byte> header)
        {
            for (int i = 0; i < magic.Length; i++)
            {
                if (header[i] != magic[i])
                    return false;
            }

            return true;
        }

        private static byte[] createPayload(Score score, IBeatmap? beatmap)
        {
            double offset = beatmap?.BeatmapVersion < 5 ? -LegacyBeatmapDecoder.EARLY_VERSION_TIMING_OFFSET : 0;

            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                var scoreFrames = score.Replay?.Frames;
                int count = scoreFrames?.Count ?? 0;
                bw.Write(count);

                if (scoreFrames != null)
                {
                    foreach (var f in scoreFrames)
                    {
                        var legacy = toLegacy(f, beatmap);
                        bw.Write(legacy.Time + offset);
                        bw.Write(legacy.MouseX ?? 0);
                        bw.Write(legacy.MouseY ?? 0);
                        bw.Write((int)legacy.ButtonState);
                    }
                }
            }

            return ms.ToArray();
        }

        private static List<LegacyReplayFrame> parsePayload(byte[] payload)
        {
            var result = new List<LegacyReplayFrame>();

            using var ms = new MemoryStream(payload);
            using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

            int count = br.ReadInt32();
            if (count < 0 || count > 10_000_000)
                return result;

            result.Capacity = count;

            for (int i = 0; i < count; i++)
            {
                double time = br.ReadDouble();
                float mouseX = br.ReadSingle();
                float mouseY = br.ReadSingle();
                var buttons = (ReplayButtonState)br.ReadInt32();
                result.Add(new LegacyReplayFrame(time, mouseX, mouseY, buttons));
            }

            return result;
        }

        private static ReplayFrame convertFrame(LegacyReplayFrame currentFrame, ReplayFrame? lastFrame, Ruleset ruleset, IBeatmap beatmap)
        {
            var convertible = ruleset.CreateConvertibleReplayFrame();
            if (convertible == null)
                throw new InvalidOperationException($"Legacy replay cannot be converted for the ruleset: {ruleset.Description}");

            convertible.FromLegacy(currentFrame, beatmap, lastFrame);

            var frame = (ReplayFrame)convertible;
            frame.Time = currentFrame.Time;
            return frame;
        }

        private static LegacyReplayFrame toLegacy(ReplayFrame replayFrame, IBeatmap? beatmap)
        {
            switch (replayFrame)
            {
                case LegacyReplayFrame legacyFrame:
                    return legacyFrame;

                case IConvertibleReplayFrame convertibleFrame:
                    Debug.Assert(beatmap != null);
                    return convertibleFrame.ToLegacy(beatmap);

                default:
                    throw new ArgumentException(@"Frame could not be converted to legacy frames", nameof(replayFrame));
            }
        }
    }
}
