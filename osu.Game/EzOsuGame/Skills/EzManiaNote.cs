// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>Mania note for chart skill analysis (hub <c>ManiaNote</c>).</summary>
    public readonly record struct EzManiaNote(double TimeMs, int Column, bool IsHold, double EndTimeMs);

    /// <summary>Uninherited timing point (hub <c>ManiaTimingPoint</c>).</summary>
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
