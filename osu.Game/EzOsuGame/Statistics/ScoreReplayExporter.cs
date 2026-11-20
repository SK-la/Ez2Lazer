// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.EzOsuGame.Statistics
{
    internal class ScoreReplayExporter
    {
        private readonly Storage exportStorage;
        private readonly Storage userFileStorage;
        private readonly ScoreManager scoreManager;
        private readonly BeatmapManager beatmapManager;

        public Action<Notification>? PostNotification { get; set; }

        public ScoreReplayExporter(Storage exportStorage, Storage userFileStorage, ScoreManager scoreManager, BeatmapManager beatmapManager)
        {
            this.exportStorage = exportStorage;
            this.userFileStorage = userFileStorage;
            this.scoreManager = scoreManager;
            this.beatmapManager = beatmapManager;
        }

        public void Export(ScoreInfo scoreInfo)
        {
            Score? score = scoreManager.GetScore(scoreInfo);

            if (score?.Replay == null || score.ScoreInfo.BeatmapInfo == null)
            {
                PostNotification?.Invoke(new SimpleErrorNotification { Text = "Failed to load replay for export." });
                return;
            }

            const string extension = EzReplayExportEncoder.EXTENSION;
            string itemFilename = $"{score.ScoreInfo.GetDisplayString()} ({score.ScoreInfo.Date.LocalDateTime:yyyy-MM-dd_HH-mm})".GetValidFilename();

            if (itemFilename.Length > LegacyExporter<ScoreInfo>.MAX_FILENAME_LENGTH - extension.Length)
                itemFilename = itemFilename.Remove(LegacyExporter<ScoreInfo>.MAX_FILENAME_LENGTH - extension.Length);

            IEnumerable<string> existingExports = exportStorage
                                                  .GetFiles(string.Empty, $"{itemFilename}*{extension}")
                                                  .Concat(exportStorage.GetDirectories(string.Empty));

            string filename = NamingUtils.GetNextBestFilename(existingExports, $"{itemFilename}{extension}");

            ProgressNotification notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = NotificationsStrings.FileExportOngoing(itemFilename),
            };

            PostNotification?.Invoke(notification);

            try
            {
                var workingBeatmap = beatmapManager.GetWorkingBeatmap(score.ScoreInfo.BeatmapInfo);
                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);

                using var output = exportStorage.CreateFileSafely(filename);

                var framesFile = score.ScoreInfo.Files.FirstOrDefault(f => f.Filename.EndsWith(EzHighPrecisionReplayFrames.EXTENSION, StringComparison.OrdinalIgnoreCase));
                using var highPrecisionFrames = framesFile == null ? null : userFileStorage.GetStream(framesFile.File.GetStoragePath());
                EzReplayExportEncoder.EncodeEzOsr(score, playableBeatmap, output, highPrecisionFrames);
            }
            catch
            {
                notification.State = ProgressNotificationState.Cancelled;
                exportStorage.Delete(filename);
                throw;
            }

            notification.CompletionText = NotificationsStrings.FileExportFinished(itemFilename);
            notification.CompletionClickAction = () => exportStorage.PresentFileExternally(filename);
            notification.State = ProgressNotificationState.Completed;
        }
    }
}
