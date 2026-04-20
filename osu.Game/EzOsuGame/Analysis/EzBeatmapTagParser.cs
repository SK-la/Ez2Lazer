// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Extensions;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 轻量读取谱面中的视频与故事板标记。
    /// 只扫描事件文本，不触发完整 storyboard 对象解码。
    /// </summary>
    internal static class EzBeatmapTagParser
    {
        public static EzBeatmapTagSummary Parse(BeatmapManager beatmapManager, BeatmapInfo beatmapInfo)
            => Parse(beatmapManager.GetWorkingBeatmap(beatmapInfo));

        public static EzBeatmapTagSummary Parse(WorkingBeatmap? workingBeatmap)
        {
            if (workingBeatmap == null)
                return EzBeatmapTagSummary.EMPTY;

            bool hasVideo = false;
            bool hasStoryboard = false;

            scanEventLines(workingBeatmap.Beatmap?.UnhandledEventLines, ref hasVideo, ref hasStoryboard);

            if (!hasVideo || !hasStoryboard)
                scanMainStoryboardFile(workingBeatmap, ref hasVideo, ref hasStoryboard);

            return new EzBeatmapTagSummary(hasVideo, hasStoryboard);
        }

        private static void scanMainStoryboardFile(WorkingBeatmap workingBeatmap, ref bool hasVideo, ref bool hasStoryboard)
        {
            BeatmapSetInfo? beatmapSet = workingBeatmap.BeatmapInfo.BeatmapSet;

            if (beatmapSet == null)
                return;

            string storyboardFilename = getMainStoryboardFilename(beatmapSet.Metadata);

            if (beatmapSet.GetFile(storyboardFilename)?.Filename is not string resolvedFilename)
                return;

            string? storagePath = beatmapSet.GetPathForFile(resolvedFilename);

            if (string.IsNullOrWhiteSpace(storagePath))
                return;

            using Stream? stream = workingBeatmap.GetStream(storagePath);

            if (stream == null)
                return;

            using var reader = new StreamReader(stream);
            bool inEventsSection = false;

            while (!reader.EndOfStream && (!hasVideo || !hasStoryboard))
            {
                string? line = reader.ReadLine();

                if (line == null)
                    break;

                string trimmed = line.Trim();

                if (trimmed.Length == 0)
                    continue;

                if (trimmed[0] == '[')
                {
                    inEventsSection = trimmed.Equals("[Events]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inEventsSection)
                    continue;

                scanSingleEventLine(trimmed, ref hasVideo, ref hasStoryboard);
            }
        }

        private static void scanEventLines(IEnumerable<string>? lines, ref bool hasVideo, ref bool hasStoryboard)
        {
            if (lines == null)
                return;

            foreach (string raw in lines)
            {
                scanSingleEventLine(raw, ref hasVideo, ref hasStoryboard);

                if (hasVideo && hasStoryboard)
                    return;
            }
        }

        private static void scanSingleEventLine(string rawLine, ref bool hasVideo, ref bool hasStoryboard)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                return;

            string trimmed = rawLine.Trim();

            if (trimmed.StartsWith("//", StringComparison.Ordinal))
                return;

            int commentIndex = trimmed.IndexOf("//", StringComparison.Ordinal);

            if (commentIndex >= 0)
                trimmed = trimmed[..commentIndex].TrimEnd();

            if (trimmed.Length == 0)
                return;

            int depth = 0;

            while (depth < trimmed.Length && (trimmed[depth] == ' ' || trimmed[depth] == '_'))
                depth++;

            if (depth > 0)
                return;

            string eventType = trimmed.Split(',')[0].Trim();

            if (!hasVideo && matchesEventType(eventType, LegacyEventType.Video, "Video"))
                hasVideo = true;

            if (!hasStoryboard && (matchesEventType(eventType, LegacyEventType.Sprite, "Sprite") || matchesEventType(eventType, LegacyEventType.Animation, "Animation")))
                hasStoryboard = true;
        }

        private static bool matchesEventType(string value, LegacyEventType eventType, string eventName)
            => value.Equals(eventName, StringComparison.OrdinalIgnoreCase) || value == ((int)eventType).ToString();

        private static string getMainStoryboardFilename(IBeatmapMetadataInfo metadata)
        {
            string baseFilename = (metadata.Artist.Length > 0 ? metadata.Artist + @" - " + metadata.Title : Path.GetFileNameWithoutExtension(metadata.AudioFile))
                                  + (metadata.Author.Username.Length > 0 ? @" (" + metadata.Author.Username + @")" : string.Empty)
                                  + @".osb";

            return baseFilename.GetValidFilename();
        }
    }
}