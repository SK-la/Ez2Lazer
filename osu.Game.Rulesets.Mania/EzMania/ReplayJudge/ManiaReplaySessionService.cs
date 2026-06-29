// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// Mania 统一 replay 入口：负责 async、环境解析与共享 cache。
    /// Panel / Graph / Race 通过此服务获取 replay 判定结果，禁止自建解析或缓存。
    /// </summary>
    public sealed class ManiaReplaySessionService : EzReplaySession
    {
        protected override async Task<Score> RunScoreAsyncFunc(Score score, IBeatmap beatmap, IGameplayEnvironment? environment, CancellationToken cancellationToken)
        {
            var ruleset = new ManiaRuleset();

            environment ??= GlobalConfigStore.EzConfig.ResolveForReplay(score.ScoreInfo, ReplayRunPurpose.ForStored);

            // Clone to isolate Session mutations from the caller's Score/ScoreInfo reference.
            var clone = score.DeepClone();
            var result = await ruleset.RunReplayAsync(clone, beatmap, environment, cancellationToken).ConfigureAwait(false);

            // Copy back HitEvents and Statistics so caller gets fresh results on their object.
            score.ScoreInfo.HitEvents = result.ScoreInfo.HitEvents;
            score.ScoreInfo.Statistics.Clear();
            foreach (var kvp in result.ScoreInfo.Statistics)
                score.ScoreInfo.Statistics[kvp.Key] = kvp.Value;

            return score;
        }

        protected override Task<EzScoreTimeline> RunTimelineAsyncFunc(Score score, IBeatmap beatmap, IGameplayEnvironment? environment, CancellationToken cancellationToken)
        {
            environment ??= GlobalConfigStore.EzConfig.ResolveForReplay(score.ScoreInfo, ReplayRunPurpose.ForStored);

            // ManiaReplaySession.RunTimeline is synchronous — execute directly to avoid nesting Task.Run
            // which can cause threadpool starvation when the caller is already on a pool thread (via ensureTimelinesLoaded).
            return Task.FromResult(ManiaReplaySession.RunTimeline(score, beatmap, environment, cancellationToken));
        }

        protected override async Task<ReplayRunResult> RunCombinedAsyncFunc(ReplayRunRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var ruleset = new ManiaRuleset();
                var resolvedEnv = request.Environment ?? GlobalConfigStore.EzConfig.ResolveForReplay(request.Score.ScoreInfo, request.Purpose);

                // Clone only for RunReplayAsync to isolate PopulateScore mutations.
                var clone = request.Score.DeepClone();
                var sessionScore = await ruleset.RunReplayAsync(clone, request.Beatmap, resolvedEnv, cancellationToken).ConfigureAwait(false);

                // RunTimeline reads only Replay.Frames, safe on original score.
                var timeline = ManiaReplaySession.RunTimeline(request.Score, request.Beatmap, resolvedEnv, cancellationToken);

                // Copy back HitEvents so caller sees fresh results on their object.
                request.Score.ScoreInfo.HitEvents = sessionScore.ScoreInfo.HitEvents;

                return new ReplayRunResult(sessionScore, timeline, hitCache: false, isValidReplay: true);
            }
            catch (OperationCanceledException)
            {
                return ReplayRunResult.Cancelled();
            }
            catch
            {
                return ReplayRunResult.InvalidReplay(request.Score);
            }
        }
    }
}



