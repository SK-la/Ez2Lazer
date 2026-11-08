// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty;
using osu.Game.Rulesets.Mania.EzMania.Analysis.KeyPattern;
using osu.Game.Rulesets.Mania.EzMania.Analysis.SR4Pattern;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis
{
    public class EzManiaAnalysisProvider : IEzAnalysisProvider, IEzSunnyStarRatingSupport
    {
        public int SunnyStarRatingVersion => EzManiaSunnyStarRating.VERSION;

        public bool SupportsSunnyStarRating(IBeatmap beatmap) => EzManiaSunnyStarRating.IsPatternSupported(beatmap);

        public bool TryCompute(in EzAnalysisRequest request, CancellationToken cancellationToken, out IEzAnalysis analysis)
        {
            analysis = null!;

            if (request.Beatmap is not ManiaBeatmap beatmap || request.RequestedScopes == EzAnalysisScope.None)
                return false;

            var bag = new EzAnalysisBag();
            bool patternSupported = EzManiaSunnyStarRating.IsPatternSupported(beatmap);
            bool wantsSSR = request.RequestedScopes.HasFlag(EzAnalysisScope.SunnySR);
            bool wantsRadar = request.RequestedScopes.HasFlag(EzAnalysisScope.RulesetSpecificRadarData);

            double? sunnySR = null;
            EzManiaSR4PatternSnapshot? xxyPatternSnapshot = null;

            if (patternSupported && (wantsSSR || wantsRadar))
            {
                // The breakdown embeds the rating, so one derivation serves both scopes instead of running the
                // difficulty core once per requested value.
                var combined = SRCalculator.CalculateSRWithPatternSnapshot(beatmap, request.ClockRate);

                sunnySR = double.IsNaN(combined.Sr) || double.IsInfinity(combined.Sr) ? null : combined.Sr;
                xxyPatternSnapshot = combined.PatternSnapshot;
            }

            if (sunnySR is double resolvedSSR)
                bag.Set(EzAnalysisFields.SUNNY_SR, resolvedSSR);

            if (wantsRadar)
            {
                bag.Set(EzAnalysisFields.RULESET_SPECIFIC_RADAR_RESULT, new EzRulesetSpecificRadarResult(
                    computeKeyPatternRadarData(beatmap, request.Chart, cancellationToken),
                    xxyPatternSnapshot is { } snapshot ? EzManiaSR4PatternRadarBuilder.Create(snapshot) : createSSRPatternRadarData(0, 0, 0, 0, 0, 0),
                    sunnySR));
            }

            if (request.RequestedScopes.HasFlag(EzAnalysisScope.PerformancePoints))
            {
                if (tryCalculatePerformancePoints(beatmap, request.StarRatingOverride, out double pp))
                    bag.Set(EzAnalysisFields.PERFORMANCE_POINTS, pp);
            }

            analysis = bag;
            return true;
        }

        /// <summary>
        /// Mania pp at a given star rating. Mania performance reads only the star rating and the judgement counts,
        /// so a substituted rating can be scored without re-deriving the difficulty at all.
        /// </summary>
        private static bool tryCalculatePerformancePoints(ManiaBeatmap beatmap, double? starRating, out double pp)
        {
            pp = 0;

            if (starRating is not double star || !double.IsFinite(star) || star <= 0)
                return false;

            int totalHits = beatmap.HitObjects.Count;

            if (totalHits == 0)
                return false;

            var ruleset = new ManiaRuleset();

            var score = new ScoreInfo(null, ruleset.RulesetInfo)
            {
                Passed = true,
                Accuracy = 1,
                Statistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = totalHits },
                Mods = Array.Empty<Mod>(),
            };

            var performance = new ManiaPerformanceCalculator().Calculate(score, new ManiaDifficultyAttributes { StarRating = star });

            pp = performance.Total;
            return double.IsFinite(pp) && pp > 0;
        }

        private static EzRadarChartData<string> computeKeyPatternRadarData(ManiaBeatmap beatmap, EzManiaChartInput? chart, CancellationToken cancellationToken)
        {
            // The pass may already hold a lossless extraction of this chart. Rebuilding the row view from it is what
            // keeps the radar from walking the hit objects a second time; a filtered view falls back to the beatmap.
            if (chart is { FullyRepresented: true })
                return computeKeyPatternRadarDataFromExtraction(beatmap, chart, cancellationToken);

            var columnObjects = EzManiaKeyPatternHelper.GetColumnObjects(beatmap);

            if (columnObjects.Count == 0)
                return createKeyPatternRadarData(0, 0, 0, 0, 0, 0);

            var rows = EzManiaKeyPatternHelper.BuildRows(beatmap, columnObjects, cancellationToken);
            int totalColumns = columnObjects.Max(obj => obj.Column) + 1;

            return createKeyPatternRadarData(
                EzManiaKeyPatternBracket.Compute(rows, totalColumns, cancellationToken),
                EzManiaKeyPatternChord.Compute(rows, cancellationToken),
                EzManiaKeyPatternLongNote.Compute(beatmap),
                EzManiaKeyPatternJack.Compute(beatmap, columnObjects, cancellationToken),
                EzManiaKeyPatternDelay.Compute(rows, totalColumns, cancellationToken),
                EzManiaKeyPatternDump.Compute(rows, totalColumns, cancellationToken));
        }

        /// <summary>
        /// The radar from an extraction that is known to describe every hit object and timing point of
        /// <paramref name="beatmap"/>. Rows, columns and beat lengths are rebuilt exactly as the beatmap path does.
        /// </summary>
        private static EzRadarChartData<string> computeKeyPatternRadarDataFromExtraction(ManiaBeatmap beatmap, EzManiaChartInput chart, CancellationToken cancellationToken)
        {
            var notes = chart.Notes;

            if (notes.Count == 0)
                return createKeyPatternRadarData(0, 0, 0, 0, 0, 0);

            var columnObjects = EzManiaKeyPatternHelper.GetColumnObjects(chart);

            var rows = EzManiaKeyPatternHelper.BuildRows(columnObjects, time => beatLengthAt(chart, time), cancellationToken);

            int totalColumns = 0;

            for (int i = 0; i < columnObjects.Count; i++)
                totalColumns = Math.Max(totalColumns, columnObjects[i].Column + 1);

            return createKeyPatternRadarData(
                EzManiaKeyPatternBracket.Compute(rows, totalColumns, cancellationToken),
                EzManiaKeyPatternChord.Compute(rows, cancellationToken),
                EzManiaKeyPatternLongNote.Compute(chart),
                EzManiaKeyPatternJack.Compute(beatmap, columnObjects, cancellationToken),
                EzManiaKeyPatternDelay.Compute(rows, totalColumns, cancellationToken),
                EzManiaKeyPatternDump.Compute(rows, totalColumns, cancellationToken));
        }

        /// <summary>
        /// The uninherited beat length in effect at <paramref name="time"/>, matching
        /// <c>ControlPointInfo.TimingPointAt(time).BeatLength</c>: the last point at or before the time, otherwise the
        /// first. Zero when the extraction has no usable timing point.
        /// </summary>
        private static double beatLengthAt(EzManiaChartInput chart, double time)
        {
            var points = chart.TimingPoints;

            if (points.Count == 0)
                return chart.Bpm > 0 ? 60000 / chart.Bpm : 0;

            int low = 0;
            int high = points.Count;

            while (low < high)
            {
                int mid = (low + high) >> 1;

                if (points[mid].TimeMs <= time)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low > 0 ? points[low - 1].BeatLengthMs : points[0].BeatLengthMs;
        }

        private static EzRadarChartData<string> createKeyPatternRadarData(double bracket, double chord, double longNoteRatio, double jack, double delay, double dump)
            => EzRadarChartData<string>.Create(
                new EzRadarAxisValue<string>("Bracket", bracket, "0.00"),
                new EzRadarAxisValue<string>("Chord", chord, "0.00"),
                new EzRadarAxisValue<string>("LN%", longNoteRatio, "0.00"),
                new EzRadarAxisValue<string>("Jack", jack, "0.00"),
                new EzRadarAxisValue<string>("Delay", delay, "0.00"),
                new EzRadarAxisValue<string>("Dump", dump, "0.00"));

        private static EzRadarChartData<string> createSSRPatternRadarData(double bracketValue, double jackValue, double burst, double longNoteUsage, double longNoteRelease, double anchor)
            => EzRadarChartData<string>.Create(
                new EzRadarAxisValue<string>("Bracket", bracketValue, "0.00"),
                new EzRadarAxisValue<string>("Jack", jackValue, "0.00"),
                new EzRadarAxisValue<string>("Burst", burst, "0.00"),
                new EzRadarAxisValue<string>("LN Hold", longNoteUsage, "0.00"),
                new EzRadarAxisValue<string>("LN Release", longNoteRelease, "0.00"),
                new EzRadarAxisValue<string>("Anchor", anchor, "0.00"));
    }
}
