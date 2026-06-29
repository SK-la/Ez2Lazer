// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    public abstract partial class EzReplaySession : IEzReplaySession
    {
        protected readonly ConcurrentDictionary<string, Lazy<Task<Score>>> ScoreCache = new ConcurrentDictionary<string, Lazy<Task<Score>>>();
        protected readonly ConcurrentDictionary<string, Lazy<Task<EzScoreTimeline>>> TimelineCache = new ConcurrentDictionary<string, Lazy<Task<EzScoreTimeline>>>();
        protected readonly ConcurrentDictionary<string, Lazy<Task<ReplayRunResult>>> CombinedCache = new ConcurrentDictionary<string, Lazy<Task<ReplayRunResult>>>();

        protected abstract Task<Score> RunScoreAsyncFunc(Score score, IBeatmap beatmap, IGameplayEnvironment? environment, CancellationToken cancellationToken);

        protected abstract Task<EzScoreTimeline> RunTimelineAsyncFunc(Score score, IBeatmap beatmap, IGameplayEnvironment? environment, CancellationToken cancellationToken);

        protected abstract Task<ReplayRunResult> RunCombinedAsyncFunc(ReplayRunRequest request, CancellationToken cancellationToken);

        public virtual Task<Score> RunAsync(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
            => GetOrCreate(
                ScoreCache,
                BuildCacheKey("score", score, beatmap, environment),
                () => RunScoreAsyncFunc(score, beatmap, environment, cancellationToken));

        public virtual Task<EzScoreTimeline> RunTimelineAsync(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
            => GetOrCreate(
                TimelineCache,
                BuildCacheKey("timeline", score, beatmap, environment),
                () => RunTimelineAsyncFunc(score, beatmap, environment, cancellationToken));

        public virtual Task<ReplayRunResult> RunRequestAsync(ReplayRunRequest request, CancellationToken cancellationToken = default)
            => GetOrCreate(
                CombinedCache,
                BuildCacheKey($"combined:{request.Purpose}", request.Score, request.Beatmap, request.Environment),
                () => RunCombinedAsyncFunc(request, cancellationToken));

        protected static Task<T> GetOrCreate<T>(ConcurrentDictionary<string, Lazy<Task<T>>> cache, string cacheKey, Func<Task<T>> factory)
        {
            var lazy = cache.GetOrAdd(cacheKey, _ => new Lazy<Task<T>>(factory));
            return lazy.Value;
        }

        protected static string BuildCacheKey(string purpose, Score score, IBeatmap beatmap, IGameplayEnvironment? environment)
        {
            string scoreKey = $"hash:{score.ScoreInfo.Hash}|id:{score.ScoreInfo.ID}";
            string beatmapKey = $"hash:{beatmap.BeatmapInfo.Hash}|id:{beatmap.BeatmapInfo.ID}";

            string envKey;

            if (environment == null)
                envKey = "env:null";
            else
            {
                string bmsPoorKey = environment.BmsPoorHitResultEnable.ToString();
                envKey = $"hm:{(int)environment.ManiaHitMode}|health:{(int)environment.ManiaHealthMode}|judge:{(int)environment.JudgePrecedence}|offset:{environment.OffsetPlusMania:F3}|bmsPoor:{bmsPoorKey}";
            }

            string raw = $"{purpose}|{scoreKey}|{beatmapKey}|{envKey}|rule:{score.ScoreInfo.Ruleset.OnlineID}";
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        }
    }
}
