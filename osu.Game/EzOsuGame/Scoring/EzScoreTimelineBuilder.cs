// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 从本地 replay 构建分数时间线。
    /// Mania / Osu：<see cref="IEzReplaySession.RunTimelineDirectAsync"/>（replay 一遍 SP 快照）。
    /// 架构详见 <see cref="EZ_SR_TL_REGISTRY"/>（仓库 docs/EZ-SR-TL-REGISTRY.md）。
    /// </summary>
    public static class EzScoreTimelineBuilder
    {
        /// <summary>
        /// 创建一个进程内 in-memory 缓存实例，绑定到调用方生命周期。
        /// </summary>
        public static IEzScoreTimelineCache CreateSessionCache() => new EzScoreTimelineCache();

        /// <summary>架构注册表 markdown 文件名（文档用）。</summary>
        public const string EZ_SR_TL_REGISTRY = "docs/EZ-SR-TL-REGISTRY.md";

        public static EzScoreTimeline? TryBuild(ScoreManager scoreManager, BeatmapManager beatmaps, ScoreInfo scoreInfo, IBeatmap? sharedPlayableBeatmap = null,
                                                IEzScoreTimelineCache? cache = null, CancellationToken cancellationToken = default)
            => tryBuild(scoreManager, beatmaps, scoreInfo, sharedPlayableBeatmap, cache ?? NullEzScoreTimelineCache.INSTANCE, cancellationToken);

        private static EzScoreTimeline? tryBuild(ScoreManager scoreManager, BeatmapManager beatmaps, ScoreInfo scoreInfo, IBeatmap? sharedPlayableBeatmap,
                                                 IEzScoreTimelineCache cache, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(scoreManager);
            ArgumentNullException.ThrowIfNull(beatmaps);
            ArgumentNullException.ThrowIfNull(scoreInfo);

            var timelineMode = EzScoreRaceRulesetSupport.GetGhostTimelineMode(scoreInfo.Ruleset);

            if (timelineMode == EzScoreRaceGhostTimelineMode.None)
                return null;

            var playableBeatmap = resolvePlayableBeatmap(beatmaps, scoreInfo, sharedPlayableBeatmap);
            var resolvedEnvironment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForLive, scoreInfo, ignoreOffset: true);

            string? cacheKey = playableBeatmap != null
                ? getCacheKey(scoreInfo, timelineMode, resolvedEnvironment, playableBeatmap)
                : null;

            if (!string.IsNullOrEmpty(cacheKey) && cache.TryGet(cacheKey, out var cached))
                return cached == EzScoreTimeline.EMPTY ? null : cached;

            cancellationToken.ThrowIfCancellationRequested();

            var databasedScore = scoreManager.GetScore(scoreInfo);

            if (databasedScore?.Replay == null || databasedScore.Replay.Frames.Count == 0)
            {
                if (!string.IsNullOrEmpty(cacheKey))
                    cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                return null;
            }

            if (playableBeatmap == null)
                return null;

            if (playableBeatmap.HitObjects.Count == 0)
            {
                if (!string.IsNullOrEmpty(cacheKey))
                    cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                return null;
            }

            EzScoreTimeline? timeline;
            var ruleset = scoreInfo.Ruleset.CreateInstance();

            switch (timelineMode)
            {
                case EzScoreRaceGhostTimelineMode.ManiaSession:
                {
                    var session = ruleset.CreateEzReplaySession();

                    if (session == null)
                    {
                        timeline = null;
                        break;
                    }

                    // 本方法已在 Service 的后台 Task.Run 中串行执行；直接同步运行，
                    // 避免嵌套 Task.Run + GetResult 同步阻塞额外占用线程池线程。
                    timeline = session.RunTimelineDirect(
                        databasedScore,
                        playableBeatmap,
                        ReplayRunPurpose.ForLive,
                        cancellationToken);
                    break;
                }

                case EzScoreRaceGhostTimelineMode.OsuSession:
                {
                    var session = ruleset.CreateEzReplaySession();

                    if (session == null)
                    {
                        timeline = null;
                        break;
                    }

                    timeline = session.RunTimelineDirect(
                        databasedScore,
                        playableBeatmap,
                        ReplayRunPurpose.ForLive,
                        cancellationToken);
                    break;
                }

                default:
                    if (!string.IsNullOrEmpty(cacheKey))
                        cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                    return null;
            }

            if (timeline == null)
            {
                if (!string.IsNullOrEmpty(cacheKey))
                    cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                return null;
            }

            if (!string.IsNullOrEmpty(cacheKey))
                cache.Store(cacheKey, timeline);

            return timeline;
        }

        private static IBeatmap? resolvePlayableBeatmap(BeatmapManager beatmaps, ScoreInfo scoreInfo, IBeatmap? sharedPlayableBeatmap)
        {
            if (sharedPlayableBeatmap != null)
                return sharedPlayableBeatmap;

            var workingBeatmap = beatmaps.GetWorkingBeatmap(scoreInfo.BeatmapInfo);

            if (workingBeatmap is DummyWorkingBeatmap)
                return null;

            return workingBeatmap.GetPlayableBeatmap(scoreInfo.Ruleset, scoreInfo.Mods);
        }

        private static string? getCacheKey(ScoreInfo? scoreInfo, EzScoreRaceGhostTimelineMode timelineMode, IGameplayEnvironment environment, IBeatmap? beatmap)
        {
            string? identity = getScoreIdentity(scoreInfo);

            if (identity == null || scoreInfo == null)
                return null;

            string modFp = getModFingerprint(scoreInfo.Mods);
            string beatmapFp = beatmap != null
                ? $"b:{beatmap.BeatmapInfo.ID}:od{beatmap.BeatmapInfo.Difficulty.OverallDifficulty:F2}:hp{beatmap.BeatmapInfo.Difficulty.DrainRate:F2}"
                : "b:?";

            switch (timelineMode)
            {
                case EzScoreRaceGhostTimelineMode.ManiaSession:
                    return $"{identity}|m|{modFp}|{beatmapFp}|hm{(int)environment.ManiaHitMode}|hh{(int)environment.ManiaHealthMode}|jp{(int)environment.JudgePrecedence}";

                case EzScoreRaceGhostTimelineMode.OsuSession:
                    return $"{identity}|m|{modFp}|{beatmapFp}|jp{(int)environment.JudgePrecedence}";

                default:
                    return null;
            }
        }

        private static string getModFingerprint(System.Collections.Generic.IReadOnlyList<Mod> mods)
            => string.Join(',', mods.OrderBy(m => m.Acronym).Select(m => m.Acronym));

        private static string? getScoreIdentity(ScoreInfo? scoreInfo)
        {
            if (scoreInfo == null)
                return null;

            if (!string.IsNullOrEmpty(scoreInfo.Hash))
                return $"hash:{scoreInfo.Hash}";

            if (scoreInfo.ID != Guid.Empty)
                return $"id:{scoreInfo.ID}";

            return null;
        }
    }
}
