// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>Port of mania-hub <c>DanFeatureMetrics</c>.</summary>
    public sealed class EzDanFeatureMetrics
    {
        public int KeyCount { get; init; }
        public int NoteCount { get; init; }
        public double DurationMs { get; init; }
        public double HoldRatio { get; init; }
        public double ChordRatio { get; init; }
        public double TwoNoteChordRatio { get; init; }
        public double PeakNps1s { get; init; }
        public double PeakNps5s { get; init; }
        public double Nps5sP50 { get; init; }
        public double Nps5sP90 { get; init; }
        public double Nps5sP95 { get; init; }
        public double SustainedNps10s { get; init; }
        public double SustainedNps30s { get; init; }
        public double SustainedNps60s { get; init; }
        public double ActiveNps { get; init; }
        public double LongGapRatio { get; init; }
        public int LongGapCount { get; init; }
        public double JackPressure { get; init; }
        public double StreamPressure { get; init; }
        public double JumpstreamPressure { get; init; }
        public double ChordjackPressure { get; init; }
        public double ChordColumnOverlapRatio { get; init; }
        public double AdjacentColumnRehitShare { get; init; }
        public double TwoBackColumnRehitShare { get; init; }
        public double TwoBackColumnRehitExcess { get; init; }
        public double TechPressure { get; init; }
        public double RowBurstPressure { get; init; }
        public double FastRowRatio { get; init; }
        public double RowIntervalEntropy { get; init; }
        public double OffGridRowShare { get; init; }
        public double PatternVariety { get; init; }
        public double RowPatternEntropy { get; init; }
        public double RowPatternVariety { get; init; }
        public double RepeatedRowPatternRatio { get; init; }
        public double AlternatingRowPatternRatio { get; init; }
        public double RowPatternChangeRate { get; init; }
        public double RowMotifRepeatRatio { get; init; }
        public double RhythmMotifRepeatRatio { get; init; }
        public double AdjacentMotifRepeatRatio { get; init; }
        public double StrainSpikiness { get; init; }
        public double SustainedPressureRatio { get; init; }
        public double AnchorPressure { get; init; }
        public double LnReleasePressure { get; init; }
        public double LnDensity { get; init; }
        public double LnOverlapPressure { get; init; }
        public double LnChordPressure { get; init; }
        public double LnHoldDurationAvg { get; init; }
        public double LnHoldDurationP90 { get; init; }
        public double ChordSizeChangeRate { get; init; }
        public double DirectionChangeRate { get; init; }
        public double StaminaPressure { get; init; }
    }

    public sealed class EzDanFeatureExtractionResult
    {
        public IReadOnlyList<EzManiaNote> Notes { get; init; } = [];
        public IReadOnlyList<double> NoteTimes { get; init; } = [];
        public double DurationMs { get; init; }
        public IReadOnlyList<(double Time, IReadOnlyList<EzManiaNote> Notes)> OrderedRows { get; init; } = [];
        public EzDanFeatureMetrics Metrics { get; init; } = new();
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    public sealed class EzManiaPatternHit
    {
        public string Id { get; init; } = "";
        public string Label { get; init; } = "";
        public double Score { get; init; }
        public double Confidence { get; init; }
        public string Evidence { get; init; } = "";
    }

    public sealed class EzManiaPatternAnalysis
    {
        public int KeyCount { get; init; }
        public EzManiaPatternHit? Primary { get; init; }
        public IReadOnlyList<EzManiaPatternHit> Patterns { get; init; } = [];
        public IReadOnlyList<EzManiaPatternHit> AllPatterns { get; init; } = [];
        public EzDanFeatureMetrics Metrics { get; init; } = new();
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }
}
