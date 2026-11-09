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

        /// <summary>
        /// True when <see cref="Notes"/> and <see cref="TimingPoints"/> describe every hit object and timing point of
        /// the beatmap they were extracted from.
        /// </summary>
        /// <remarks>
        /// Consumers that would otherwise re-walk the source beatmap may use this input only when it is lossless.
        /// It defaults to false because an input built by hand (tests, synthetic charts) has no such guarantee, and
        /// silently treating a filtered view as complete is how an analyzer ends up reading a different chart.
        /// </remarks>
        public bool FullyRepresented { get; init; }
    }
}
