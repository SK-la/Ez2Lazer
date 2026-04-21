// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.Mania.EzMania.Analysis.KeyPattern;
using osu.Game.Rulesets.Mania.EzMania.Analysis.SR4Pattern;
using osu.Game.Rulesets.Mania.Beatmaps;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis
{
    public class EzManiaAnalysisProvider : IEzAnalysisProvider
    {
        public bool TryCompute(in EzAnalysisRequest request, CancellationToken cancellationToken, out IEzAnalysis analysis)
        {
            analysis = null!;

            if (request.Beatmap is not ManiaBeatmap beatmap || request.RequestedScopes == EzAnalysisScope.None)
                return false;

            var bag = new EzAnalysisBag();
            bool hasXxySr = false;
            double xxySr = 0;

            if (request.RequestedScopes.HasFlag(EzAnalysisScope.XxySr) && tryCalculateXxySr(beatmap, request.ClockRate, out xxySr))
            {
                bag.Set(EzAnalysisFields.XXY_SR, xxySr);
                hasXxySr = true;
            }

            if (request.RequestedScopes.HasFlag(EzAnalysisScope.RulesetSpecificRadarData))
            {
                if (!hasXxySr && tryCalculateXxySr(beatmap, request.ClockRate, out double computedXxySr))
                {
                    xxySr = computedXxySr;
                    bag.Set(EzAnalysisFields.XXY_SR, xxySr);
                    hasXxySr = true;
                }

                bag.Set(EzAnalysisFields.RULESET_SPECIFIC_RADAR_RESULT, new EzRulesetSpecificRadarResult(
                    computeKeyPatternRadarData(beatmap, cancellationToken),
                    computeXxySrPatternRadarData(beatmap, request.ClockRate),
                    hasXxySr ? xxySr : null));
            }

            analysis = bag;
            return true;
        }

        private static bool tryCalculateXxySr(IBeatmap beatmap, double clockRate, out double sr)
        {
            sr = 0;

            if (!isXxySrPatternSupported(beatmap))
                return false;

            sr = SRCalculator.CalculateSR(beatmap, clockRate);
            return !double.IsNaN(sr) && !double.IsInfinity(sr);
        }

        private static EzRadarChartData<string> computeKeyPatternRadarData(ManiaBeatmap beatmap, CancellationToken cancellationToken)
        {
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

        private static EzRadarChartData<string> computeXxySrPatternRadarData(ManiaBeatmap beatmap, double clockRate)
        {
            if (!isXxySrPatternSupported(beatmap))
                return createXxySrPatternRadarData(0, 0, 0, 0, 0, 0);

            EzManiaSR4PatternSnapshot snapshot = SRCalculator.CalculateSR4PatternSnapshot(beatmap, clockRate);
            return EzManiaSR4PatternRadarBuilder.Create(snapshot);
        }

        private static bool isXxySrPatternSupported(IBeatmap beatmap)
        {
            int keyCount = beatmap is ManiaBeatmap maniaBeatmap && maniaBeatmap.TotalColumns > 0
                ? maniaBeatmap.TotalColumns
                : Math.Max(1, (int)Math.Round(beatmap.BeatmapInfo.Difficulty.CircleSize));

            return keyCount < 11 || keyCount % 2 == 0;
        }

        private static EzRadarChartData<string> createKeyPatternRadarData(double bracket, double chord, double longNoteRatio, double jack, double delay, double dump)
            => EzRadarChartData<string>.Create(
                new EzRadarAxisValue<string>("Bracket", bracket, "0.00"),
                new EzRadarAxisValue<string>("Chord", chord, "0.00"),
                new EzRadarAxisValue<string>("LN%", longNoteRatio, "0.00"),
                new EzRadarAxisValue<string>("Jack", jack, "0.00"),
                new EzRadarAxisValue<string>("Delay", delay, "0.00"),
                new EzRadarAxisValue<string>("Dump", dump, "0.00"));

        private static EzRadarChartData<string> createXxySrPatternRadarData(double bracketValue, double jackValue, double burst, double longNoteUsage, double longNoteRelease, double anchor)
            => EzRadarChartData<string>.Create(
                new EzRadarAxisValue<string>("Bracket", bracketValue, "0.00"),
                new EzRadarAxisValue<string>("Jack", jackValue, "0.00"),
                new EzRadarAxisValue<string>("Burst", burst, "0.00"),
                new EzRadarAxisValue<string>("LN Hold", longNoteUsage, "0.00"),
                new EzRadarAxisValue<string>("LN Release", longNoteRelease, "0.00"),
                new EzRadarAxisValue<string>("Anchor", anchor, "0.00"));
    }
}
