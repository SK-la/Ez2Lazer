// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using osu.Framework.Logging;
using osu.Game.Beatmaps;

namespace osu.Game.LAsEzExtensions.Analysis
{
    internal static class XxySrDebugJson
    {
        // Performance/debugging note:
        // This logging can be very spammy during song select scrolling and may impact frame times.
        // Keep disabled unless actively investigating xxy_SR correctness.
        private const bool enabled = false;

        public static string FormatAbnormalSr(BeatmapInfo beatmap, string eventType, double? star = null, double? xxySr = null)
        {
            var buffer = new ArrayBufferWriter<byte>(256);

            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();

                // Fixed property order for stable diffs.
                writer.WriteString("event", eventType);

                writer.WriteString("beatmap_id", beatmap.ID.ToString());
                writer.WriteString("beatmap_hash", beatmap.Hash);

                // In this codebase OnlineID is an int; treat non-positive values as "no online id".
                if (beatmap.OnlineID > 0)
                    writer.WriteNumber("beatmap_online_id", beatmap.OnlineID);
                else
                    writer.WriteNull("beatmap_online_id");

                writer.WriteString("difficulty_name", beatmap.DifficultyName);

                if (beatmap.BeatmapSet != null)
                {
                    writer.WriteString("beatmapset_id", beatmap.BeatmapSet.ID.ToString());

                    if (beatmap.BeatmapSet.OnlineID > 0)
                        writer.WriteNumber("beatmapset_online_id", beatmap.BeatmapSet.OnlineID);
                    else
                        writer.WriteNull("beatmapset_online_id");
                }
                else
                {
                    writer.WriteNull("beatmapset_id");
                    writer.WriteNull("beatmapset_online_id");
                }

                writer.WriteNumber("ruleset_online_id", 3);
                writer.WriteStartArray("mods");
                writer.WriteEndArray();

                if (star.HasValue)
                    writer.WriteNumber("star", star.Value);
                else
                    writer.WriteNull("star");

                if (xxySr.HasValue)
                    writer.WriteNumber("xxy_sr", xxySr.Value);
                else
                    writer.WriteNull("xxy_sr");

                if (star.HasValue && xxySr.HasValue)
                {
                    double absDiff = Math.Abs(star.Value - xxySr.Value);
                    writer.WriteNumber("abs_diff", absDiff);
                }

                writer.WriteEndObject();
                writer.Flush();
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        public static void LogAbnormalSr(BeatmapInfo? beatmap, double? star, double? xxySr, Guid beatmapId, ref Guid? loggedAbnormalId)
        {
            if (!enabled)
                return;

            if (beatmap == null || star == null)
                return;

            if (loggedAbnormalId == beatmapId)
                return;

            loggedAbnormalId = beatmapId;

            if (xxySr == null)
            {
                Logger.Log(
                    FormatAbnormalSr(beatmap, "xxySR_null", null, xxySr),
                    EzAnalysisPersistentStore.LOGGER_NAME,
                    LogLevel.Error);
            }
            else if (Math.Abs(star.Value - xxySr.Value) > 3)
            {
                Logger.Log(
                    FormatAbnormalSr(beatmap, "xxySR_large_diff", star, xxySr),
                    EzAnalysisPersistentStore.LOGGER_NAME,
                    LogLevel.Error);
            }
        }
    }
}
