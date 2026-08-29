// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Resolves average absolute hit offset from stored or replay-generated <see cref="ScoreInfo.HitEvents"/>.
    /// </summary>
    public static class EzLocalProfileHitEventResolver
    {
        public static async Task<ScoreInfo?> LoadScoreWithHitEventsAsync(
            Guid scoreId,
            RealmAccess realm,
            ScoreManager scoreManager,
            BeatmapManager beatmapManager,
            IEzReplaySession replaySession,
            CancellationToken cancellationToken = default)
        {
            var detached = realm.Run(r => r.Find<ScoreInfo>(scoreId)?.DeepClone());
            if (detached == null)
                return null;

            await ensureHitEventsAsync(detached, scoreManager, beatmapManager, replaySession, cancellationToken).ConfigureAwait(false);
            return detached;
        }

        public static async Task<double?> ResolveAvgAbsOffsetMsAsync(
            Guid scoreId,
            RealmAccess realm,
            ScoreManager scoreManager,
            BeatmapManager beatmapManager,
            IEzReplaySession replaySession,
            CancellationToken cancellationToken = default)
        {
            var detached = await LoadScoreWithHitEventsAsync(scoreId, realm, scoreManager, beatmapManager, replaySession, cancellationToken).ConfigureAwait(false);
            return detached == null ? null : EzLocalProfileDrillScoreRow.ComputeAvgAbsOffsetMs(detached);
        }

        public static double? ResolveAvgAbsOffsetMs(
            ScoreInfo score,
            RealmAccess realm,
            ScoreManager scoreManager,
            BeatmapManager beatmapManager,
            IEzReplaySession replaySession,
            CancellationToken cancellationToken = default)
        {
            if (score.HitEvents.Count > 0)
                return EzLocalProfileDrillScoreRow.ComputeAvgAbsOffsetMs(score);

            return ResolveAvgAbsOffsetMsAsync(score.ID, realm, scoreManager, beatmapManager, replaySession, cancellationToken)
                   .GetAwaiter()
                   .GetResult();
        }

        private static async Task<bool> ensureHitEventsAsync(
            ScoreInfo detached,
            ScoreManager scoreManager,
            BeatmapManager beatmapManager,
            IEzReplaySession replaySession,
            CancellationToken cancellationToken)
        {
            if (detached.HitEvents.Count > 0)
                return true;

            var databasedScore = scoreManager.GetScore(detached);
            if (databasedScore == null)
                return false;

            var workingBeatmap = beatmapManager.GetWorkingBeatmap(detached.BeatmapInfo);
            var playable = workingBeatmap.GetPlayableBeatmap(detached.Ruleset, detached.Mods);
            var generated = await replaySession.RunHitEventsAsync(databasedScore, playable, ReplayRunPurpose.ForStored, cancellationToken).ConfigureAwait(false);

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (generated == null)
                return false;

            detached.HitEvents = generated;
            return true;
        }
    }
}
