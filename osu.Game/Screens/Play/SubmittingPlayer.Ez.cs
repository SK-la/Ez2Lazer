// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Online;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play
{
    public abstract partial class SubmittingPlayer
    {
        private void queueEzManiaMetadataSubmission(Score score, long onlineScoreId)
        {
            if (!score.ScoreInfo.TryGetManiaGameplayModes(out int hitMode, out int healthMode))
                return;

            var preset = GlobalConfigStore.EzConfig.Get<ServerPreset>(Ez2Setting.ServerPreset);

            if (preset == ServerPreset.Official)
                return;

            int beatmapId = score.ScoreInfo.BeatmapInfo?.OnlineID ?? 0;

            if (beatmapId <= 0)
                return;

            var request = new SubmitEzManiaScoreMetadataRequest(hitMode, healthMode, onlineScoreId, beatmapId);

            request.Success += () => Logger.Log(
                $"[EzMania] Uploaded gameplay mode metadata (hit={hitMode}, hp={healthMode}) for score {onlineScoreId}",
                Ez2ConfigManager.LOGGER_NAME,
                LogLevel.Debug);

            request.Failure += e => Logger.Log(
                $"[EzMania] Failed to upload gameplay mode metadata for score {onlineScoreId}: {e.Message}",
                Ez2ConfigManager.LOGGER_NAME,
                LogLevel.Important);

            api.Queue(request);
        }
    }
}
