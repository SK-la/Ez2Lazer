// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Audio;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Prepares BMS keysounds for externally hosted library beatmaps (Realm index + on-disk folder).
    /// </summary>
    public static class BmsBeatmapAudioResolver
    {
        public static BmsKeysoundManager? TryPrepare(IWorkingBeatmap workingBeatmap, AudioManager audioManager, bool preloadKeysounds)
        {
            BeatmapInfo? info = workingBeatmap.BeatmapInfo as BeatmapInfo;

            if (info?.BeatmapSet == null || !info.BeatmapSet.IsExternallyHosted)
                return null;

            if (!string.Equals(info.Ruleset.ShortName, "bms", StringComparison.Ordinal))
                return null;

            if (!BMSExternalPath.TryGetContentRoot(info.BeatmapSet, out string folder))
                return null;

            var manager = new BmsKeysoundManager(audioManager, folder);

            if (!preloadKeysounds)
                return manager;

            IBeatmap? beatmap = workingBeatmap.Beatmap;

            if (beatmap == null)
            {
                manager.Prepare(Array.Empty<HitObject>());
                return manager;
            }

            if (beatmap is BMSBeatmap bmsBeatmap)
            {
                manager.Prepare(bmsBeatmap.HitObjects, bmsBeatmap.BackgroundSoundEvents);
                return manager;
            }

            manager.Prepare(ManiaConvertedWorkingBeatmap.ConvertToManiaBeatmap(beatmap).HitObjects);
            return manager;
        }
    }
}
