// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    /// <summary>
    /// 单元测专用：合成 HitEvents → 按序 ApplyResult → Timeline（不回流生产代码）。
    /// </summary>
    internal static class EzScoreTimelineTestHelper
    {
        public static double GetJudgementTime(HitEvent hitEvent, bool offsetsRelativeToEnd, HitObject? beatmapHitObject = null, double fallbackMissWindow = 0)
        {
            double rate = hitEvent.GameplayRate ?? 1.0;
            double offset = hitEvent.TimeOffset / rate;

            var timingObject = beatmapHitObject ?? hitEvent.HitObject;
            var windowObject = beatmapHitObject ?? hitEvent.HitObject;

            double startTime = timingObject.StartTime;
            double endTime = timingObject.GetEndTime();

            double judgementTime = offsetsRelativeToEnd
                ? endTime + offset
                : startTime + offset;

            double missWindow = windowObject.HitWindows != null && windowObject.HitWindows != HitWindows.Empty
                ? windowObject.HitWindows.WindowFor(HitResult.Miss)
                : 0;

            if (missWindow <= 0 && fallbackMissWindow > 0)
                missWindow = fallbackMissWindow;

            if (missWindow > 0)
                judgementTime = Math.Max(startTime - missWindow, judgementTime);

            return judgementTime;
        }

        public static EzScoreTimeline BuildFromHitEvents(Ruleset ruleset, IBeatmap beatmap, ScoreInfo scoreInfo, IReadOnlyList<HitEvent> hitEvents,
                                                         bool offsetsRelativeToEnd = false)
        {
            double fallbackMissWindow = resolveFallbackMissWindow(beatmap);

            var scoreProcessor = ruleset.CreateScoreProcessor();
            applyScoreProcessorContext(scoreProcessor, scoreInfo);
            scoreProcessor.ApplyBeatmap(beatmap);
            scoreProcessor.Mods.Value = scoreInfo.Mods;

            foreach (var mod in scoreInfo.Mods.OfType<IApplicableToScoreProcessor>())
                mod.ApplyToScoreProcessor(scoreProcessor);

            var hitObjectMap = buildHitObjectReferenceMap(beatmap);

            var snapshots = new List<EzScoreTimelineSnapshot>();
            double lastClockTime = double.NegativeInfinity;

            foreach (var hitEvent in hitEvents.OrderBy(e => getJudgementTime(e, offsetsRelativeToEnd, beatmap, fallbackMissWindow, null, hitObjectMap)))
            {
                var beatmapHitObject = findBeatmapHitObject(beatmap, hitEvent.HitObject, hitObjectMap);
                ensureHitWindows(beatmap, beatmapHitObject);

                scoreProcessor.ApplyResult(new JudgementResult(hitEvent.HitObject, hitEvent.HitObject.CreateJudgement())
                {
                    Type = hitEvent.Result,
                    TimeOffset = hitEvent.TimeOffset,
                });

                double clockTime = getJudgementTime(hitEvent, offsetsRelativeToEnd, beatmap, fallbackMissWindow, beatmapHitObject, hitObjectMap);

                if (clockTime <= lastClockTime)
                    clockTime = lastClockTime + 0.001;

                lastClockTime = clockTime;
                snapshots.Add(EzScoreTimelineSnapshot.Create(clockTime, scoreProcessor, hitEvent.GameplayRate ?? 1.0));
            }

            if (snapshots.Count == 0)
                snapshots.Add(EzScoreTimelineSnapshot.Create(0, scoreProcessor));
            else if (snapshots[0].ClockTime > 0)
                snapshots.Insert(0, new EzScoreTimelineSnapshot { ClockTime = 0 });

            return new EzScoreTimeline(snapshots);
        }

        private static double getJudgementTime(HitEvent hitEvent, bool offsetsRelativeToEnd, IBeatmap beatmap, double fallbackMissWindow, HitObject? beatmapHitObject = null,
                                               Dictionary<HitObject, HitObject>? hitObjectMap = null)
        {
            beatmapHitObject ??= findBeatmapHitObject(beatmap, hitEvent.HitObject, hitObjectMap);
            return GetJudgementTime(hitEvent, offsetsRelativeToEnd, beatmapHitObject, fallbackMissWindow);
        }

        private static Dictionary<HitObject, HitObject> buildHitObjectReferenceMap(IBeatmap beatmap)
        {
            var map = new Dictionary<HitObject, HitObject>(ReferenceEqualityComparer.Instance);

            foreach (var ho in beatmap.HitObjects)
                collectHitObjectReferences(ho, map);

            return map;
        }

        private static void collectHitObjectReferences(HitObject hitObject, Dictionary<HitObject, HitObject> map)
        {
            map.TryAdd(hitObject, hitObject);

            foreach (var nested in hitObject.NestedHitObjects)
                collectHitObjectReferences(nested, map);
        }

        private static HitObject findBeatmapHitObject(IBeatmap beatmap, HitObject hitObject, Dictionary<HitObject, HitObject>? hitObjectMap = null)
        {
            if (hitObjectMap != null && hitObjectMap.TryGetValue(hitObject, out var mapped))
                return mapped;

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
        }

        private static void ensureHitWindows(IBeatmap? beatmap, HitObject hitObject)
        {
            if (beatmap == null)
                return;

            if (hitObject.HitWindows != null && hitObject.HitWindows != HitWindows.Empty)
                return;

            if (hitObject.NestedHitObjects.Count > 0)
                return;

            hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty);
        }

        private static double resolveFallbackMissWindow(IBeatmap? beatmap)
        {
            if (beatmap == null)
                return 0;

            foreach (var hitObject in beatmap.HitObjects)
            {
                var windows = findFirstNonEmptyHitWindows(hitObject);
                if (windows != null)
                    return windows.WindowFor(HitResult.Miss);
            }

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
    }
}
