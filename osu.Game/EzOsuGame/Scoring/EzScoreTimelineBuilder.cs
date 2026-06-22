// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
    /// <summary>
    /// 从本地 replay 构建分数时间线。Mania 走 Session 一遍 SP 快照；其他规则集暂用 HitEvents 重放（仅非 Mania）。
    ///
    /// 缓存策略：原进程级 <c>timeline_cache</c> 已废弃，改由调用方通过 <see cref="IEzScoreTimelineCache"/> 注入
    /// Session 作用域缓存。这样可以避免不同上下文（HitMode / HealthMode / JudgePrecedence / Beatmap 难度）、
    /// 不同 Player（皮肤编辑器嵌入 Player / 真实 Player）之间互相污染缓存。
    /// 重启进程即清空所有 cache（无磁盘持久化）。
    ///
    /// 设计选择：timeline 与 <see cref="ScoreInfo.TotalScore"/> 解耦。调用方对同一条 ScoreInfo 调
    /// <see cref="osu.Game.Scoring.ScoreManager.Recalculate"/> / <see cref="osu.Game.Scoring.ScoreManager.RenamePlayer"/>
    /// 后，cache 仍命中，UI 上 ghost 终局分可能与 ScoreInfo 终局分不一致 — 视为可接受。
    /// </summary>
    // TODO(EZ-SR-TL-020): Osu Session 对齐后更新类注释，移除 HitEvents 重放描述。
    public static class EzScoreTimelineBuilder
    {
        private static bool generatorsInitialised;

        /// <summary>
        /// 创建一个进程内 in-memory 缓存实例，绑定到调用方生命周期。
        /// 通常由 <see cref="EzScoreRaceSession"/> 持有，Session Dispose 时调用 <see cref="IEzScoreTimelineCache.Clear"/>。
        /// </summary>
        public static IEzScoreTimelineCache CreateSessionCache() => new EzScoreTimelineCache();

        public static EzScoreTimeline? TryBuild(ScoreManager scoreManager, BeatmapManager beatmaps, ScoreInfo scoreInfo, IBeatmap? sharedPlayableBeatmap = null,
                                                IEzScoreTimelineCache? cache = null, IGameplayEnvironment? environment = null, CancellationToken cancellationToken = default)
            => tryBuild(scoreManager, beatmaps, scoreInfo, sharedPlayableBeatmap, cache ?? NullEzScoreTimelineCache.INSTANCE, environment, cancellationToken);

        private static EzScoreTimeline? tryBuild(ScoreManager scoreManager, BeatmapManager beatmaps, ScoreInfo scoreInfo, IBeatmap? sharedPlayableBeatmap,
                                                 IEzScoreTimelineCache cache, IGameplayEnvironment? environment, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(scoreManager);
            ArgumentNullException.ThrowIfNull(beatmaps);
            ArgumentNullException.ThrowIfNull(scoreInfo);

            ensureGeneratorsInitialised();

            var timelineMode = EzScoreRaceRulesetSupport.GetGhostTimelineMode(scoreInfo.Ruleset);

            if (timelineMode == EzScoreRaceGhostTimelineMode.None)
                return null;

            IBeatmap? beatmapForFingerprint;

            if (sharedPlayableBeatmap != null)
            {
                beatmapForFingerprint = sharedPlayableBeatmap;
            }
            else
            {
                var workingBeatmap = beatmaps.GetWorkingBeatmap(scoreInfo.BeatmapInfo);

                if (workingBeatmap is DummyWorkingBeatmap)
                    beatmapForFingerprint = null;
                else
                    beatmapForFingerprint = workingBeatmap.GetPlayableBeatmap(scoreInfo.Ruleset, scoreInfo.Mods);
            }

            string? cacheKey = beatmapForFingerprint != null
                ? getCacheKey(scoreInfo, timelineMode, environment ?? GameplayEnvironment.FromLive(GlobalConfigStore.EzConfig), beatmapForFingerprint)
                : null;

            if (!string.IsNullOrEmpty(cacheKey) && cache.TryGet(cacheKey, out var cached))
                return cached == EzScoreTimeline.EMPTY ? null : cached;

            cancellationToken.ThrowIfCancellationRequested();

            var databasedScore = scoreManager.GetScore(scoreInfo);

            // 唯一门槛：磁盘/库内是否有可读的 replay 帧（不依赖 ScoreInfo.Files 元数据）。
            if (databasedScore?.Replay == null || databasedScore.Replay.Frames.Count == 0)
            {
                // 显式缓存 null 结果（以 Empty sentinel 形式），避免后续每帧反复走这条路径。
                if (!string.IsNullOrEmpty(cacheKey))
                    cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                return null;
            }

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
                {
                    if (!string.IsNullOrEmpty(cacheKey))
                        cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                    return null;
                }

                playableBeatmap = workingBeatmap.GetPlayableBeatmap(scoreInfo.Ruleset, scoreInfo.Mods);
            }

            if (playableBeatmap.HitObjects.Count == 0)
            {
                if (!string.IsNullOrEmpty(cacheKey))
                    cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                return null;
            }

            EzScoreTimeline? timeline;

            switch (timelineMode)
            {
                case EzScoreRaceGhostTimelineMode.ManiaSession:
                    timeline = EzScoreTimelineBridge.TryBuildManiaTimeline(databasedScore, playableBeatmap, cancellationToken);
                    break;

                case EzScoreRaceGhostTimelineMode.HitEvents:
                    // TODO(EZ-SR-TL-007): 改走 OsuReplaySession + Bridge，勿再 resolveHitEvents/buildFromHitEvents。
                    var (hitEvents, offsetsRelativeToEnd) = resolveHitEvents(databasedScore, playableBeatmap, cancellationToken);

                    if (hitEvents == null || hitEvents.Count == 0)
                    {
                        if (!string.IsNullOrEmpty(cacheKey))
                            cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                        return null;
                    }

                    timeline = buildFromHitEvents(ruleset, playableBeatmap, scoreInfo, hitEvents, offsetsRelativeToEnd);
                    break;

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

        // TODO(EZ-SR-TL-010): Osu Session 完成后删除；角逐 timeline 不再二次从 HitEvents 构建。
        private static (List<HitEvent>? hitEvents, bool offsetsRelativeToEnd) resolveHitEvents(Score databasedScore, IBeatmap playableBeatmap, CancellationToken cancellationToken)
        {
            // 统计页打开时 ScoreInfo 上可能有临时 HitEvents（[Ignored]，不持久化）。
            if (databasedScore.ScoreInfo.HitEvents.Count > 0)
                return (databasedScore.ScoreInfo.HitEvents.ToList(), true);

            return (EzScoreReloadBridge.TryGenerate(databasedScore, playableBeatmap, cancellationToken), false);
        }

        // TODO(EZ-SR-TL-011): Osu Session 完成后删除 buildFromHitEvents 及 BuildFromHitEventsForTesting。
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

        // TODO(EZ-SR-TL-012): Osu Session 完成后删除；仅 HitEvents 重放路径使用。
        private static double getJudgementTime(HitEvent hitEvent, bool offsetsRelativeToEnd, IBeatmap beatmap, double fallbackMissWindow, HitObject? beatmapHitObject = null)
        {
            beatmapHitObject ??= findBeatmapHitObject(beatmap, hitEvent.HitObject);
            return EzScoreTimelineJudgementTime.Get(hitEvent, offsetsRelativeToEnd, beatmapHitObject, fallbackMissWindow);
        }

        // TODO(EZ-SR-TL-013): Osu Session 完成后删除 HitObject 查找补丁（findBeatmapHitObject 等）。
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

        // TODO(EZ-SR-TL-014): Osu Session 完成后删除；Mania TimelineHitModeOverride 已在 ManiaReplaySession 内设置。
        private static void applyScoreProcessorContext(ScoreProcessor scoreProcessor, ScoreInfo scoreInfo)
        {
            if (scoreInfo.IsLegacyScore)
                scoreProcessor.IsLegacyScore = true;

            if (scoreInfo.Ruleset.OnlineID != 3)
                return;

            var environment = GameplayEnvironment.FromLive(GlobalConfigStore.EzConfig);

            PropertyInfo? overrideProperty = scoreProcessor.GetType().GetProperty("TimelineHitModeOverride", BindingFlags.Public | BindingFlags.Instance);

            if (overrideProperty != null && overrideProperty.CanWrite)
                overrideProperty.SetValue(scoreProcessor, environment.ManiaHitMode);
        }

        // TODO(EZ-SR-TL-015): Osu Session 完成后删除 ensureHitWindows/resolveFallbackMissWindow 等 HitEvents 专用辅助。
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

        // TODO(EZ-SR-TL-016): Osu Session 完成后删除；与 ManiaReplayTimelineRecorder 重复。
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

        private static string? getCacheKey(ScoreInfo? scoreInfo, EzScoreRaceGhostTimelineMode timelineMode, IGameplayEnvironment environment, IBeatmap beatmap)
        {
            string? identity = getScoreIdentity(scoreInfo);

            if (identity == null || scoreInfo == null)
                return null;

            string modFp = getModFingerprint(scoreInfo.Mods);
            string beatmapFp = beatmap.BeatmapInfo != null
                ? $"b:{beatmap.BeatmapInfo.ID}:od{beatmap.BeatmapInfo.Difficulty.OverallDifficulty:F2}:hp{beatmap.BeatmapInfo.Difficulty.DrainRate:F2}"
                : "b:?";

            switch (timelineMode)
            {
                case EzScoreRaceGhostTimelineMode.ManiaSession:
                {
                    // 与角逐 HUD 一致：全局 HitMode/HealthMode/JudgePrecedence，不读成绩嵌入字段。
                    return $"{identity}|m|{modFp}|{beatmapFp}|hm{(int)environment.ManiaHitMode}|hh{(int)environment.ManiaHealthMode}|jp{(int)environment.JudgePrecedence}";
                }

                case EzScoreRaceGhostTimelineMode.HitEvents:
                    // TODO(EZ-SR-TL-008): Osu Session 对齐后定义 Osu 缓存键策略（参照 ManiaSession 环境键）。
                    return $"{identity}|h|{modFp}|{beatmapFp}";

                default:
                    return null;
            }
        }

        private static string getModFingerprint(IReadOnlyList<Mod> mods)
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

        private static void ensureGeneratorsInitialised()
        {
            if (generatorsInitialised)
                return;

            generatorsInitialised = true;
            EzScoreReloadBridge.InitializeAllGenerators();
        }
    }
}
