// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    public static class EzScoreTimelineBuilder
    {
        private const string timeline_cache_version = "v1";

        private static readonly ConcurrentDictionary<string, EzScoreTimeline> timeline_cache = new ConcurrentDictionary<string, EzScoreTimeline>();
        private static bool generatorsInitialised;

        public static EzScoreTimeline? TryBuild(ScoreManager scoreManager, BeatmapManager beatmaps, ScoreInfo scoreInfo, CancellationToken cancellationToken = default)
            => tryBuild(scoreManager, beatmaps, scoreInfo, sharedPlayableBeatmap: null, cancellationToken);

        public static EzScoreTimeline? TryBuild(ScoreManager scoreManager, BeatmapManager beatmaps, ScoreInfo scoreInfo, IBeatmap? sharedPlayableBeatmap,
            CancellationToken cancellationToken = default)
            => tryBuild(scoreManager, beatmaps, scoreInfo, sharedPlayableBeatmap, cancellationToken);

        private static EzScoreTimeline? tryBuild(ScoreManager scoreManager, BeatmapManager beatmaps, ScoreInfo scoreInfo, IBeatmap? sharedPlayableBeatmap,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(scoreManager);
            ArgumentNullException.ThrowIfNull(beatmaps);
            ArgumentNullException.ThrowIfNull(scoreInfo);

            ensureGeneratorsInitialised();

            string? cacheKey = getCacheKey(scoreInfo);

            if (!string.IsNullOrEmpty(cacheKey) && timeline_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            if (!hasReplayFile(scoreInfo))
                return null;

            cancellationToken.ThrowIfCancellationRequested();

            var databasedScore = scoreManager.GetScore(scoreInfo);

            if (databasedScore?.Replay == null || databasedScore.Replay.Frames.Count == 0)
                return null;

            var ruleset = scoreInfo.Ruleset.CreateInstance();
            IBeatmap playableBeatmap;

            if (sharedPlayableBeatmap != null)
            {
                playableBeatmap = sharedPlayableBeatmap;
            }
            else
            {
                var workingBeatmap = beatmaps.GetWorkingBeatmap(scoreInfo.BeatmapInfo);

                if (workingBeatmap is DummyWorkingBeatmap)
                    return null;

                playableBeatmap = workingBeatmap.GetPlayableBeatmap(scoreInfo.Ruleset, scoreInfo.Mods);
            }

            if (playableBeatmap.HitObjects.Count == 0)
                return null;

            EzScoreTimeline? timeline;

            if (scoreInfo.Ruleset.OnlineID == 3)
            {
                timeline = EzScoreTimelineBridge.TryBuildManiaTimeline(databasedScore, playableBeatmap, cancellationToken);
            }
            else
            {
                var (hitEvents, offsetsRelativeToEnd) = resolveHitEvents(databasedScore, playableBeatmap, cancellationToken);

                if (hitEvents == null || hitEvents.Count == 0)
                    return null;

                timeline = buildFromHitEvents(ruleset, playableBeatmap, scoreInfo, hitEvents, offsetsRelativeToEnd);
            }

            if (timeline == null)
                return null;

            if (!string.IsNullOrEmpty(cacheKey))
                timeline_cache[cacheKey] = timeline;

            return timeline;
        }

        public static void InvalidateCache(ScoreInfo? scoreInfo)
        {
            if (scoreInfo == null)
                return;

            foreach (string key in timeline_cache.Keys.Where(k => k.Contains(getScoreIdentity(scoreInfo) ?? string.Empty)).ToArray())
                timeline_cache.TryRemove(key, out _);
        }

        private static (List<HitEvent>? hitEvents, bool offsetsRelativeToEnd) resolveHitEvents(Score databasedScore, IBeatmap playableBeatmap, CancellationToken cancellationToken)
        {
            if (databasedScore.ScoreInfo.HitEvents.Count > 0)
                return (databasedScore.ScoreInfo.HitEvents.ToList(), true);

            return (EzScoreReloadBridge.TryGenerate(databasedScore, playableBeatmap, cancellationToken), false);
        }

        internal static EzScoreTimeline BuildFromHitEventsForTesting(Ruleset ruleset, IBeatmap beatmap, ScoreInfo scoreInfo, IReadOnlyList<HitEvent> hitEvents,
            bool offsetsRelativeToEnd = false)
            => buildFromHitEvents(ruleset, beatmap, scoreInfo, hitEvents, offsetsRelativeToEnd);

        private static EzScoreTimeline buildFromHitEvents(Ruleset ruleset, IBeatmap beatmap, ScoreInfo scoreInfo, IReadOnlyList<HitEvent> hitEvents, bool offsetsRelativeToEnd)
        {
            double fallbackMissWindow = resolveFallbackMissWindow(beatmap);

            var scoreProcessor = ruleset.CreateScoreProcessor();
            applyScoreProcessorContext(scoreProcessor, scoreInfo);
            scoreProcessor.ApplyBeatmap(beatmap);
            scoreProcessor.Mods.Value = scoreInfo.Mods;

            foreach (var mod in scoreInfo.Mods.OfType<IApplicableToScoreProcessor>())
                mod.ApplyToScoreProcessor(scoreProcessor);

            var snapshots = new List<EzScoreTimelineSnapshot>();
            int missCount = 0;
            double lastClockTime = double.NegativeInfinity;

            foreach (var hitEvent in hitEvents.OrderBy(e => getJudgementTime(e, offsetsRelativeToEnd, beatmap, fallbackMissWindow)))
            {
                var beatmapHitObject = findBeatmapHitObject(beatmap, hitEvent.HitObject);
                ensureHitWindows(beatmap, beatmapHitObject);

                scoreProcessor.ApplyResult(new JudgementResult(hitEvent.HitObject, hitEvent.HitObject.CreateJudgement())
                {
                    Type = hitEvent.Result,
                    TimeOffset = hitEvent.TimeOffset,
                });

                if (hitEvent.Result.IsMiss())
                    missCount++;

                double clockTime = getJudgementTime(hitEvent, offsetsRelativeToEnd, beatmap, fallbackMissWindow, beatmapHitObject);

                // 保持时间线严格单调，避免同一时刻多条快照导致查询抖动。
                if (clockTime <= lastClockTime)
                    clockTime = lastClockTime + 0.001;

                lastClockTime = clockTime;
                snapshots.Add(createSnapshot(clockTime, scoreProcessor, missCount));
            }

            if (snapshots.Count == 0)
                snapshots.Add(createSnapshot(0, scoreProcessor, 0));
            else if (snapshots[0].ClockTime > 0)
                snapshots.Insert(0, new EzScoreTimelineSnapshot { ClockTime = 0 });

            return new EzScoreTimeline(snapshots);
        }

        private static double getJudgementTime(HitEvent hitEvent, bool offsetsRelativeToEnd, IBeatmap beatmap, double fallbackMissWindow, HitObject? beatmapHitObject = null)
        {
            beatmapHitObject ??= findBeatmapHitObject(beatmap, hitEvent.HitObject);
            return EzScoreTimelineJudgementTime.Get(hitEvent, offsetsRelativeToEnd, beatmapHitObject, fallbackMissWindow);
        }

        private static HitObject findBeatmapHitObject(IBeatmap beatmap, HitObject hitObject)
        {
            foreach (var candidate in beatmap.HitObjects)
            {
                if (ReferenceEquals(candidate, hitObject))
                    return candidate;

                var nested = findNestedBeatmapHitObject(candidate, hitObject);
                if (nested != null)
                    return nested;
            }

            foreach (var candidate in beatmap.HitObjects)
            {
                if (objectsMatchForLookup(candidate, hitObject))
                    return candidate;

                var nested = findNestedBeatmapHitObject(candidate, hitObject);
                if (nested != null)
                    return nested;
            }

            return hitObject;
        }

        private static HitObject? findNestedBeatmapHitObject(HitObject parent, HitObject hitObject)
        {
            foreach (var nested in parent.NestedHitObjects)
            {
                if (ReferenceEquals(nested, hitObject))
                    return nested;

                var deeper = findNestedBeatmapHitObject(nested, hitObject);
                if (deeper != null)
                    return deeper;
            }

            foreach (var nested in parent.NestedHitObjects)
            {
                if (objectsMatchForLookup(nested, hitObject))
                    return nested;
            }

            return null;
        }

        private static bool objectsMatchForLookup(HitObject candidate, HitObject hitObject)
        {
            if (candidate.StartTime != hitObject.StartTime || candidate.GetType() != hitObject.GetType())
                return false;

            if (hitObject is IHasColumn hitColumn)
            {
                if (candidate is IHasColumn candidateColumn)
                    return candidateColumn.Column == hitColumn.Column;

                return false;
            }

            return true;
        }

        private static void applyScoreProcessorContext(ScoreProcessor scoreProcessor, ScoreInfo scoreInfo)
        {
            if (scoreInfo.IsLegacyScore)
                scoreProcessor.IsLegacyScore = true;

            if (scoreInfo.Ruleset.OnlineID != 3)
                return;

            var environment = GameplayEnvironment.FromScore(scoreInfo, GlobalConfigStore.EzConfig);

            PropertyInfo? overrideProperty = scoreProcessor.GetType().GetProperty("TimelineHitModeOverride", BindingFlags.Public | BindingFlags.Instance);

            if (overrideProperty != null && overrideProperty.CanWrite)
                overrideProperty.SetValue(scoreProcessor, environment.ManiaHitMode);
        }

        private static void ensureHitWindows(IBeatmap beatmap, HitObject hitObject)
        {
            if (beatmap.BeatmapInfo == null)
                return;

            if (hitObject.HitWindows != null && hitObject.HitWindows != HitWindows.Empty)
                return;

            if (hitObject.NestedHitObjects.Count > 0)
                return;

            hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty);
        }

        private static double resolveFallbackMissWindow(IBeatmap beatmap)
        {
            foreach (var hitObject in beatmap.HitObjects)
            {
                var windows = findFirstNonEmptyHitWindows(hitObject);
                if (windows != null)
                    return windows.WindowFor(HitResult.Miss);
            }

            if (beatmap.BeatmapInfo == null)
                return 0;

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject.NestedHitObjects.Count > 0)
                    continue;

                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty);

                if (hitObject.HitWindows != null && hitObject.HitWindows != HitWindows.Empty)
                    return hitObject.HitWindows.WindowFor(HitResult.Miss);
            }

            return 0;
        }

        private static HitWindows? findFirstNonEmptyHitWindows(HitObject hitObject)
        {
            if (hitObject.HitWindows != null && hitObject.HitWindows != HitWindows.Empty)
                return hitObject.HitWindows;

            foreach (var nested in hitObject.NestedHitObjects)
            {
                var windows = findFirstNonEmptyHitWindows(nested);
                if (windows != null)
                    return windows;
            }

            return null;
        }

        private static EzScoreTimelineSnapshot createSnapshot(double clockTime, ScoreProcessor scoreProcessor, int missCount)
        {
            return new EzScoreTimelineSnapshot
            {
                ClockTime = clockTime,
                TotalScore = scoreProcessor.TotalScore.Value,
                Accuracy = scoreProcessor.Accuracy.Value,
                Combo = scoreProcessor.Combo.Value,
                HighestCombo = scoreProcessor.HighestCombo.Value,
                MissCount = missCount,
            };
        }

        private static string? getCacheKey(ScoreInfo? scoreInfo)
        {
            string? identity = getScoreIdentity(scoreInfo);

            if (identity == null)
                return null;

            if (scoreInfo!.Ruleset.OnlineID == 3)
            {
                var environment = GameplayEnvironment.FromScore(scoreInfo, GlobalConfigStore.EzConfig);
                return $"{timeline_cache_version}:{identity}:hm{(int)environment.ManiaHitMode}:hh{(int)environment.ManiaHealthMode}:jp{(int)environment.JudgePrecedence}";
            }

            return $"{timeline_cache_version}:{identity}";
        }

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

        private static void ensureGeneratorsInitialised()
        {
            if (generatorsInitialised)
                return;

            generatorsInitialised = true;
            EzScoreReloadBridge.InitializeAllGenerators();
        }

        private static bool hasReplayFile(ScoreInfo scoreInfo)
            => scoreInfo.Files.Any(f => f.Filename.EndsWith(".osr", StringComparison.OrdinalIgnoreCase));
    }
}
