// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace osu.Game.EzOsuGame.Skills.LeoBlack
{
    /// <summary>Hub <c>patterns/patternsDef.js</c> core names + rating multipliers.</summary>
    internal static class EzLeoBlackCorePattern
    {
        public const string Stream = "Stream";
        public const string Chordstream = "Chordstream";
        public const string Jacks = "Jacks";
        public const string Coordination = "Coordination";
        public const string Density = "Density";
        public const string Wildcard = "Wildcard";

        public static readonly string[] List =
        [
            Stream, Chordstream, Jacks, Coordination, Density, Wildcard
        ];
    }

    internal delegate int EzLeoBlackPatternMatcher(IReadOnlyList<EzLeoBlackPrimitive> xs);

    internal sealed class EzLeoBlackSpecificPatterns
    {
        public required List<(string Name, EzLeoBlackPatternMatcher Match)> Stream { get; init; }
        public required List<(string Name, EzLeoBlackPatternMatcher Match)> Chordstream { get; init; }
        public required List<(string Name, EzLeoBlackPatternMatcher Match)> Jack { get; init; }
        public required List<(string Name, EzLeoBlackPatternMatcher Match)> Coordination { get; init; }
        public required List<(string Name, EzLeoBlackPatternMatcher Match)> Density { get; init; }
        public required List<(string Name, EzLeoBlackPatternMatcher Match)> Wildcard { get; init; }
    }

    internal sealed class EzLeoBlackFoundPattern
    {
        public required string Pattern { get; init; }
        public string? SpecificType { get; init; }
        public bool Mixed { get; init; }
        public double Start { get; init; }
        public double End { get; init; }
        public double MsPerBeat { get; init; }
    }

    /// <summary>Hub <c>patternsDef.js</c> matchers + <c>findPatterns.js</c>.</summary>
    internal static class EzLeoBlackFindPatterns
    {
        private const int MATCHER_WINDOW = 8;

        public static double ResolveRatingMultiplier(string pattern, string? specificType, string modeTag = "Mix")
        {
            var lnCore = new HashSet<string>(StringComparer.Ordinal)
            {
                EzLeoBlackCorePattern.Coordination, EzLeoBlackCorePattern.Density, EzLeoBlackCorePattern.Wildcard
            };
            var rcCore = new HashSet<string>(StringComparer.Ordinal)
            {
                EzLeoBlackCorePattern.Stream, EzLeoBlackCorePattern.Chordstream, EzLeoBlackCorePattern.Jacks
            };

            double defaultMultiplier = EzLeoBlackConfig.CORE_RATING_MULTIPLIER.TryGetValue(pattern, out double d) ? d : 1.0;

            if (!EzLeoBlackConfig.SUBTYPE_RATING_MULTIPLIER_BY_MODE.TryGetValue(modeTag, out var subtypeMap))
                subtypeMap = EzLeoBlackConfig.SUBTYPE_RATING_MULTIPLIER_BY_MODE["Mix"];

            double value = specificType == null
                ? defaultMultiplier
                : (subtypeMap.TryGetValue(specificType, out double s) ? s : defaultMultiplier);

            if (modeTag == "RC" && lnCore.Contains(pattern))
            {
                var mixMap = EzLeoBlackConfig.SUBTYPE_RATING_MULTIPLIER_BY_MODE["Mix"];
                double bas = specificType == null
                    ? defaultMultiplier
                    : (mixMap.TryGetValue(specificType, out double m) ? m : defaultMultiplier);
                value = bas * EzLeoBlackConfig.RC_LN_CORE_SCALE;
            }

            if (modeTag == "LN" && rcCore.Contains(pattern))
                value *= EzLeoBlackConfig.RC_CORE_LN_SCALE;

            return value;
        }

        public static List<EzLeoBlackFoundPattern> Find(EzLeoBlackChart chart)
            => Find(chart, CancellationToken.None);

        public static List<EzLeoBlackFoundPattern> Find(EzLeoBlackChart chart, CancellationToken cancellationToken)
        {
            var primitives = EzLeoBlackPrimitives.CalculatePrimitives(chart, cancellationToken);
            EzLeoBlackSpecificPatterns keymodePatterns = chart.Keys switch
            {
                4 => four_key_patterns,
                7 => seven_key_patterns,
                _ => other_key_patterns,
            };

            return matches(keymodePatterns, chart.LastNote - chart.FirstNote, primitives);
        }

        private static List<(string, EzLeoBlackPatternMatcher)> reorderSpecific(
            List<(string Name, EzLeoBlackPatternMatcher Match)> items,
            string[] preferredOrder)
        {
            if (items.Count <= 1 || preferredOrder.Length == 0)
                return items;

            var orderRank = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < preferredOrder.Length; i++)
                orderRank[preferredOrder[i]] = i;

            return items
                   .Select((item, index) => (item, index))
                   .OrderBy(x => orderRank.TryGetValue(x.item.Name, out int r) ? r : orderRank.Count)
                   .ThenBy(x => x.index)
                   .Select(x => x.item)
                   .ToList();
        }

        private static bool sameCols(IReadOnlyList<int> a, IReadOnlyList<int> b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        private static bool containsCol(IReadOnlyList<int> cols, int x)
        {
            for (int i = 0; i < cols.Count; i++)
            {
                if (cols[i] == x)
                    return true;
            }

            return false;
        }

        private static EzLeoBlackPrimitive asHeadPointRow(EzLeoBlackPrimitive row, IReadOnlyList<int> previousHeadCols, EzLeoBlackPrimitive target)
        {
            var headCols = row.LNHeads;
            int jacks = 0;

            if (headCols.Count > 0)
            {
                for (int i = 0; i < headCols.Count; i++)
                {
                    if (containsCol(previousHeadCols, headCols[i]))
                        jacks++;
                }
            }

            EzLeoBlackDirection direction = EzLeoBlackDirection.None;
            bool roll = false;

            if (previousHeadCols.Count > 0 && headCols.Count > 0)
                (direction, roll) = EzLeoBlackPrimitives.DetectDirection(previousHeadCols, headCols);

            target.Index = row.Index;
            target.Time = row.Time;
            target.MsPerBeat = row.MsPerBeat;
            target.BeatLength = row.BeatLength;
            target.Notes = headCols.Count;
            target.Jacks = jacks;
            target.Direction = direction;
            target.Roll = roll;
            target.Keys = row.Keys;
            target.LeftHandKeys = row.LeftHandKeys;
            target.LNHeads = row.LNHeads;
            target.LNBodies = row.LNBodies;
            target.LNTails = row.LNTails;
            target.NormalNotes = EzLeoBlackPrimitive.EMPTY_COLUMNS;
            target.RawNotes = headCols;

            return target;
        }

        /// <summary>
        /// The head-point view of the first <paramref name="n"/> rows. Writes into the thread's reusable buffer: every
        /// consumer reads the result and returns, so no caller may hold it past its own call.
        /// </summary>
        private static List<EzLeoBlackPrimitive> headRows(IReadOnlyList<EzLeoBlackPrimitive> xs, int n)
        {
            var scratch = ThreadScratch;
            List<EzLeoBlackPrimitive> rows = scratch.BeginHeadRows();
            IReadOnlyList<int> prev = EzLeoBlackPrimitive.EMPTY_COLUMNS;

            int limit = Math.Min(n, xs.Count);

            for (int i = 0; i < limit; i++)
            {
                var hr = asHeadPointRow(xs[i], prev, scratch.NextHeadPrimitive());
                rows.Add(hr);

                if (hr.RawNotes.Count > 0)
                    prev = hr.RawNotes;
            }

            return rows;
        }

        private static bool isSameHandAdjacent(int colA, int colB, int split)
        {
            if (Math.Abs(colA - colB) != 1)
                return false;

            return (colA < split) == (colB < split);
        }

        private static double jackBpm(double deltaMs)
        {
            if (deltaMs <= 0)
                return 230;

            return Math.Min(15000 / deltaMs, 230);
        }

        private static bool isLnHeadContext(IReadOnlyList<EzLeoBlackPrimitive> xs)
            => xs.Count > 0 && xs[0].LNHeads.Count > 0;

        private static bool hasLnContext(IReadOnlyList<EzLeoBlackPrimitive> xs, int window)
        {
            int limit = Math.Min(window, xs.Count);

            for (int i = 0; i < limit; i++)
            {
                var row = xs[i];

                if (row.LNHeads.Count > 0 || row.LNBodies.Count > 0 || row.LNTails.Count > 0)
                    return true;
            }

            return false;
        }

        private static bool inverseReady(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 5)
                return false;

            for (int i = 0; i < 5; i++)
            {
                if (xs[i].NormalNotes.Count > 0)
                    return false;
            }

            int maxBodies = 0;

            for (int i = 0; i < 5; i++)
                maxBodies = Math.Max(maxBodies, xs[i].LNBodies.Count);

            if (maxBodies < EzLeoBlackConfig.INVERSE_MIN_FILLED_LANES)
                return false;

            var scratch = ThreadScratch;
            List<double> gaps = scratch.InverseGaps;
            gaps.Clear();

            for (int i = 0; i < 4; i++)
            {
                if (xs[i].LNTails.Count > 0 && xs[i + 1].LNHeads.Count > 0)
                    gaps.Add(xs[i + 1].Time - xs[i].Time);
            }

            if (gaps.Count < 2)
                return false;

            double max = gaps[0];
            double min = gaps[0];

            for (int i = 1; i < gaps.Count; i++)
            {
                max = Math.Max(max, gaps[i]);
                min = Math.Min(min, gaps[i]);
            }

            return max - min <= EzLeoBlackConfig.INVERSE_GAP_TOLERANCE_MS;
        }

        #region Core matchers

        private static int coreStream(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 5)
                return 0;

            var a = xs[0];
            var b = xs[1];
            var c = xs[2];
            var d = xs[3];
            var e = xs[4];

            if (a.Notes == 1 && a.Jacks == 0
                && b.Notes == 1 && b.Jacks == 0
                && c.Notes == 1 && c.Jacks == 0
                && d.Notes == 1 && d.Jacks == 0
                && e.Notes == 1 && e.Jacks == 0
                && a.RawNotes[0] != e.RawNotes[0])
            {
                return 5;
            }

            return 0;
        }

        private static int coreJacks(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count == 0)
                return 0;

            var x0 = xs[0];
            return x0.Jacks > 1 && x0.MsPerBeat < 2000 ? 1 : 0;
        }

        private static int coreChordstream(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 4)
                return 0;

            var a = xs[0];
            var b = xs[1];
            var c = xs[2];
            var d = xs[3];

            if (a.Notes > 1 && a.Jacks == 0 && b.Jacks == 0 && c.Jacks == 0 && d.Jacks == 0
                && (b.Notes > 1 || c.Notes > 1 || d.Notes > 1))
            {
                return 4;
            }

            return 0;
        }

        private static int coreCoordination(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count == 0)
                return 0;

            var a = xs[0];
            return a.LNHeads.Count > 0 || a.LNBodies.Count > 0 || a.LNTails.Count > 0 ? 1 : 0;
        }

        private static int coreDensity(IReadOnlyList<EzLeoBlackPrimitive> xs)
            => xs.Count > 0 && isLnHeadContext(xs) ? 1 : 0;

        private static int coreWildcard(IReadOnlyList<EzLeoBlackPrimitive> xs)
            => xs.Count > 0 && isLnHeadContext(xs) ? 1 : 0;

        #endregion

        #region Jack / chordstream / stream specifics

        private static int jacksChordjacks(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 2)
                return 0;

            var a = xs[0];
            var b = xs[1];

            if (a.Notes > 2 && b.Notes > 1 && b.Jacks >= 1 && (b.Notes < a.Notes || b.Jacks < b.Notes))
                return 2;

            return 0;
        }

        private static int jacksMinijacks(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 2)
                return 0;

            return xs[0].Jacks > 0 && xs[1].Jacks == 0 ? 2 : 0;
        }

        private static int jacksLongjacks(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 5)
                return 0;

            var a = xs[0];
            var b = xs[1];
            var c = xs[2];
            var d = xs[3];
            var e = xs[4];

            if (a.Jacks > 0 && b.Jacks > 0 && c.Jacks > 0 && d.Jacks > 0 && e.Jacks > 0)
            {
                foreach (int x in a.RawNotes)
                {
                    if (containsCol(b.RawNotes, x) && containsCol(c.RawNotes, x)
                        && containsCol(d.RawNotes, x) && containsCol(e.RawNotes, x))
                    {
                        return 5;
                    }
                }
            }

            return 0;
        }

        private static int jacks4kQuadstream(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 4)
                return 0;

            return xs[0].Notes == 4 && xs[2].Jacks == 0 && xs[3].Jacks == 0 ? 4 : 0;
        }

        private static int jacks4kGluts(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 3)
                return 0;

            var a = xs[0];
            var b = xs[1];
            var c = xs[2];

            if (b.Jacks == 1 && c.Jacks == 1)
            {
                foreach (int x in a.RawNotes)
                {
                    if (containsCol(b.RawNotes, x) && containsCol(c.RawNotes, x))
                        return 0;
                }

                return 3;
            }

            return 0;
        }

        private static int chordstream4kHandstream(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 4)
                return 0;

            var a = xs[0];
            return a.Notes == 3 && a.Jacks == 0 && xs[1].Jacks == 0 && xs[2].Jacks == 0 && xs[3].Jacks == 0 ? 4 : 0;
        }

        private static int chordstream4kJumpstream(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 4)
                return 0;

            var a = xs[0];
            var b = xs[1];
            var c = xs[2];
            var d = xs[3];

            if (a.Notes == 2 && a.Jacks == 0 && b.Notes == 1 && b.Jacks == 0 && c.Jacks == 0 && d.Jacks == 0
                && c.Notes < 3 && d.Notes < 3)
            {
                return 4;
            }

            return 0;
        }

        private static int chordstream4kJumptrill(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 4)
                return 0;

            var a = xs[0];
            var b = xs[1];
            var c = xs[2];
            var d = xs[3];
            return a.Notes == 2 && b.Notes == 2 && c.Notes == 2 && d.Notes == 2 && b.Roll && c.Roll && d.Roll ? 4 : 0;
        }

        private static int chordstream4kSplittrill(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 3)
                return 0;

            var a = xs[0];
            var b = xs[1];
            var c = xs[2];
            return a.Notes == 2 && b.Notes == 2 && c.Notes == 2 && b.Jacks == 0 && c.Jacks == 0 && !b.Roll && !c.Roll ? 3 : 0;
        }

        private static int stream4kRoll(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 3)
                return 0;

            var a = xs[0];
            var b = xs[1];
            var c = xs[2];

            if (a.Notes == 1 && b.Notes == 1 && c.Notes == 1)
            {
                bool left = a.Direction == EzLeoBlackDirection.Left && b.Direction == EzLeoBlackDirection.Left && c.Direction == EzLeoBlackDirection.Left;
                bool right = a.Direction == EzLeoBlackDirection.Right && b.Direction == EzLeoBlackDirection.Right && c.Direction == EzLeoBlackDirection.Right;

                if (left || right)
                    return 3;
            }

            return 0;
        }

        private static int stream4kTrill(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 4)
                return 0;

            var a = xs[0];
            var b = xs[1];
            var c = xs[2];
            var d = xs[3];

            if (b.Jacks == 0 && c.Jacks == 0 && d.Jacks == 0
                && sameCols(a.RawNotes, c.RawNotes) && sameCols(b.RawNotes, d.RawNotes))
            {
                return 4;
            }

            return 0;
        }

        private static int stream4kMinitrill(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 4)
                return 0;

            var a = xs[0];
            var b = xs[1];
            var c = xs[2];
            var d = xs[3];

            if (b.Jacks == 0 && c.Jacks == 0
                && sameCols(a.RawNotes, c.RawNotes) && !sameCols(b.RawNotes, d.RawNotes))
            {
                return 4;
            }

            return 0;
        }

        private static int chordstream7kDoubleStreams(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 2)
                return 0;

            var a = xs[0];
            var b = xs[1];
            return a.Notes == 2 && b.Notes == 2 && b.Jacks == 0 && !b.Roll ? 2 : 0;
        }

        private static int chordstream7kDenseChordstream(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 2)
                return 0;

            return xs[0].Notes > 1 && xs[1].Notes > 1 && xs[1].Jacks == 0 ? 2 : 0;
        }

        private static int chordstream7kLightChordstream(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 2)
                return 0;

            return xs[0].Notes > 1 && xs[1].Notes == 1 && xs[1].Jacks == 0 ? 2 : 0;
        }

        private static int chordstream7kChordRoll(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 3)
                return 0;

            var a = xs[0];
            var b = xs[1];
            var c = xs[2];

            if (a.Notes > 1 && b.Notes > 1 && c.Notes > 1 && b.Roll && c.Roll
                && ((b.Direction == EzLeoBlackDirection.Left && c.Direction == EzLeoBlackDirection.Left)
                    || (b.Direction == EzLeoBlackDirection.Right && c.Direction == EzLeoBlackDirection.Right)))
            {
                return 3;
            }

            return 0;
        }

        private static int chordstream7kBrackets(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 3)
                return 0;

            var a = xs[0];
            var b = xs[1];
            var c = xs[2];

            if (a.Notes > 2 && b.Notes > 2 && c.Notes > 2 && !b.Roll && !c.Roll && b.Jacks == 0 && c.Jacks == 0
                && a.Notes + b.Notes + c.Notes > 9)
            {
                return 3;
            }

            return 0;
        }

        #endregion

        #region LN coordination / density / wildcard

        private static int coordinationColumnLock(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 3)
                return 0;

            int split = xs[0].LeftHandKeys;
            int? lnCol = xs[0].LNHeads.Count > 0 ? xs[0].LNHeads[0] : null;

            if (lnCol == null)
                return 0;

            var scratch = ThreadScratch;
            List<int> adjCols = scratch.AdjacentColumns;
            adjCols.Clear();

            int lower = lnCol.Value - 1;
            int upper = lnCol.Value + 1;

            if (lower >= 0 && lower < xs[0].Keys && isSameHandAdjacent(lnCol.Value, lower, split))
                adjCols.Add(lower);

            if (upper >= 0 && upper < xs[0].Keys && isSameHandAdjacent(lnCol.Value, upper, split))
                adjCols.Add(upper);

            if (adjCols.Count == 0)
                return 0;

            int scan = Math.Min(8, xs.Count);

            for (int a = 0; a < adjCols.Count; a++)
            {
                int adj = adjCols[a];
                List<double> hits = scratch.HitTimes;
                hits.Clear();

                for (int i = 0; i < scan; i++)
                {
                    var row = xs[i];

                    if (containsCol(row.LNBodies, lnCol.Value) && containsCol(row.NormalNotes, adj))
                        hits.Add(row.Time);
                }

                if (hits.Count < 3)
                    continue;

                double maxBpm = 0;

                for (int i = 0; i < hits.Count - 1; i++)
                    maxBpm = Math.Max(maxBpm, jackBpm(hits[i + 1] - hits[i]));

                if (maxBpm >= EzLeoBlackConfig.JACKY_MIN_BPM)
                    return 3;
            }

            return 0;
        }

        private static int coordinationShield(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 2)
                return 0;

            var a = xs[0];
            var b = xs[1];
            double dt = b.Time - a.Time;
            double beatLimit = b.BeatLength * EzLeoBlackConfig.SHIELD_MAX_BEAT_RATIO;

            if (dt < 0 || dt > beatLimit)
                return 0;

            foreach (int col in a.NormalNotes)
            {
                if (containsCol(b.LNHeads, col))
                    return 2;
            }

            foreach (int col in a.LNTails)
            {
                if (containsCol(b.NormalNotes, col))
                    return 2;
            }

            return 0;
        }

        private static int coordinationRelease(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < EzLeoBlackConfig.RELEASE_MIN_TAIL_ROWS)
                return 0;

            if (coordinationShield(xs) != 0)
                return 0;

            if (inverseReady(xs))
                return 0;

            if (wildcardJack(xs) != 0)
                return 0;

            var scratch = ThreadScratch;
            List<EzLeoBlackPrimitive> pickedRows = scratch.PickedRows;
            pickedRows.Clear();
            int scan = Math.Min(EzLeoBlackConfig.RELEASE_SCAN_ROWS, xs.Count);

            for (int i = 0; i < scan; i++)
            {
                if (xs[i].LNTails.Count == 1)
                    pickedRows.Add(xs[i]);
            }

            if (pickedRows.Count < EzLeoBlackConfig.RELEASE_MIN_TAIL_ROWS)
                return 0;

            int useRows = Math.Min(EzLeoBlackConfig.RELEASE_FULL_MATCH_ROWS, pickedRows.Count);
            List<EzLeoBlackPrimitive> rows = scratch.BeginReleaseRows();

            List<int> prev = scratch.ReleaseColumns[0];
            prev.Clear();
            prev.Add(pickedRows[0].LNTails[0]);

            for (int i = 0; i < useRows; i++)
            {
                var row = pickedRows[i];
                List<int> cur = scratch.ReleaseColumns[i + 1];
                cur.Clear();
                cur.Add(row.LNTails[0]);
                var (direction, roll) = EzLeoBlackPrimitives.DetectDirection(prev, cur);

                var releaseRow = scratch.NextReleasePrimitive();
                releaseRow.Index = row.Index;
                releaseRow.Time = row.Time;
                releaseRow.MsPerBeat = row.MsPerBeat;
                releaseRow.BeatLength = row.BeatLength;
                releaseRow.Notes = 1;
                releaseRow.Jacks = cur[0] == prev[0] ? 1 : 0;
                releaseRow.Direction = direction;
                releaseRow.Roll = roll;
                releaseRow.Keys = row.Keys;
                releaseRow.LeftHandKeys = row.LeftHandKeys;
                releaseRow.LNHeads = row.LNHeads;
                releaseRow.LNBodies = row.LNBodies;
                releaseRow.LNTails = row.LNTails;
                releaseRow.NormalNotes = EzLeoBlackPrimitive.EMPTY_COLUMNS;
                releaseRow.RawNotes = cur;

                rows.Add(releaseRow);
                prev = cur;
            }

            // Effective rows are everything after the first, which is what the roll check reads.
            if (rows.Count - 1 < EzLeoBlackConfig.RELEASE_ROLL_POINTS)
                return 0;

            // hub RELEASE_ROLL_POINTS is 2, so the >=3 STREAM_4K_ROLL arm never runs.
            int aCol = rows[1].RawNotes[0];
            int bCol = rows.Count > 2 ? rows[2].RawNotes[0] : aCol;
            double dt = rows.Count > 2 ? rows[2].Time - rows[1].Time : 0;
            bool matched = aCol != bCol && dt > 0;

            if (matched)
                return useRows >= EzLeoBlackConfig.RELEASE_FULL_MATCH_ROWS ? 5 : 4;

            return 0;
        }

        private static int density4kJumpstream(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 4 || !isLnHeadContext(xs))
                return 0;

            return chordstream4kJumpstream(headRows(xs, 4)) != 0 ? 4 : 0;
        }

        private static int density4kHandstream(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 4 || !isLnHeadContext(xs))
                return 0;

            return chordstream4kHandstream(headRows(xs, 4)) != 0 ? 4 : 0;
        }

        private static int densityInverse(IReadOnlyList<EzLeoBlackPrimitive> xs)
            => inverseReady(xs) ? 5 : 0;

        private static int density7kDoubleStreams(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 2 || !isLnHeadContext(xs))
                return 0;

            return chordstream7kDoubleStreams(headRows(xs, 2)) != 0 ? 2 : 0;
        }

        private static int density7kDenseChordstream(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 2 || !isLnHeadContext(xs))
                return 0;

            return chordstream7kDenseChordstream(headRows(xs, 2)) != 0 ? 2 : 0;
        }

        private static int density7kLightChordstream(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 2 || !isLnHeadContext(xs))
                return 0;

            return chordstream7kLightChordstream(headRows(xs, 2)) != 0 ? 2 : 0;
        }

        private static int wildcardJack(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 2 || !hasLnContext(xs, EzLeoBlackConfig.JACKY_CONTEXT_WINDOW))
                return 0;

            int take = Math.Max(4, EzLeoBlackConfig.JACKY_CONTEXT_WINDOW);
            var scratch = ThreadScratch;
            List<EzLeoBlackPrimitive> rows = scratch.WildcardRows;
            rows.Clear();

            for (int i = 0; i < Math.Min(take, xs.Count); i++)
            {
                if (xs[i].Notes > 0)
                    rows.Add(xs[i]);
            }

            if (rows.Count < 2)
                return 0;

            if (jacksChordjacks(rows) != 0 || jacksMinijacks(rows) != 0)
                return 4;

            int checkCount = Math.Min(4, rows.Count);
            int jackRows = 0;
            bool hasChord = false;
            double fastestMspb = double.PositiveInfinity;

            for (int i = 0; i < checkCount; i++)
            {
                if (rows[i].Jacks > 0)
                    jackRows++;

                if (rows[i].Notes >= 2)
                    hasChord = true;

                fastestMspb = Math.Min(fastestMspb, rows[i].MsPerBeat);
            }

            if (jackRows >= 2 && hasChord)
                return 3;

            if (jackRows >= 2 && fastestMspb <= EzLeoBlackConfig.JACKY_FALLBACK_MAX_MSPB)
                return 3;

            return 0;
        }

        private static int wildcardSpeed(IReadOnlyList<EzLeoBlackPrimitive> xs)
        {
            if (xs.Count < 2 || !hasLnContext(xs, 4))
                return 0;

            var scratch = ThreadScratch;
            List<EzLeoBlackPrimitive> rows = headRows(xs, Math.Min(4, xs.Count));

            if (xs[0].Keys == 4)
            {
                if (rows.Count >= 3 && stream4kRoll(take(rows, 3, scratch)) != 0)
                    return 3;

                if (rows.Count >= 2)
                {
                    bool sameDir = (rows[0].Direction is EzLeoBlackDirection.Left or EzLeoBlackDirection.Right)
                                   && rows[0].Direction == rows[1].Direction;

                    if (sameDir || rows[0].MsPerBeat <= 180)
                        return 3;
                }
            }
            else
            {
                if (rows.Count >= 3 && chordstream7kChordRoll(take(rows, 3, scratch)) != 0)
                    return 3;

                if (rows.Count >= 2)
                {
                    bool cond = rows[0].Notes >= 2 && rows[1].Notes >= 2
                                && rows[0].Direction == rows[1].Direction
                                && rows[0].Direction is EzLeoBlackDirection.Left or EzLeoBlackDirection.Right;

                    if (cond || rows[0].MsPerBeat <= 170)
                        return 3;
                }
            }

            return 0;
        }

        #endregion

        /// <summary>
        /// Pattern tables are pure data (name + delegate), so they are built once instead of on every chart. The
        /// lists are read-only by convention; nothing mutates them after construction.
        /// </summary>
        private static readonly EzLeoBlackSpecificPatterns four_key_patterns = specific4K();
        private static readonly EzLeoBlackSpecificPatterns seven_key_patterns = specific7K();
        private static readonly EzLeoBlackSpecificPatterns other_key_patterns = specificOther();

        private static EzLeoBlackSpecificPatterns specific4K()
        {
            var coordination = reorderSpecific(
            [
                ("Column Lock", coordinationColumnLock),
                ("Release", coordinationRelease),
                ("Shield", coordinationShield),
            ], EzLeoBlackConfig.COORDINATION_SPECIFIC_ORDER);

            var density = reorderSpecific(
            [
                ("JS Density", density4kJumpstream),
                ("HS Density", density4kHandstream),
                ("Inverse", densityInverse),
            ], EzLeoBlackConfig.DENSITY_SPECIFIC_ORDER);

            var wildcard = reorderSpecific(
            [
                ("Jacky WC", wildcardJack),
                ("Speedy WC", wildcardSpeed),
            ], EzLeoBlackConfig.WILDCARD_SPECIFIC_ORDER);

            return new EzLeoBlackSpecificPatterns
            {
                Stream =
                [
                    ("Rolls", stream4kRoll),
                    ("Trills", stream4kTrill),
                    ("Minitrills", stream4kMinitrill),
                ],
                Chordstream =
                [
                    ("Handstream", chordstream4kHandstream),
                    ("Split Trill", chordstream4kSplittrill),
                    ("Jumptrill", chordstream4kJumptrill),
                    ("Jumpstream", chordstream4kJumpstream),
                ],
                Jack =
                [
                    ("Longjacks", jacksLongjacks),
                    ("Quadstream", jacks4kQuadstream),
                    ("Gluts", jacks4kGluts),
                    ("Chordjacks", jacksChordjacks),
                    ("Minijacks", jacksMinijacks),
                ],
                Coordination = coordination,
                Density = density,
                Wildcard = wildcard,
            };
        }

        private static EzLeoBlackSpecificPatterns specific7K()
        {
            var coordination = reorderSpecific(
            [
                ("Column Lock", coordinationColumnLock),
                ("Release", coordinationRelease),
                ("Shield", coordinationShield),
            ], EzLeoBlackConfig.COORDINATION_SPECIFIC_ORDER);

            var density = reorderSpecific(
            [
                ("DS Density", density7kDoubleStreams),
                ("DCS Density", density7kDenseChordstream),
                ("LCS Density", density7kLightChordstream),
                ("Inverse", densityInverse),
            ], EzLeoBlackConfig.DENSITY_SPECIFIC_ORDER);

            var wildcard = reorderSpecific(
            [
                ("Jacky WC", wildcardJack),
                ("Speedy WC", wildcardSpeed),
            ], EzLeoBlackConfig.WILDCARD_SPECIFIC_ORDER);

            return new EzLeoBlackSpecificPatterns
            {
                Stream = [],
                Chordstream =
                [
                    ("Brackets", chordstream7kBrackets),
                    ("Double Stream", chordstream7kDoubleStreams),
                    ("Dense Chordstream", chordstream7kDenseChordstream),
                    ("Light Chordstream", chordstream7kLightChordstream),
                ],
                Jack =
                [
                    ("Longjacks", jacksLongjacks),
                    ("Chordjacks", jacksChordjacks),
                    ("Minijacks", jacksMinijacks),
                ],
                Coordination = coordination,
                Density = density,
                Wildcard = wildcard,
            };
        }

        private static EzLeoBlackSpecificPatterns specificOther()
        {
            var coordination = reorderSpecific(
            [
                ("Column Lock", coordinationColumnLock),
                ("Release", coordinationRelease),
                ("Shield", coordinationShield),
            ], EzLeoBlackConfig.COORDINATION_SPECIFIC_ORDER);

            var density = reorderSpecific(
            [
                ("DS Density", density7kDoubleStreams),
                ("DCS Density", density7kDenseChordstream),
                ("LCS Density", density7kLightChordstream),
                ("Inverse", densityInverse),
            ], EzLeoBlackConfig.DENSITY_SPECIFIC_ORDER);

            var wildcard = reorderSpecific(
            [
                ("Jacky WC", wildcardJack),
                ("Speedy WC", wildcardSpeed),
            ], EzLeoBlackConfig.WILDCARD_SPECIFIC_ORDER);

            return new EzLeoBlackSpecificPatterns
            {
                Stream = [],
                Chordstream =
                [
                    ("Chord Rolls", chordstream7kChordRoll),
                    ("Double Stream", chordstream7kDoubleStreams),
                    ("Dense Chordstream", chordstream7kDenseChordstream),
                    ("Light Chordstream", chordstream7kLightChordstream),
                ],
                Jack =
                [
                    ("Longjacks", jacksLongjacks),
                    ("Chordjacks", jacksChordjacks),
                    ("Minijacks", jacksMinijacks),
                ],
                Coordination = coordination,
                Density = density,
                Wildcard = wildcard,
            };
        }

        /// <summary>
        /// Per-thread buffers for one analyzer run. The scan is sequential and the matchers read their inputs and
        /// return, so a single instance may be reused across every window of a chart. Thread-static because
        /// <see cref="EzLeoBlackPatternAnalyzer"/> can be called from more than one worker.
        /// </summary>
        private sealed class MatchScratch
        {
            public readonly List<EzLeoBlackPrimitive> Window = new List<EzLeoBlackPrimitive>(MATCHER_WINDOW);
            public readonly List<(int N, string SpecificType)> Matched = new List<(int N, string SpecificType)>(4);

            public readonly List<EzLeoBlackPrimitive> HeadRows = new List<EzLeoBlackPrimitive>(4);
            public readonly List<EzLeoBlackPrimitive> ReleaseRows = new List<EzLeoBlackPrimitive>(EzLeoBlackConfig.RELEASE_FULL_MATCH_ROWS);
            public readonly List<EzLeoBlackPrimitive> WildcardRows = new List<EzLeoBlackPrimitive>(EzLeoBlackConfig.JACKY_CONTEXT_WINDOW);
            public readonly List<EzLeoBlackPrimitive> PickedRows = new List<EzLeoBlackPrimitive>(EzLeoBlackConfig.RELEASE_SCAN_ROWS);
            public readonly List<EzLeoBlackPrimitive> TakeRows = new List<EzLeoBlackPrimitive>(3);

            public readonly List<int> AdjacentColumns = new List<int>(2);
            public readonly List<double> HitTimes = new List<double>(8);
            public readonly List<double> InverseGaps = new List<double>(4);
            public readonly List<int>[] ReleaseColumns = createReleaseColumns();

            private readonly List<EzLeoBlackPrimitive> headPool = new List<EzLeoBlackPrimitive>(4);
            private readonly List<EzLeoBlackPrimitive> releasePool = new List<EzLeoBlackPrimitive>(EzLeoBlackConfig.RELEASE_FULL_MATCH_ROWS);
            private int headCount;
            private int releaseCount;

            private static List<int>[] createReleaseColumns()
            {
                var columns = new List<int>[EzLeoBlackConfig.RELEASE_FULL_MATCH_ROWS + 1];

                for (int i = 0; i < columns.Length; i++)
                    columns[i] = new List<int>(1);

                return columns;
            }

            public List<EzLeoBlackPrimitive> BeginHeadRows()
            {
                HeadRows.Clear();
                headCount = 0;
                return HeadRows;
            }

            public EzLeoBlackPrimitive NextHeadPrimitive()
            {
                if (headCount == headPool.Count)
                    headPool.Add(new EzLeoBlackPrimitive());

                return headPool[headCount++];
            }

            public List<EzLeoBlackPrimitive> BeginReleaseRows()
            {
                ReleaseRows.Clear();
                releaseCount = 0;
                return ReleaseRows;
            }

            public EzLeoBlackPrimitive NextReleasePrimitive()
            {
                if (releaseCount == releasePool.Count)
                    releasePool.Add(new EzLeoBlackPrimitive());

                return releasePool[releaseCount++];
            }
        }

        [ThreadStatic]
        private static MatchScratch? threadScratch;

        private static MatchScratch ThreadScratch => threadScratch ??= new MatchScratch();

        private static IReadOnlyList<EzLeoBlackPrimitive> take(List<EzLeoBlackPrimitive> rows, int count, MatchScratch scratch)
        {
            List<EzLeoBlackPrimitive> taken = scratch.TakeRows;
            taken.Clear();

            for (int i = 0; i < count && i < rows.Count; i++)
                taken.Add(rows[i]);

            return taken;
        }

        private static List<EzLeoBlackFoundPattern> matches(
            EzLeoBlackSpecificPatterns specificPatterns,
            double lastNote,
            List<EzLeoBlackPrimitive> primitives)
        {
            var results = new List<EzLeoBlackFoundPattern>();
            var scratch = ThreadScratch;
            int start = 0;

            while (start < primitives.Count)
            {
                int windowLength = Math.Min(MATCHER_WINDOW, primitives.Count - start);
                List<EzLeoBlackPrimitive> remaining = scratch.Window;
                remaining.Clear();

                for (int i = 0; i < windowLength; i++)
                    remaining.Add(primitives[start + i]);

                appendCoreMatches(results, EzLeoBlackCorePattern.Stream, coreStream(remaining), specificPatterns.Stream, remaining, lastNote, scratch);
                appendCoreMatches(results, EzLeoBlackCorePattern.Chordstream, coreChordstream(remaining), specificPatterns.Chordstream, remaining, lastNote, scratch);
                appendCoreMatches(results, EzLeoBlackCorePattern.Jacks, coreJacks(remaining), specificPatterns.Jack, remaining, lastNote, scratch);
                appendCoreMatches(results, EzLeoBlackCorePattern.Coordination, coreCoordination(remaining), specificPatterns.Coordination, remaining, lastNote, scratch);
                appendCoreMatches(results, EzLeoBlackCorePattern.Density, coreDensity(remaining), specificPatterns.Density, remaining, lastNote, scratch);
                appendCoreMatches(results, EzLeoBlackCorePattern.Wildcard, coreWildcard(remaining), specificPatterns.Wildcard, remaining, lastNote, scratch);
                start++;
            }

            return results;
        }

        private static void appendCoreMatches(
            List<EzLeoBlackFoundPattern> results,
            string pattern,
            int coreN,
            List<(string Name, EzLeoBlackPatternMatcher Match)> specificList,
            List<EzLeoBlackPrimitive> remaining,
            double lastNote,
            MatchScratch scratch)
        {
            if (coreN == 0)
                return;

            // hub ENABLE_MULTI_LABEL_SAME_WINDOW is true; single-label arm omitted.
            pickSpecificAll(specificList, remaining, scratch.Matched);

            if (scratch.Matched.Count == 0)
            {
                appendFoundPattern(results, pattern, null, coreN, remaining, lastNote);
                return;
            }

            foreach (var (m, specificType) in scratch.Matched)
                appendFoundPattern(results, pattern, specificType, Math.Max(coreN, m), remaining, lastNote);
        }

        private static void pickSpecificAll(
            List<(string Name, EzLeoBlackPatternMatcher Match)> specificList,
            List<EzLeoBlackPrimitive> remaining,
            List<(int N, string SpecificType)> matched)
        {
            matched.Clear();

            foreach (var (name, match) in specificList)
            {
                int n = match(remaining);

                if (n != 0)
                    matched.Add((n, name));
            }
        }

        private static void appendFoundPattern(
            List<EzLeoBlackFoundPattern> results,
            string pattern,
            string? specificType,
            int n2,
            List<EzLeoBlackPrimitive> remaining,
            double lastNote)
        {
            int count = Math.Min(n2, remaining.Count);
            double sum = 0;

            for (int i = 0; i < count; i++)
                sum += remaining[i].MsPerBeat;

            double meanMspb = sum / count;
            bool mixed = false;

            for (int i = 0; i < count; i++)
            {
                if (!(Math.Abs(remaining[i].MsPerBeat - meanMspb) < EzLeoBlackConfig.PATTERN_STABILITY_THRESHOLD))
                {
                    mixed = true;
                    break;
                }
            }

            double start = remaining[0].Time;
            double end;

            if (pattern == EzLeoBlackCorePattern.Jacks)
            {
                double endCandidate = n2 < remaining.Count ? remaining[n2].Time : lastNote;
                end = Math.Max(remaining[0].Time + remaining[0].MsPerBeat * 0.5, endCandidate);
            }
            else
            {
                end = n2 < remaining.Count ? remaining[n2].Time : lastNote;
            }

            double resolvedMspb = pattern == EzLeoBlackCorePattern.Density
                                  && string.Equals(specificType, "Inverse", StringComparison.Ordinal)
                ? 0.0
                : meanMspb;

            results.Add(new EzLeoBlackFoundPattern
            {
                Pattern = pattern,
                SpecificType = specificType,
                Mixed = mixed,
                Start = start,
                End = end,
                MsPerBeat = resolvedMspb,
            });
        }
    }
}
