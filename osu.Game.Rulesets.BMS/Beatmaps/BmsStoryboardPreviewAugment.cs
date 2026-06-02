// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.IO;
using osu.Game.Storyboards;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Merges BMS non-note BGM events into a <see cref="Storyboard"/> so the standard
    /// osu! preview / storyboard sample scheduling can see them.
    ///
    /// Called only from inside the BMS ruleset assembly (e.g. <see cref="BMSWorkingBeatmap.GetStoryboard"/>).
    /// </summary>
    public static class BmsStoryboardPreviewAugment
    {
        public const string SAMPLE_LAYER_NAME = "BMSBackgroundSamples";

        /// <summary>
        /// Re-decode a BMS chart file from disk and augment <paramref name="storyboard"/> with its BGM events.
        /// Useful for callers that hold a chart path but no parsed <see cref="BMSBeatmap"/> instance.
        /// </summary>
        public static void Augment(Storyboard storyboard, string chartFilePath)
        {
            if (!File.Exists(chartFilePath))
                return;

            using var stream = File.OpenRead(chartFilePath);
            using var reader = new LineBufferedReader(stream);
            var decoder = new BMSBeatmapDecoder();

            if (decoder.Decode(reader) is not BMSBeatmap bms || bms.BackgroundSoundEvents.Count == 0)
                return;

            Augment(storyboard, bms.BackgroundSoundEvents);
        }

        /// <summary>
        /// Augment <paramref name="storyboard"/> with the supplied background sound events.
        /// </summary>
        public static void Augment(Storyboard storyboard, IEnumerable<BmsBackgroundSoundEvent> backgroundSoundEvents)
        {
            var sampleLayer = storyboard.GetLayer(SAMPLE_LAYER_NAME);

            foreach (var e in backgroundSoundEvents)
            {
                if (string.IsNullOrEmpty(e.Filename))
                    continue;

                sampleLayer.Add(new StoryboardSampleInfo(StoryboardElementSource.Beatmap, e.Filename.Replace('\\', '/'), e.Time, 100));
            }
        }
    }
}
