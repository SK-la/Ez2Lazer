// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Port of mania-hub <c>motion-features.ts</c>. 4K wrist-versus-roll shares for the speed/tech model.
    /// </summary>
    public static class EzMotionFeaturesComputer
    {
        private const double ROW_EPSILON_MS = 10;
        private const double MIN_GAP_MS = 25;
        private const double MAX_GAP_MS = 400;
        private const int MIN_ROWS = 24;

        /// <summary>Null when the chart is not 4K or is too short to measure.</summary>
        public static EzMotionFeatures? Compute(IReadOnlyList<EzManiaNote> notes, int keyCount)
        {
            if (keyCount != 4 || notes.Count < 32)
                return null;

            var rows = buildRows(notes);
            if (rows.Count < MIN_ROWS)
                return null;

            double pairW = 0, sameHandW = 0, miniJackW = 0, chordSwingW = 0;
            double tripW = 0, trillW = 0, crossTrillW = 0;
            double quadW = 0, roll4W = 0;
            double rhythmW = 0, rhythmBreakW = 0;

            for (int i = 0; i + 1 < rows.Count; i++)
            {
                var a = rows[i];
                var b = rows[i + 1];
                double gap = b.Time - a.Time;
                if (gap <= 0 || gap > MAX_GAP_MS)
                    continue;

                double w = weightFor(gap);
                pairW += w;
                if (a.Columns.Count != b.Columns.Count)
                    chordSwingW += w;

                if (a.Columns.Count == 1 && b.Columns.Count == 1)
                {
                    int ca = a.Columns[0];
                    int cb = b.Columns[0];
                    if (ca == cb)
                        miniJackW += w;
                    else if (hand(ca) == hand(cb))
                        sameHandW += w;
                }
            }

            for (int i = 0; i + 2 < rows.Count; i++)
            {
                var a = rows[i];
                var b = rows[i + 1];
                var c = rows[i + 2];
                double g0 = b.Time - a.Time;
                double g1 = c.Time - b.Time;
                if (g0 <= 0 || g1 <= 0 || g0 > MAX_GAP_MS || g1 > MAX_GAP_MS)
                    continue;

                double w = weightFor(Math.Max(g0, g1));
                rhythmW += w;
                if (!ratioIsMusical(g1 / g0))
                    rhythmBreakW += w;

                if (a.Columns.Count != 1 || b.Columns.Count != 1 || c.Columns.Count != 1)
                    continue;

                tripW += w;
                int ca = a.Columns[0];
                int cb = b.Columns[0];
                int cc = c.Columns[0];

                if (ca == cc && ca != cb)
                {
                    if (hand(ca) == hand(cb))
                        trillW += w;
                    else
                        crossTrillW += w;
                }
            }

            for (int i = 0; i + 3 < rows.Count; i++)
            {
                var window = new[] { rows[i], rows[i + 1], rows[i + 2], rows[i + 3] };
                double[] gaps =
                {
                    window[1].Time - window[0].Time,
                    window[2].Time - window[1].Time,
                    window[3].Time - window[2].Time,
                };

                if (gaps.Any(gap => gap <= 0 || gap > MAX_GAP_MS))
                    continue;
                if (window.Any(row => row.Columns.Count != 1))
                    continue;

                double w = weightFor(gaps.Max());
                quadW += w;
                int[] columns = window.Select(row => row.Columns[0]).ToArray();
                int[] steps = { columns[1] - columns[0], columns[2] - columns[1], columns[3] - columns[2] };
                if (steps.All(step => step == 1) || steps.All(step => step == -1))
                    roll4W += w;
            }

            // chordSwing / oneHandTrill / roll4 are measured in hub but unused by the filing subset.
            _ = chordSwingW;
            _ = trillW;
            _ = roll4W;

            return new EzMotionFeatures
            {
                SameHand = round4(share(sameHandW, pairW)),
                MiniJack = round4(share(miniJackW, pairW)),
                CrossHandTrill = round4(share(crossTrillW, tripW)),
                RhythmBreak = round4(share(rhythmBreakW, rhythmW)),
            };
        }

        private static int hand(int column) => column < 2 ? 0 : 1;

        private static double weightFor(double gap) => 1 / Math.Max(gap, MIN_GAP_MS);

        private static double share(double numerator, double denominator)
            => denominator > 0 ? numerator / denominator : 0;

        private static double round4(double value) => Math.Round(value * 10000) / 10000;

        private static bool ratioIsMusical(double ratio)
        {
            foreach (double target in new[] { 1, 2, 0.5, 1.5, 2.0 / 3.0, 3, 1.0 / 3.0, 4, 0.25 })
            {
                if (Math.Abs(ratio - target) <= target * 0.08)
                    return true;
            }

            return false;
        }

        private static List<Row> buildRows(IReadOnlyList<EzManiaNote> notes)
        {
            var sorted = notes.OrderBy(n => n.TimeMs).ToList();
            var rows = new List<Row>();

            foreach (var note in sorted)
            {
                if (rows.Count > 0 && note.TimeMs - rows[^1].Time <= ROW_EPSILON_MS)
                    rows[^1].Columns.Add(note.Column);
                else
                    rows.Add(new Row(note.TimeMs, new List<int> { note.Column }));
            }

            foreach (var row in rows)
                row.Columns.Sort();

            return rows;
        }

        private sealed class Row
        {
            public Row(double time, List<int> columns)
            {
                Time = time;
                Columns = columns;
            }

            public double Time { get; }
            public List<int> Columns { get; }
        }
    }
}
