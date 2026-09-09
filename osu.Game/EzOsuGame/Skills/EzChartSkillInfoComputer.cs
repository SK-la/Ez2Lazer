// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Builds <see cref="EzChartSkillInfo"/> from note data (hub chart-analysis lean filing subset).
    /// Cluster fields stay null until LeoBlack lands.
    /// </summary>
    public static class EzChartSkillInfoComputer
    {
        private static readonly HashSet<string> ln_pattern_ids = new HashSet<string>(StringComparer.Ordinal)
        {
            "ln", "lngeneral", "lnrelease", "lninverse", "lntech"
        };

        public static EzChartSkillInfo Compute(
            EzManiaChartInput chart,
            IReadOnlyDictionary<string, double>? msdValues = null,
            double rate = 1)
        {
            ArgumentNullException.ThrowIfNull(chart);

            var features = EzDanFeatureExtractor.Extract(chart, rate);
            var patterns = EzManiaPatternAnalyzer.Analyze(chart, rate, features);
            var patternScores = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var hit in patterns.AllPatterns)
            {
                if (!patternScores.TryGetValue(hit.Id, out double existing) || hit.Score > existing)
                    patternScores[hit.Id] = hit.Score;
            }

            int keyCount = chart.KeyCount;
            double chordjackScore = patternScores.GetValueOrDefault("chordjack");
            double jackScore = patternScores.GetValueOrDefault("jack");
            double? jackShare = null;
            bool isJack = EzDanSkillsetFiling.ChartIsJack(keyCount, chordjackScore, jackScore, jackShare);
            bool vetoesTech = EzDanSkillsetFiling.JackVetoesTech(keyCount, chordjackScore, jackScore, jackShare);
            double lnRatio = features.Metrics.HoldRatio;
            bool chartIsLn = lnRatio >= EzDanAlgorithm.LnPrimaryMinRatioFor(keyCount);

            var patternIds = patternScores
                             .Where(kvp =>
                                 kvp.Value >= EzDanSkillsetFiling.PatternTagMinScore(kvp.Key)
                                 && !(kvp.Key == "tech" && vetoesTech)
                                 && !(ln_pattern_ids.Contains(kvp.Key) && !chartIsLn))
                             .Select(kvp => kvp.Key)
                             .ToList();

            if (EzDanSkillsetFiling.UsesPatternSkillAxes(keyCount) && isJack && !patternIds.Contains("jack"))
                patternIds.Add("jack");

            var jackDemand = EzFourKeyJackDemand.Classify(new EzFourKeyJackDemand.Input
            {
                KeyCount = keyCount,
                DurationMs = features.DurationMs,
                ChordRatio = features.Metrics.ChordRatio,
                ChordColumnOverlapRatio = features.Metrics.ChordColumnOverlapRatio,
                TwoBackColumnRehitExcess = features.Metrics.TwoBackColumnRehitExcess,
                JackPressure = features.Metrics.JackPressure,
                Patterns = patterns.AllPatterns
                                   .Select(p => new EzFourKeyJackDemand.PatternScore { Id = p.Id, Score = p.Score })
                                   .ToList(),
                Clusters = [],
            });

            var mina = EzDanSkillsetFiling.NormalizeMsdValues(msdValues);
            bool handstreamEndurance = keyCount == 4 && !chartIsLn && hasHandstreamEndurance(mina);
            double? lengthSeconds = features.DurationMs > 0 ? features.DurationMs / 1000.0 : chart.TotalLengthMs > 0 ? chart.TotalLengthMs / 1000.0 : null;

            return new EzChartSkillInfo
            {
                Patterns = patternIds.ToArray(),
                JackDemand = jackDemand.Detected,
                JackShare = null,
                StreamShare = null,
                TechCategory = null,
                ClusterTrill = null,
                HandstreamCluster = null,
                HandstreamEndurance = handstreamEndurance,
                TechScore = vetoesTech ? 0 : patternScores.GetValueOrDefault("tech"),
                ChordjackScore = chordjackScore,
                Motion = EzMotionFeaturesComputer.Compute(features.Notes, keyCount),
                LnRatio = lnRatio,
                Vibro = false,
                DanEligible = true,
                LengthSeconds = lengthSeconds,
                KeyCount = keyCount,
            };
        }

        public static EzManiaChartInput FromPlayable(IBeatmap playable)
        {
            ArgumentNullException.ThrowIfNull(playable);

            int keyCount = EzMinaNoteConverter.ResolveKeyCount(playable);
            var notes = new List<EzManiaNote>();
            double lastTime = 0;

            foreach (HitObject obj in playable.HitObjects)
            {
                if (obj is not IHasColumn columnObj)
                    continue;

                int column = columnObj.Column;
                if (column < 0 || column >= keyCount)
                    continue;

                bool isHold = obj is IHasDuration duration && duration.Duration > 0;
                double endTime = isHold && obj is IHasDuration d ? d.EndTime : obj.StartTime;
                notes.Add(new EzManiaNote(obj.StartTime, column, isHold, endTime));
                lastTime = Math.Max(lastTime, endTime);
            }

            var timingPoints = new List<EzManiaTimingPoint>();
            double bpm = 0;

            foreach (var point in playable.ControlPointInfo.TimingPoints)
            {
                if (point.BeatLength <= 0)
                    continue;

                timingPoints.Add(new EzManiaTimingPoint(point.Time, point.BeatLength));
                if (bpm <= 0)
                    bpm = 60000 / point.BeatLength;
            }

            if (bpm <= 0 && playable.ControlPointInfo.TimingPoints.Any())
            {
                var first = playable.ControlPointInfo.TimingPoints.First();
                if (first.BeatLength > 0)
                    bpm = 60000 / first.BeatLength;
            }

            double totalLength = playable.HitObjects.Count > 0
                ? Math.Max(lastTime, playable.HitObjects.Max(h => h.GetEndTime()))
                : 0;

            return new EzManiaChartInput
            {
                KeyCount = keyCount,
                Bpm = bpm,
                TotalLengthMs = totalLength,
                Notes = notes,
                TimingPoints = timingPoints,
            };
        }

        private static bool hasHandstreamEndurance(IReadOnlyDictionary<string, double> values)
        {
            string? top = EzDanSkillsetFiling.DominantSkillset(values);
            if (top != "Stamina")
                return false;

            string[] basePool = EzDanSkillsetFiling.SKILL_RATING_SKILLSETS
                                                   .Where(s => s is not ("Overall" or "Stamina"))
                                                   .ToArray();
            var picked = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (string skillset in basePool)
            {
                if (values.TryGetValue(skillset, out double v) && double.IsFinite(v) && v > 0)
                    picked[skillset] = v;
            }

            return EzDanSkillsetFiling.DominantSkillset(picked) == "Handstream";
        }
    }
}
