// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills.SkillTag
{
    /// <summary>Mania note for chart structure analysis (column, span, timing).</summary>
    public readonly record struct EzManiaNote(double TimeMs, int Column, bool IsHold, double EndTimeMs);

    /// <summary>Uninherited timing point.</summary>
    public readonly record struct EzManiaTimingPoint(double TimeMs, double BeatLengthMs);

    /// <summary>Minimal chart input for feature / pattern / motion analysis.</summary>
    public sealed class EzManiaChartInput
    {
        public int KeyCount { get; init; }

        public double Bpm { get; init; }

        public double TotalLengthMs { get; init; }

        public IReadOnlyList<EzManiaNote> Notes { get; init; } = [];

        public IReadOnlyList<EzManiaTimingPoint> TimingPoints { get; init; } = [];
    }
}
