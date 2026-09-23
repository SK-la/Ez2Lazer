// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    public abstract partial class EzReplaySession : IEzReplaySession
    {
        private readonly ConcurrentDictionary<string, Lazy<Task<(Score Score, EzScoreTimeline Timeline)>>> sessionRunCache =
            new ConcurrentDictionary<string, Lazy<Task<(Score Score, EzScoreTimeline Timeline)>>>();

        protected abstract (Score Score, EzScoreTimeline Timeline) RunWithTimeline(
            Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken);

        /// <summary>
        /// 注入 beatmap 来源，供需要「仿真副本」的规则集在 <see cref="RunWithTimeline"/> 里取独立实例。
        /// 不需要隔离的规则集保持无操作。
        /// </summary>
        public virtual void AttachBeatmaps(IWorkingBeatmapCache beatmapCache)
        {
        }

        public async Task<Score> RunAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default)
        {
            var (resultScore, _, _) = await getOrRunSession(score, beatmap, purpose, cancellationToken).ConfigureAwait(false);
            return resultScore;
        }

        public async Task<EzScoreTimeline> RunTimelineAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default)
        {
            var (_, timeline, _) = await getOrRunSession(score, beatmap, purpose, cancellationToken).ConfigureAwait(false);
            return timeline;
        }

        public async Task<EzScoreTimeline> RunTimelineDirectAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default)
        {
            var resolvedEnv = ResolveEnvironment(score, purpose);
            var (_, timeline) = await runSessionDirect(score, beatmap, resolvedEnv, cancellationToken).ConfigureAwait(false);
            return timeline;
        }

        public EzScoreTimeline RunTimelineDirect(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolvedEnv = ResolveEnvironment(score, purpose);
            var (_, timeline) = RunWithTimeline(score, beatmap, resolvedEnv, cancellationToken);
            return timeline;
        }

        public async Task<ReplayRunResult> RunRequestAsync(ReplayRunRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var (score, timeline, environment) = await getOrRunSession(request, cancellationToken).ConfigureAwait(false);

                return new ReplayRunResult(score, timeline, environment, hitCache: false, isValidReplay: true);
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

        public async Task<List<HitEvent>> RunHitEventsAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose = ReplayRunPurpose.ForLive, CancellationToken cancellationToken = default)
        {
            var (resultScore, _, _) = await getOrRunSession(score, beatmap, purpose, cancellationToken).ConfigureAwait(false);
            return resultScore.ScoreInfo.HitEvents.ToList();
        }

        private async Task<(Score Score, EzScoreTimeline Timeline, GameplayEnvironment Environment)> getOrRunSession(
            ReplayRunRequest request,
            CancellationToken cancellationToken)
        {
            var resolvedEnv = ResolveEnvironment(request);

            var result = await GetOrCreate(
                sessionRunCache,
                BuildCacheKey($"session:{request.Purpose}", request.Score, request.Beatmap, resolvedEnv),
                () => runSessionDirect(request.Score, request.Beatmap, resolvedEnv, cancellationToken)).ConfigureAwait(false);

            return (result.Score, result.Timeline, resolvedEnv);
        }

        private async Task<(Score Score, EzScoreTimeline Timeline, GameplayEnvironment Environment)> getOrRunSession(
            Score score,
            IBeatmap beatmap,
            ReplayRunPurpose purpose,
            CancellationToken cancellationToken)
            => await getOrRunSession(new ReplayRunRequest(score, beatmap, purpose), cancellationToken).ConfigureAwait(false);

        private Task<(Score Score, EzScoreTimeline Timeline)> runSessionDirect(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return RunWithTimeline(score, beatmap, environment, cancellationToken);
            }, cancellationToken);
        }

        protected static GameplayEnvironment ResolveEnvironment(ReplayRunRequest request)
        {
            if (request.Purpose == ReplayRunPurpose.ForLive)
            {
                var environment = GlobalConfigStore.EzConfig.ResolveEnvironment(
                    request.Purpose,
                    request.Score.ScoreInfo,
                    ignoreOffset: !request.IncludeGlobalManiaOffset);

                return request.IncludeGlobalManiaOffset
                    ? environment with { ApplyInputOffsetViaReplayFrameShift = true }
                    : environment;
            }

            return GlobalConfigStore.EzConfig.ResolveEnvironment(request.Purpose, request.Score.ScoreInfo);
        }

        protected static GameplayEnvironment ResolveEnvironment(Score score, ReplayRunPurpose purpose)
        {
            if (purpose == ReplayRunPurpose.ForLive)
                return GlobalConfigStore.EzConfig.ResolveEnvironment(purpose, score.ScoreInfo, ignoreOffset: true);

            return GlobalConfigStore.EzConfig.ResolveEnvironment(purpose, score.ScoreInfo);
        }

        protected static Task<T> GetOrCreate<T>(ConcurrentDictionary<string, Lazy<Task<T>>> cache, string cacheKey, Func<Task<T>> factory)
        {
            var lazy = cache.GetOrAdd(cacheKey, _ => new Lazy<Task<T>>(factory));
            return lazy.Value;
        }

        protected static string BuildCacheKey(string purpose, Score score, IBeatmap beatmap, IGameplayEnvironment environment)
        {
            string scoreKey = $"hash:{score.ScoreInfo.Hash}|id:{score.ScoreInfo.ID}";
            string beatmapKey = $"hash:{beatmap.BeatmapInfo.Hash}|id:{beatmap.BeatmapInfo.ID}";
            string bmsPoorKey = environment.BmsPoorHitResultEnable.ToString();
            string envKey = $"hm:{(int)environment.ManiaHitMode}|health:{(int)environment.ManiaHealthMode}|judge:{(int)environment.JudgePrecedence}|offset:{environment.OffsetPlusMania:F3}|bmsPoor:{bmsPoorKey}|frameShift:{environment.ApplyInputOffsetViaReplayFrameShift}";

            string raw = $"{purpose}|{scoreKey}|{beatmapKey}|{envKey}|rule:{score.ScoreInfo.Ruleset.OnlineID}";
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        }
    }
}
