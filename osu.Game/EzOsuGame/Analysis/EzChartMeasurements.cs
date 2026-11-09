// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// One chart's note-level measurements, taken once and shared by every consumer of the same pass.
    /// </summary>
    /// <remarks>
    /// The structure pass, the Mina engine, the dan inputs and (through them) the axis table all need the same
    /// note list, key count and hold counts. Deriving each per consumer is what made a single analysis walk the
    /// hit objects a dozen times. The type is a carrier, not a cache: it lives for one pass.
    /// </remarks>
    internal readonly struct EzChartMeasurements
    {
        public readonly EzManiaChartInput Chart;

        /// <summary>Column objects carrying a duration (long notes).</summary>
        public readonly int HoldCount;

        public EzChartMeasurements(EzManiaChartInput chart, int holdCount)
        {
            Chart = chart;
            HoldCount = holdCount;
        }

        public int KeyCount => Chart.KeyCount;

        public double HoldRatio => Chart.Notes.Count > 0 ? (double)HoldCount / Chart.Notes.Count : 0;

        public static EzChartMeasurements FromPlayable(IBeatmap playable)
            => FromPlayable(playable, CancellationToken.None);

        public static EzChartMeasurements FromPlayable(IBeatmap playable, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(playable);

            var chart = EzCSIEngine.FromPlayable(playable, cancellationToken);
            int holds = 0;
            int scanned = 0;

            foreach (var note in chart.Notes)
            {
                if ((scanned++ & 0xFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                if (note.IsHold)
                    holds++;
            }

            return new EzChartMeasurements(chart, holds);
        }
    }
}
