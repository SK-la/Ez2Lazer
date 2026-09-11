// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills.LeoBlack
{
    internal enum EzLeoBlackDirection
    {
        None,
        Left,
        Right,
        Outwards,
        Inwards,
    }

    /// <summary>One primitive row between consecutive chart notes (hub <c>calculatePrimitives</c>).</summary>
    internal sealed class EzLeoBlackPrimitive
    {
        public int Index { get; init; }
        public double Time { get; init; }
        public double MsPerBeat { get; init; }
        public double BeatLength { get; init; }
        public int Notes { get; init; }
        public int Jacks { get; init; }
        public EzLeoBlackDirection Direction { get; init; }
        public bool Roll { get; init; }
        public int Keys { get; init; }
        public int LeftHandKeys { get; init; }
        public List<int> LNHeads { get; init; } = [];
        public List<int> LNBodies { get; init; } = [];
        public List<int> LNTails { get; init; } = [];
        public List<int> NormalNotes { get; init; } = [];
        public List<int> RawNotes { get; init; } = [];
    }

    /// <summary>Hub <c>patterns/primitives.js</c>.</summary>
    internal static class EzLeoBlackPrimitives
    {
        public static (EzLeoBlackDirection Direction, bool IsRoll) DetectDirection(IReadOnlyList<int> previousRow, IReadOnlyList<int> currentRow)
        {
            int pleftmost = previousRow[0];
            int prightmost = previousRow[^1];
            int cleftmost = currentRow[0];
            int crightmost = currentRow[^1];

            int leftmostChange = cleftmost - pleftmost;
            int rightmostChange = crightmost - prightmost;

            EzLeoBlackDirection direction = EzLeoBlackDirection.None;

            if (leftmostChange > 0)
                direction = rightmostChange > 0 ? EzLeoBlackDirection.Right : EzLeoBlackDirection.Inwards;
            else if (leftmostChange < 0)
                direction = rightmostChange < 0 ? EzLeoBlackDirection.Left : EzLeoBlackDirection.Outwards;
            else if (rightmostChange < 0)
                direction = EzLeoBlackDirection.Inwards;
            else if (rightmostChange > 0)
                direction = EzLeoBlackDirection.Outwards;

            bool isRoll = pleftmost > crightmost || prightmost < cleftmost;
            return (direction, isRoll);
        }

        public static List<EzLeoBlackPrimitive> CalculatePrimitives(EzLeoBlackChart chart)
        {
            if (chart.Notes.Count == 0)
                return [];

            double firstNote = chart.Notes[0].Time;
            int[] firstRow = chart.Notes[0].Data;

            var previousRow = new List<int>();

            for (int k = 0; k < chart.Keys; k++)
            {
                int n = firstRow[k];

                if (n is EzLeoBlackNoteType.NORMAL or EzLeoBlackNoteType.HOLDHEAD)
                    previousRow.Add(k);
            }

            if (previousRow.Count == 0)
                return [];

            double previousTime = firstNote;
            int index = 0;
            int leftHandKeys = keysOnLeftHand(chart.Keys);
            var outList = new List<EzLeoBlackPrimitive>();

            for (int i = 1; i < chart.Notes.Count; i++)
            {
                var item = chart.Notes[i];
                double t = item.Time;
                int[] row = item.Data;
                index++;

                var currentRow = new List<int>();
                var normalNotes = new List<int>();
                var lnHeads = new List<int>();
                var lnBodies = new List<int>();
                var lnTails = new List<int>();

                for (int k = 0; k < chart.Keys; k++)
                {
                    int n = row[k];

                    if (n is EzLeoBlackNoteType.NORMAL or EzLeoBlackNoteType.HOLDHEAD)
                        currentRow.Add(k);

                    if (n == EzLeoBlackNoteType.NORMAL)
                        normalNotes.Add(k);
                    else if (n == EzLeoBlackNoteType.HOLDHEAD)
                        lnHeads.Add(k);
                    else if (n == EzLeoBlackNoteType.HOLDBODY)
                        lnBodies.Add(k);
                    else if (n == EzLeoBlackNoteType.HOLDTAIL)
                        lnTails.Add(k);
                }

                if (currentRow.Count == 0 && lnHeads.Count == 0 && lnBodies.Count == 0 && lnTails.Count == 0)
                    continue;

                EzLeoBlackDirection direction = EzLeoBlackDirection.None;
                bool isRoll = false;
                int jacks = 0;

                if (currentRow.Count > 0)
                {
                    (direction, isRoll) = DetectDirection(previousRow, currentRow);
                    var prevSet = new HashSet<int>(previousRow);
                    jacks = 0;

                    for (int r = 0; r < currentRow.Count; r++)
                    {
                        if (prevSet.Contains(currentRow[r]))
                            jacks++;
                    }
                }

                outList.Add(new EzLeoBlackPrimitive
                {
                    Index = index,
                    Time = t - firstNote,
                    MsPerBeat = (t - previousTime) * 4.0,
                    BeatLength = beatLengthAt(chart, t),
                    Notes = currentRow.Count,
                    Jacks = jacks,
                    Direction = direction,
                    Roll = isRoll,
                    Keys = chart.Keys,
                    LeftHandKeys = leftHandKeys,
                    LNHeads = lnHeads,
                    LNBodies = lnBodies,
                    LNTails = lnTails,
                    NormalNotes = normalNotes,
                    RawNotes = currentRow,
                });

                if (currentRow.Count > 0)
                    previousRow = currentRow;

                previousTime = t;
            }

            return outList;
        }

        public static double LnPercent(EzLeoBlackChart chart)
        {
            int notes = 0;
            int lnotes = 0;

            foreach (var item in chart.Notes)
            {
                foreach (int n in item.Data)
                {
                    if (n == EzLeoBlackNoteType.NORMAL)
                        notes++;
                    else if (n == EzLeoBlackNoteType.HOLDHEAD)
                    {
                        notes++;
                        lnotes++;
                    }
                }
            }

            return notes > 0 ? (double)lnotes / notes : 0;
        }

        public static double SvTime(EzLeoBlackChart chart)
        {
            if (chart.SV.Count == 0)
                return 0;

            double total = 0;
            double time = chart.FirstNote;
            double vel = 1;
            int nonOneIntervals = 0;
            bool inNonOne = false;

            foreach (var sv in chart.SV)
            {
                double curVel = sv.Data;
                bool curNonOne = !double.IsFinite(curVel) || Math.Abs(curVel - 1) > EzLeoBlackConfig.SV_SPEED_EPS;

                if (!double.IsFinite(vel) || Math.Abs(vel - 1) > EzLeoBlackConfig.SV_SPEED_EPS)
                    total += sv.Time - time;

                if (curNonOne && !inNonOne)
                {
                    nonOneIntervals++;
                    inNonOne = true;
                }
                else if (!curNonOne)
                {
                    inNonOne = false;
                }

                vel = curVel;
                time = sv.Time;
            }

            if (!double.IsFinite(vel) || Math.Abs(vel - 1) > EzLeoBlackConfig.SV_SPEED_EPS)
                total += chart.LastNote - time;

            if (nonOneIntervals <= 1)
                return 0;

            bool extreme = false;
            var bpms = chart.BPM;

            if (bpms.Count >= 1)
            {
                double? prevMsPerBeat = null;

                foreach (var item in bpms)
                {
                    double msPerBeat = item.Data.MsPerBeat;

                    if (!double.IsFinite(msPerBeat) || msPerBeat <= 0)
                    {
                        extreme = true;
                        break;
                    }

                    double bpm = 60000.0 / msPerBeat;

                    if (bpm <= EzLeoBlackConfig.SV_EXTREME_BPM_MIN || bpm >= EzLeoBlackConfig.SV_EXTREME_BPM_MAX)
                    {
                        extreme = true;
                        break;
                    }

                    if (prevMsPerBeat is double prev && prev > 0)
                    {
                        double ratio = Math.Max(prev / msPerBeat, msPerBeat / prev);

                        if (ratio >= EzLeoBlackConfig.SV_EXTREME_BPM_RATIO)
                        {
                            extreme = true;
                            break;
                        }
                    }

                    prevMsPerBeat = msPerBeat;
                }
            }

            if (extreme)
                return Math.Max(total, EzLeoBlackConfig.SV_AMOUNT_THRESHOLD + 1.0);

            return total;
        }

        private static int keysOnLeftHand(int keymode) => keymode switch
        {
            3 => 2,
            4 => 2,
            5 => 3,
            6 => 3,
            7 => 4,
            8 => 4,
            9 => 5,
            10 => 5,
            _ => Math.Max(1, keymode / 2),
        };

        private static double beatLengthAt(EzLeoBlackChart chart, double time)
        {
            if (chart.BPM.Count == 0)
                return 500;

            double current = chart.BPM[0].Data.MsPerBeat;

            foreach (var item in chart.BPM)
            {
                if (item.Time > time)
                    break;

                current = item.Data.MsPerBeat;
            }

            return current;
        }
    }
}
