// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftBracket : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Bracket;
        protected override string PatternName => "Bracket";
        protected override string PatternAcronym => "PSB";

        protected override int DefaultLevel => 4;
        protected override EzOscillator.EzWaveform DefaultWaveform => EzOscillator.EzWaveform.Sine;
        protected override int DefaultOscillationBeats => 1;
        protected override int DefaultWindowProcessInterval => 1;
        protected override int DefaultWindowProcessOffset => 0;
        protected override int DefaultApplyOrder => 50;

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_Level_Label), nameof(EzManiaModStrings.PatternShift_Level_Description))]
        public new BindableNumber<int> Level => base.Level;

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_WindowMaxIterations_Label), nameof(EzManiaModStrings.PatternShift_WindowMaxIterations_Description))]
        public BindableNumber<int> WindowMaxNotes { get; } = new BindableInt(2)
        {
            MinValue = 1,
            MaxValue = 8,
            Precision = 1
        };

        protected override int MaxIterationsPerWindow => Math.Clamp(WindowMaxNotes.Value, 1, 4);

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                foreach (var item in base.SettingDescription)
                    yield return item;

                yield return ("Window Max Notes", $"{WindowMaxNotes.Value}");
            }
        }

        protected override void ApplyPatternForWindow(List<ManiaHitObject> windowObjects,
                                                      ManiaBeatmap beatmap,
                                                      double windowStart,
                                                      double windowEnd,
                                                      KeyPatternSettings settings,
                                                      Random rng,
                                                      int maxIterationsPerWindow)
        {
            if (windowObjects.Count == 0)
                return;

            int beforeCount = beatmap.HitObjects.Count;

            double beatLength = beatmap.ControlPointInfo.TimingPointAt(windowStart).BeatLength;
            if (beatLength <= 0)
                return;

            double activeBeatFraction = ManiaKeyPatternHelp.GetActiveBeatFraction(windowObjects, beatmap, 1.0 / 4.0);

            // 先尝试 Cut 风格
            if (ManiaKeyPatternHelp.TryGetCutJackBaseFraction(activeBeatFraction, out double baseFraction))
            {
                double mainFraction = baseFraction;
                double mainStep = Math.Max(1, beatLength * mainFraction);
                double subStep = Math.Max(1, beatLength * mainFraction);

                if (subStep > 0)
                {
                    double t = windowStart + subStep;
                    double lineOffset = Math.Floor((t - windowStart) / mainStep) * mainStep;
                    double prevLine = windowStart + lineOffset;
                    double nextLine = prevLine + mainStep;

                    var prevColumns = ManiaKeyPatternHelp.GetColumnsAtTime(windowObjects, prevLine, TIME_TOLERANCE);
                    var nextColumns = ManiaKeyPatternHelp.GetColumnsAtTime(windowObjects, nextLine, TIME_TOLERANCE);

                    applyCutPattern(beatmap, windowObjects, prevColumns, nextColumns, t, Math.Max(0, settings.MinK), Math.Max(0, settings.MaxK), rng, maxIterationsPerWindow);
                }
            }

            if (beatmap.HitObjects.Count > beforeCount)
                return;

            // 否则尝试 Cross 风格
            double mainFraction2 = Math.Max(activeBeatFraction, 1.0 / 8.0);
            double mainStep2 = Math.Max(1, beatLength * mainFraction2);
            double subStep2 = mainStep2 / 2.0;

            if (subStep2 <= 0)
                return;

            double t2 = windowStart + subStep2;
            double lineOffset2 = Math.Floor((t2 - windowStart) / mainStep2) * mainStep2;
            double prevLine2 = windowStart + lineOffset2;
            double nextLine2 = prevLine2 + mainStep2;

            var prevColumns2 = ManiaKeyPatternHelp.GetColumnsAtTime(windowObjects, prevLine2, TIME_TOLERANCE);
            var nextColumns2 = ManiaKeyPatternHelp.GetColumnsAtTime(windowObjects, nextLine2, TIME_TOLERANCE);

            applyBracketPattern(beatmap, windowObjects, prevColumns2, nextColumns2, t2, Math.Max(0, settings.MinK), Math.Max(0, settings.MaxK), rng, maxIterationsPerWindow);
        }

        private static void applyBracketPattern(ManiaBeatmap beatmap,
                                                List<ManiaHitObject> windowObjects,
                                                HashSet<int> prevColumns,
                                                HashSet<int> nextColumns,
                                                double time,
                                                int minK,
                                                int maxK,
                                                Random rng,
                                                int maxIterationsPerWindow)
        {
            int totalColumns = Math.Max(1, beatmap.TotalColumns);
            var avoid = new HashSet<int>(prevColumns);
            avoid.UnionWith(nextColumns);

            var available = new List<int>();

            for (int i = 0; i < totalColumns; i++)
            {
                if (!avoid.Contains(i))
                    available.Add(i);
            }

            if (available.Count == 0)
                return;

            double beatLength = beatmap.ControlPointInfo.TimingPointAt(time).BeatLength;
            double quarter = beatLength / 4.0;

            int preferredDivisor = 2; // 默认 1/2

            bool isOnDivisionLocal(double t, int divisor)
            {
                if (divisor <= 0 || beatLength <= 0)
                    return false;

                double interval = beatLength / divisor;
                double mod = t % interval;
                return mod <= TIME_TOLERANCE || Math.Abs(interval - mod) <= TIME_TOLERANCE;
            }

            foreach (var obj in windowObjects)
            {
                bool isOn1To4 = isOnDivisionLocal(obj.StartTime, 1)
                                || isOnDivisionLocal(obj.StartTime, 2)
                                || isOnDivisionLocal(obj.StartTime, 3)
                                || isOnDivisionLocal(obj.StartTime, 4);
                if (!isOn1To4)
                    return;
            }

            foreach (var obj in windowObjects)
            {
                if (isOnDivisionLocal(obj.StartTime, 2))
                {
                    preferredDivisor = 2;
                    break;
                }

                if (isOnDivisionLocal(obj.StartTime, 3))
                {
                    preferredDivisor = 3;
                    break;
                }
            }

            double targetInterval = beatLength / Math.Max(1, preferredDivisor);
            double baseAligned = Math.Round(time / targetInterval) * targetInterval;
            double windowStart = windowObjects.Count > 0 ? windowObjects[0].StartTime : time;
            double windowEnd = windowObjects.Count > 0 ? windowObjects[^1].StartTime : time;

            foreach (var obj in windowObjects)
            {
                double qTime = Math.Round(obj.StartTime / quarter) * quarter;
                double prevTime = qTime - quarter;
                double nextTime = qTime + quarter;

                bool conflictPrev = ManiaKeyPatternHelp.HasNoteAtTime(beatmap, obj.Column, prevTime, null, TIME_TOLERANCE);
                bool conflictNext = ManiaKeyPatternHelp.HasNoteAtTime(beatmap, obj.Column, nextTime, null, TIME_TOLERANCE);

                if (!conflictPrev && !conflictNext)
                    continue;

                int? chosen = ManiaKeyPatternHelp.FindNearestAvailableColumnForQuarter(beatmap, obj.Column, qTime, prevTime, nextTime, totalColumns, TIME_TOLERANCE);
                if (chosen.HasValue)
                    obj.Column = chosen.Value;
            }

            double? chosenCandidate = null;
            int[] tries = { 0, -1, 1, -2, 2 };

            foreach (int off in tries)
            {
                double ct = baseAligned + off * targetInterval;
                if (ct < windowStart - TIME_TOLERANCE || ct > windowEnd + TIME_TOLERANCE)
                    continue;

                bool onBeat = false;

                if (beatLength > 0)
                {
                    double modBeat = ct % beatLength;
                    onBeat = modBeat <= TIME_TOLERANCE || Math.Abs(beatLength - modBeat) <= TIME_TOLERANCE;
                }

                if (onBeat)
                    continue;

                double prevTimeTry = ct - quarter;
                double nextTimeTry = ct + quarter;
                var filteredTry = new List<int>();

                foreach (int col in available)
                {
                    if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, col, prevTimeTry, null, TIME_TOLERANCE))
                        continue;
                    if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, col, nextTimeTry, null, TIME_TOLERANCE))
                        continue;
                    if (ManiaKeyPatternHelp.IsHoldOccupyingColumn(beatmap, col, ct, null, TIME_TOLERANCE))
                        continue;

                    filteredTry.Add(col);
                }

                if (filteredTry.Count > 0)
                {
                    chosenCandidate = ct;
                    available = filteredTry;
                    break;
                }

                if (chosenCandidate == null && !onBeat)
                    chosenCandidate = ct;
            }

            double candidateTime = chosenCandidate ?? baseAligned;

            double halfInterval = beatLength / 2.0;
            double thirdInterval = beatLength / 3.0;

            double snapTo(double t, double interval) => Math.Round(t / interval) * interval;
            double snappedHalf = snapTo(candidateTime, halfInterval);
            double snappedThird = snapTo(candidateTime, thirdInterval);

            bool isOnBeat(double t)
            {
                if (beatLength <= 0)
                    return false;

                double mod = t % beatLength;
                return mod <= TIME_TOLERANCE || Math.Abs(beatLength - mod) <= TIME_TOLERANCE;
            }

            bool isWithinWindow(double t) => t >= windowStart - TIME_TOLERANCE && t <= windowEnd + TIME_TOLERANCE;

            double distHalf = Math.Abs(candidateTime - snappedHalf);
            double distThird = Math.Abs(candidateTime - snappedThird);

            double? snappedCandidate = null;

            if (distHalf <= distThird)
            {
                if (!isOnBeat(snappedHalf) && isWithinWindow(snappedHalf))
                    snappedCandidate = snappedHalf;
                else if (!isOnBeat(snappedThird) && isWithinWindow(snappedThird))
                    snappedCandidate = snappedThird;
            }
            else
            {
                if (!isOnBeat(snappedThird) && isWithinWindow(snappedThird))
                    snappedCandidate = snappedThird;
                else if (!isOnBeat(snappedHalf) && isWithinWindow(snappedHalf))
                    snappedCandidate = snappedHalf;
            }

            if (snappedCandidate == null)
                return;

            candidateTime = snappedCandidate.Value;

            int count = rng.Next(minK, maxK + 1);
            count = Math.Clamp(count, 0, available.Count);
            count = Math.Min(count, Math.Max(0, maxIterationsPerWindow));

            if (avoid.Count == 0)
            {
                var globalObjectsLocal = beatmap.HitObjects.ToList();
                var existingColsAtTimeLocal = ManiaKeyPatternHelp.GetColumnsAtTime(globalObjectsLocal, candidateTime, TIME_TOLERANCE);

                var localCandidates0 = new List<int>(available);
                List<double>? localWeights0 = null;

                for (int i = 0; i < count && localCandidates0.Count > 0; i++)
                {
                    int pickedIndex = -1;

                    while (localCandidates0.Count > 0)
                    {
                        int idx = localWeights0 != null ? ManiaKeyPatternHelp.PickWeightedIndex(localWeights0, rng) : rng.Next(localCandidates0.Count);
                        int col = localCandidates0[idx];

                        bool adjacent = false;

                        foreach (int ec in existingColsAtTimeLocal)
                        {
                            if (Math.Abs(ec - col) <= 1)
                            {
                                adjacent = true;
                                break;
                            }
                        }

                        if (!adjacent)
                        {
                            pickedIndex = idx;
                            break;
                        }

                        localCandidates0.RemoveAt(idx);
                        localWeights0?.RemoveAt(idx);
                    }

                    if (pickedIndex < 0)
                        break;

                    int chosen = localCandidates0[pickedIndex];
                    ManiaKeyPatternHelp.TryAddNoteFromCandidates(beatmap, new List<int> { chosen }, null, rng, candidateTime);

                    for (int rem = localCandidates0.Count - 1; rem >= 0; rem--)
                    {
                        if (Math.Abs(localCandidates0[rem] - chosen) <= 1)
                        {
                            localCandidates0.RemoveAt(rem);
                            localWeights0?.RemoveAt(rem);
                        }
                    }
                }

                return;
            }

            double median = ManiaKeyPatternHelp.GetMedian(avoid);
            double maxDistance = 1;

            for (int i = 0; i < available.Count; i++)
            {
                double dist = ManiaKeyPatternHelp.DistanceToNearest(avoid, available[i]);
                if (dist > maxDistance)
                    maxDistance = dist;
            }

            var weights = new List<double>(available.Count);

            for (int i = 0; i < available.Count; i++)
            {
                int column = available[i];
                double dist = ManiaKeyPatternHelp.DistanceToNearest(avoid, column);
                double medianBias = maxDistance - Math.Abs(column - median);
                weights.Add(Math.Max(0.001, dist * 2.0 + medianBias));
            }

            var weightsForUse = weights;

            var globalObjects = beatmap.HitObjects.ToList();
            var existingColsAtTime = ManiaKeyPatternHelp.GetColumnsAtTime(globalObjects, candidateTime, TIME_TOLERANCE);

            var localCandidates = new List<int>(available);
            var localWeights = new List<double>(weightsForUse);

            for (int i = 0; i < count && localCandidates.Count > 0; i++)
            {
                int pickedIndex = -1;

                while (localCandidates.Count > 0)
                {
                    int idx = localWeights != null ? ManiaKeyPatternHelp.PickWeightedIndex(localWeights, rng) : rng.Next(localCandidates.Count);
                    int col = localCandidates[idx];

                    bool adjacentToExisting = false;

                    foreach (int ec in existingColsAtTime)
                    {
                        if (Math.Abs(ec - col) <= 1)
                        {
                            adjacentToExisting = true;
                            break;
                        }
                    }

                    if (!adjacentToExisting)
                    {
                        pickedIndex = idx;
                        break;
                    }

                    localCandidates.RemoveAt(idx);
                    localWeights?.RemoveAt(idx);
                }

                if (pickedIndex < 0)
                    break;

                int chosenCol = localCandidates[pickedIndex];
                ManiaKeyPatternHelp.TryAddNoteFromCandidates(beatmap, new List<int> { chosenCol }, new List<double> { 1.0 }, rng, candidateTime);

                for (int rem = localCandidates.Count - 1; rem >= 0; rem--)
                {
                    if (Math.Abs(localCandidates[rem] - chosenCol) <= 1)
                    {
                        localCandidates.RemoveAt(rem);
                        localWeights?.RemoveAt(rem);
                    }
                }
            }
        }

        // Cut logic (来源于 ManiaModPatternShiftCut)
        private static void applyCutPattern(ManiaBeatmap beatmap,
                                            List<ManiaHitObject> windowObjects,
                                            HashSet<int> prevColumns,
                                            HashSet<int> nextColumns,
                                            double time,
                                            int minK,
                                            int maxK,
                                            Random rng,
                                            int maxIterationsPerWindow)
        {
            int totalColumns = Math.Max(1, beatmap.TotalColumns);
            var avoid = new HashSet<int>(prevColumns);
            avoid.UnionWith(nextColumns);

            var available = new List<int>();

            for (int i = 0; i < totalColumns; i++)
            {
                if (!avoid.Contains(i))
                    available.Add(i);
            }

            if (available.Count == 0)
                return;

            double beatLength = beatmap.ControlPointInfo.TimingPointAt(time).BeatLength;
            double quarter = beatLength / 4.0;

            bool isOnDivisionLocal(double t, int divisor)
            {
                if (divisor <= 0 || beatLength <= 0)
                    return false;

                double interval = beatLength / divisor;
                double mod = t % interval;
                return mod <= TIME_TOLERANCE || Math.Abs(interval - mod) <= TIME_TOLERANCE;
            }

            foreach (var obj in windowObjects)
            {
                bool isOn1To4 = isOnDivisionLocal(obj.StartTime, 1)
                                || isOnDivisionLocal(obj.StartTime, 2)
                                || isOnDivisionLocal(obj.StartTime, 3)
                                || isOnDivisionLocal(obj.StartTime, 4);
                if (!isOn1To4)
                    return;
            }

            int preferredDivisor = 2;

            foreach (var obj in windowObjects)
            {
                if (isOnDivisionLocal(obj.StartTime, 2))
                {
                    preferredDivisor = 2;
                    break;
                }

                if (isOnDivisionLocal(obj.StartTime, 3))
                {
                    preferredDivisor = 3;
                    break;
                }
            }

            double targetInterval = beatLength / preferredDivisor;

            double baseAligned = Math.Round(time / targetInterval) * targetInterval;
            double windowStart = windowObjects.Count > 0 ? windowObjects[0].StartTime : time;
            double windowEnd = windowObjects.Count > 0 ? windowObjects[^1].StartTime : time;

            foreach (var obj in windowObjects)
            {
                double qTime = Math.Round(obj.StartTime / quarter) * quarter;
                double prevTime = qTime - quarter;
                double nextTime = qTime + quarter;

                bool conflictPrev = ManiaKeyPatternHelp.HasNoteAtTime(beatmap, obj.Column, prevTime, null, TIME_TOLERANCE);
                bool conflictNext = ManiaKeyPatternHelp.HasNoteAtTime(beatmap, obj.Column, nextTime, null, TIME_TOLERANCE);

                if (!conflictPrev && !conflictNext)
                    continue;

                int? chosen = ManiaKeyPatternHelp.FindNearestAvailableColumnForQuarter(beatmap, obj.Column, qTime, prevTime, nextTime, totalColumns, TIME_TOLERANCE);
                if (chosen.HasValue)
                    obj.Column = chosen.Value;
            }

            double? chosenCandidate = null;
            int[] tries = { 0, -1, 1, -2, 2 };

            foreach (int off in tries)
            {
                double ct = baseAligned + (off * targetInterval);
                if (ct < windowStart - TIME_TOLERANCE || ct > windowEnd + TIME_TOLERANCE)
                    continue;

                bool onBeat = false;

                if (beatLength > 0)
                {
                    double modBeat = ct % beatLength;
                    onBeat = modBeat <= TIME_TOLERANCE || Math.Abs(beatLength - modBeat) <= TIME_TOLERANCE;
                }

                if (onBeat)
                    continue;

                var filteredTry = new List<int>();
                double prevTimeTry = ct - quarter;
                double nextTimeTry = ct + quarter;

                foreach (int col in available)
                {
                    if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, col, prevTimeTry, null, TIME_TOLERANCE))
                        continue;
                    if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, col, nextTimeTry, null, TIME_TOLERANCE))
                        continue;
                    if (ManiaKeyPatternHelp.IsHoldOccupyingColumn(beatmap, col, ct, null, TIME_TOLERANCE))
                        continue;

                    filteredTry.Add(col);
                }

                if (filteredTry.Count > 0)
                {
                    chosenCandidate = ct;
                    break;
                }

                if (chosenCandidate == null && !onBeat)
                    chosenCandidate = ct;
            }

            double candidateTime = chosenCandidate ?? baseAligned;

            double halfInterval = beatLength / 2.0;
            double thirdInterval = beatLength / 3.0;

            double snapTo(double t, double interval) => Math.Round(t / interval) * interval;
            double snappedHalf = snapTo(candidateTime, halfInterval);
            double snappedThird = snapTo(candidateTime, thirdInterval);

            bool isOnBeat(double t)
            {
                if (beatLength <= 0)
                    return false;

                double mod = t % beatLength;
                return mod <= TIME_TOLERANCE || Math.Abs(beatLength - mod) <= TIME_TOLERANCE;
            }

            bool isWithinWindow(double t) => t >= windowStart - TIME_TOLERANCE && t <= windowEnd + TIME_TOLERANCE;

            double distHalf = Math.Abs(candidateTime - snappedHalf);
            double distThird = Math.Abs(candidateTime - snappedThird);

            double? snappedCandidate = null;

            if (distHalf <= distThird)
            {
                if (!isOnBeat(snappedHalf) && isWithinWindow(snappedHalf))
                    snappedCandidate = snappedHalf;
                else if (!isOnBeat(snappedThird) && isWithinWindow(snappedThird))
                    snappedCandidate = snappedThird;
            }
            else
            {
                if (!isOnBeat(snappedThird) && isWithinWindow(snappedThird))
                    snappedCandidate = snappedThird;
                else if (!isOnBeat(snappedHalf) && isWithinWindow(snappedHalf))
                    snappedCandidate = snappedHalf;
            }

            if (snappedCandidate == null)
                return;

            candidateTime = snappedCandidate.Value;

            int count = rng.Next(minK, maxK + 1);
            count = Math.Clamp(count, 0, available.Count);
            count = Math.Min(count, Math.Max(0, maxIterationsPerWindow));

            if (avoid.Count == 0)
            {
                var globalObjectsLocal = beatmap.HitObjects.ToList();
                var existingColsAtTimeLocal = ManiaKeyPatternHelp.GetColumnsAtTime(globalObjectsLocal, candidateTime, TIME_TOLERANCE);

                var localCandidates0 = new List<int>(available);

                for (int i = 0; i < count && localCandidates0.Count > 0; i++)
                {
                    int pickedIndex = rng.Next(localCandidates0.Count);
                    int col = localCandidates0[pickedIndex];

                    bool adjacent = false;

                    foreach (int ec in existingColsAtTimeLocal)
                    {
                        if (Math.Abs(ec - col) <= 1)
                        {
                            adjacent = true;
                            break;
                        }
                    }

                    if (adjacent)
                    {
                        localCandidates0.RemoveAt(pickedIndex);
                        continue;
                    }

                    ManiaKeyPatternHelp.TryAddNoteFromCandidates(beatmap, new List<int> { col }, null, rng, candidateTime);

                    for (int rem = localCandidates0.Count - 1; rem >= 0; rem--)
                    {
                        if (Math.Abs(localCandidates0[rem] - col) <= 1)
                            localCandidates0.RemoveAt(rem);
                    }
                }

                return;
            }

            double median = ManiaKeyPatternHelp.GetMedianFromList(available);
            var weights = new List<double>(available.Count);

            for (int i = 0; i < available.Count; i++)
            {
                int column = available[i];
                weights.Add(Math.Max(0.001, Math.Abs(column - median)));
            }

            var filtered = new List<int>();

            foreach (int col in available)
            {
                double prevTime = candidateTime - quarter;
                double nextTime = candidateTime + quarter;

                if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, col, prevTime, null, TIME_TOLERANCE))
                    continue;

                if (ManiaKeyPatternHelp.HasNoteAtTime(beatmap, col, nextTime, null, TIME_TOLERANCE))
                    continue;

                if (ManiaKeyPatternHelp.IsHoldOccupyingColumn(beatmap, col, candidateTime, null, TIME_TOLERANCE))
                    continue;

                filtered.Add(col);
            }

            var useCandidates = filtered.Count > 0 ? filtered : available;

            List<double>? weightsForUse;

            if (useCandidates == available)
                weightsForUse = weights;
            else
            {
                weightsForUse = new List<double>(useCandidates.Count);

                for (int i = 0; i < useCandidates.Count; i++)
                {
                    int col = useCandidates[i];
                    int idx = available.IndexOf(col);
                    if (idx >= 0 && idx < weights.Count)
                        weightsForUse.Add(weights[idx]);
                    else
                        weightsForUse.Add(1.0);
                }
            }

            var globalObjects = beatmap.HitObjects.ToList();
            var existingColsAtTime = ManiaKeyPatternHelp.GetColumnsAtTime(globalObjects, candidateTime, TIME_TOLERANCE);

            var localCandidates = new List<int>(useCandidates);
            var localWeights = new List<double>(weightsForUse);

            for (int i = 0; i < count && localCandidates.Count > 0; i++)
            {
                int pickedIndex = -1;

                while (localCandidates.Count > 0)
                {
                    int idx = localWeights != null ? ManiaKeyPatternHelp.PickWeightedIndex(localWeights, rng) : rng.Next(localCandidates.Count);
                    int col = localCandidates[idx];

                    bool adjacentToExisting = false;

                    foreach (int ec in existingColsAtTime)
                    {
                        if (Math.Abs(ec - col) <= 1)
                        {
                            adjacentToExisting = true;
                            break;
                        }
                    }

                    if (!adjacentToExisting)
                    {
                        pickedIndex = idx;
                        break;
                    }

                    localCandidates.RemoveAt(idx);
                    localWeights?.RemoveAt(idx);
                }

                if (pickedIndex < 0)
                    break;

                int chosenCol = localCandidates[pickedIndex];
                ManiaKeyPatternHelp.TryAddNoteFromCandidates(beatmap, new List<int> { chosenCol }, new List<double> { 1.0 }, rng, candidateTime);

                for (int rem = localCandidates.Count - 1; rem >= 0; rem--)
                {
                    if (Math.Abs(localCandidates[rem] - chosenCol) <= 1)
                    {
                        localCandidates.RemoveAt(rem);
                        localWeights?.RemoveAt(rem);
                    }
                }
            }
        }
    }
}
