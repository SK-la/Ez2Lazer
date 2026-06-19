// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// Mania 统一 replay 入口：负责 async、环境解析与共享 cache。
    /// Panel / Graph / Race 通过此服务获取 replay 判定结果，禁止自建解析或缓存。
    /// </summary>
    public sealed class ManiaReplaySessionService : IEzReplaySession
    {
        private readonly ConcurrentDictionary<string, Lazy<Task<Score>>> scoreCache = new ConcurrentDictionary<string, Lazy<Task<Score>>>();
        private readonly ConcurrentDictionary<string, Lazy<Task<EzScoreTimeline>>> timelineCache = new ConcurrentDictionary<string, Lazy<Task<EzScoreTimeline>>>();
        private readonly ConcurrentDictionary<string, Lazy<Task<ReplayRunResult>>> combinedCache = new ConcurrentDictionary<string, Lazy<Task<ReplayRunResult>>>();

        public Task<Score> RunAsync(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
            => getOrCreate(scoreCache, buildScoreCacheKey(score, beatmap, environment), () => runScoreAsync(score, beatmap, environment, cancellationToken));

        public Task<EzScoreTimeline> RunTimelineAsync(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
            => getOrCreate(timelineCache, buildTimelineCacheKey(score, beatmap, environment), () => runTimelineAsync(score, beatmap, environment, cancellationToken));

        public Task<ReplayRunResult> RunRequestAsync(ReplayRunRequest request, CancellationToken cancellationToken = default)
            => getOrCreate(combinedCache, buildCombinedCacheKey(request), () => runCombinedAsync(request, cancellationToken));

        private static async Task<Score> runScoreAsync(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken)
        {
            var ruleset = new ManiaRuleset();
            return await ruleset.RunReplayAsync(score, beatmap, environment, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<EzScoreTimeline> runTimelineAsync(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken)
        {
            return await Task.Run(() => ManiaReplaySession.RunTimeline(score, beatmap, environment, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        private static async Task<ReplayRunResult> runCombinedAsync(ReplayRunRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var ruleset = new ManiaRuleset();
                var score = await ruleset.RunReplayAsync(request.Score, request.Beatmap, request.Environment, cancellationToken).ConfigureAwait(false);
                var timeline = ManiaReplaySession.RunTimeline(request.Score, request.Beatmap, request.Environment, cancellationToken);
                return new ReplayRunResult(score, timeline, hitCache: false, isValidReplay: true);
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

        private static Task<T> getOrCreate<T>(ConcurrentDictionary<string, Lazy<Task<T>>> cache, string cacheKey, Func<Task<T>> factory)
        {
            var lazy = cache.GetOrAdd(cacheKey, _ => new Lazy<Task<T>>(factory));
            return lazy.Value;
        }

        private static string buildScoreCacheKey(Score score, IBeatmap beatmap, IGameplayEnvironment environment)
            => buildCacheKey("score", score, beatmap, environment);

        private static string buildTimelineCacheKey(Score score, IBeatmap beatmap, IGameplayEnvironment environment)
            => buildCacheKey("timeline", score, beatmap, environment);

        private static string buildCombinedCacheKey(ReplayRunRequest request)
            => buildCacheKey($"combined:{request.Purpose}", request.Score, request.Beatmap, request.Environment);

        private static string buildCacheKey(string purpose, Score score, IBeatmap beatmap, IGameplayEnvironment environment)
        {
            string scoreKey = $"hash:{score.ScoreInfo.Hash}|id:{score.ScoreInfo.ID}";
            string beatmapKey = $"hash:{beatmap.BeatmapInfo.Hash}|id:{beatmap.BeatmapInfo.ID}";
            string bmsPoorKey = environment is IManiaGameplayEnvironment maniaEnvironment ? maniaEnvironment.BmsPoorHitResultEnable.ToString() : "null";
            string envKey = $"hm:{(int)environment.ManiaHitMode}|health:{(int)environment.ManiaHealthMode}|judge:{(int)environment.JudgePrecedence}|offset:{environment.OffsetPlusMania:F3}|bmsPoor:{bmsPoorKey}";
            string raw = $"{purpose}|{scoreKey}|{beatmapKey}|{envKey}|rule:{score.ScoreInfo.Ruleset.OnlineID}";

            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        }
    }
}



