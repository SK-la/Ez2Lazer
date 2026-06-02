// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Localization;
using osu.Game.Screens;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// Shared launch path from BMS song-select screens into <see cref="BMSPlayerLoader"/>.
    /// </summary>
    public static class BmsSongSelectPlayHelper
    {
        public static bool TryLaunchFromChart(
            OsuScreen screen,
            string chartPath,
            BMSChartCache? chartCache,
            BeatmapInfo? beatmapInfo,
            AudioManager audioManager,
            IRenderer renderer,
            MusicController? musicController,
            INotificationOverlay? notifications)
        {
            if (string.IsNullOrEmpty(chartPath))
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = BmsStrings.SONG_SELECT_SOURCE_FILE_NOT_FOUND,
                });
                return false;
            }

            try
            {
                BeatmapInfo? info = beatmapInfo?.Detach();
                var workingBeatmap = new BMSWorkingBeatmap(chartPath, audioManager, renderer, chartCache, info);

                Logger.Log(
                    $"[BMS] StartGame chart resolve: title={chartCache?.Title}, file={chartCache?.FileName}, path={chartPath}",
                    LoggingTarget.Runtime, LogLevel.Debug);

                screen.Beatmap.Value = workingBeatmap;
                musicController?.Stop();
                screen.Push(new BMSPlayerLoader(workingBeatmap));
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to launch BMS gameplay");
                notifications?.Post(new SimpleNotification { Text = BmsStrings.SongSelect_LoadBeatmapFailed(ex.Message) });
                return false;
            }
        }

        public static bool TryLaunchFromBeatmapInfo(
            OsuScreen screen,
            BeatmapInfo beatmapInfo,
            BMSBeatmapManager beatmapManager,
            AudioManager audioManager,
            IRenderer renderer,
            MusicController? musicController,
            INotificationOverlay? notifications)
        {
            if (!TryResolveSource(beatmapManager, beatmapInfo, out string chartPath, out BMSChartCache? chartCache))
            {
                notifications?.Post(new SimpleNotification { Text = BmsStrings.SONG_SELECT_SOURCE_FILE_NOT_FOUND });
                return false;
            }

            return TryLaunchFromChart(screen, chartPath, chartCache, beatmapInfo, audioManager, renderer, musicController, notifications);
        }

        public static bool TryResolveSource(BMSBeatmapManager beatmapManager, BeatmapInfo info, out string chartPath, out BMSChartCache? chartCache)
        {
            chartPath = string.Empty;
            chartCache = null;

            if (beatmapManager.TryGetSourceReference(info.ID, out BMSSourceReference byId))
                chartPath = byId.ChartPath;
            else if (!string.IsNullOrEmpty(info.MD5Hash) && beatmapManager.TryGetSourceReferenceByHash(info.MD5Hash, out BMSSourceReference byHash))
                chartPath = byHash.ChartPath;
            else
                return false;

            if (beatmapManager.LibraryCache != null)
            {
                string resolvedPath = chartPath;
                chartCache = beatmapManager.LibraryCache.Songs
                                           .SelectMany(s => s.Charts)
                                           .FirstOrDefault(c => string.Equals(c.Md5Hash, info.MD5Hash, StringComparison.OrdinalIgnoreCase)
                                                                || string.Equals(c.FullPath, resolvedPath, StringComparison.OrdinalIgnoreCase));
            }

            return !string.IsNullOrEmpty(chartPath);
        }
    }
}
