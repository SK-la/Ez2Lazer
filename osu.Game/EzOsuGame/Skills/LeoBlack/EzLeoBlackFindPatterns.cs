// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

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
        {
            var primitives = EzLeoBlackPrimitives.CalculatePrimitives(chart);
            EzLeoBlackSpecificPatterns keymodePatterns = chart.Keys switch
            {
                4 => specific4K(),
                7 => specific7K(),
                _ => specificOther(),
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

        private static EzLeoBlackPrimitive asHeadPointRow(EzLeoBlackPrimitive row, IReadOnlyList<int> previousHeadCols)
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

            return new EzLeoBlackPrimitive
            {
                Index = row.Index,
                Time = row.Time,
                MsPerBeat = row.MsPerBeat,
                BeatLength = row.BeatLength,
                Notes = headCols.Count,
                Jacks = jacks,
                Direction = direction,
                Roll = roll,
                Keys = row.Keys,
                LeftHandKeys = row.LeftHandKeys,
                LNHeads = row.LNHeads,
                LNBodies = row.LNBodies,
                LNTails = row.LNTails,
                NormalNotes = [],
                RawNotes = headCols,
            };
        }

        private static List<EzLeoBlackPrimitive> headRows(IReadOnlyList<EzLeoBlackPrimitive> xs, int n)
        {
            var rows = new List<EzLeoBlackPrimitive>();
            IReadOnlyList<int> prev = Array.Empty<int>();

            int limit = Math.Min(n, xs.Count);

            for (int i = 0; i < limit; i++)
            {
                var hr = asHeadPointRow(xs[i], prev);
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

            var gaps = new List<double>();

            for (int i = 0; i < 4; i++)
            {
                if (xs[i].LNTails.Count > 0 && xs[i + 1].LNHeads.Count > 0)
                    gaps.Add(xs[i + 1].Time - xs[i].Time);
            }

            if (gaps.Count < 2)
                return false;

            return gaps.Max() - gaps.Min() <= EzLeoBlackConfig.INVERSE_GAP_TOLERANCE_MS;
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

            var adjCols = new List<int>();

            foreach (int c in new[] { lnCol.Value - 1, lnCol.Value + 1 })
            {
                if (c >= 0 && c < xs[0].Keys && isSameHandAdjacent(lnCol.Value, c, split))
                    adjCols.Add(c);
            }

            if (adjCols.Count == 0)
                return 0;

            int scan = Math.Min(8, xs.Count);

            foreach (int adj in adjCols)
            {
                var hits = new List<double>();

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

            var pickedRows = new List<EzLeoBlackPrimitive>();
            int scan = Math.Min(EzLeoBlackConfig.RELEASE_SCAN_ROWS, xs.Count);

            for (int i = 0; i < scan; i++)
            {
                if (xs[i].LNTails.Count == 1)
                    pickedRows.Add(xs[i]);
            }

            if (pickedRows.Count < EzLeoBlackConfig.RELEASE_MIN_TAIL_ROWS)
                return 0;

            int useRows = Math.Min(EzLeoBlackConfig.RELEASE_FULL_MATCH_ROWS, pickedRows.Count);
            int[] tails = new int[useRows];

            for (int i = 0; i < useRows; i++)
                tails[i] = pickedRows[i].LNTails[0];

            var prev = new List<int> { tails[0] };
            var rows = new List<EzLeoBlackPrimitive>();

            for (int i = 0; i < useRows; i++)
            {
                var row = pickedRows[i];
                var cur = new List<int> { tails[i] };
                var (direction, roll) = EzLeoBlackPrimitives.DetectDirection(prev, cur);

                rows.Add(new EzLeoBlackPrimitive
                {
                    Index = row.Index,
                    Time = row.Time,
                    MsPerBeat = row.MsPerBeat,
                    BeatLength = row.BeatLength,
                    Notes = 1,
                    Jacks = cur[0] == prev[0] ? 1 : 0,
                    Direction = direction,
                    Roll = roll,
                    Keys = row.Keys,
                    LeftHandKeys = row.LeftHandKeys,
                    LNHeads = row.LNHeads,
                    LNBodies = row.LNBodies,
                    LNTails = row.LNTails,
                    NormalNotes = [],
                    RawNotes = cur,
                });

                prev = cur;
            }

            var effectiveRows = rows.Count > 1 ? rows.Skip(1).ToList() : [];

            if (effectiveRows.Count < EzLeoBlackConfig.RELEASE_ROLL_POINTS)
                return 0;

            // hub RELEASE_ROLL_POINTS is 2, so the >=3 STREAM_4K_ROLL arm never runs.
            int aCol = effectiveRows[0].RawNotes[0];
            int bCol = effectiveRows.Count > 1 ? effectiveRows[1].RawNotes[0] : aCol;
            double dt = effectiveRows.Count > 1 ? effectiveRows[1].Time - effectiveRows[0].Time : 0;
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
            var rows = new List<EzLeoBlackPrimitive>();

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

            var rows = headRows(xs, Math.Min(4, xs.Count));

            if (xs[0].Keys == 4)
            {
                if (rows.Count >= 3 && stream4kRoll(rows.Take(3).ToList()) != 0)
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
                if (rows.Count >= 3 && chordstream7kChordRoll(rows.Take(3).ToList()) != 0)
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

        private static List<EzLeoBlackFoundPattern> matches(
            EzLeoBlackSpecificPatterns specificPatterns,
            double lastNote,
            List<EzLeoBlackPrimitive> primitives)
        {
            var results = new List<EzLeoBlackFoundPattern>();
            int start = 0;

            while (start < primitives.Count)
            {
                var remaining = primitives.GetRange(start, Math.Min(MATCHER_WINDOW, primitives.Count - start));
                appendCoreMatches(results, EzLeoBlackCorePattern.Stream, coreStream(remaining), specificPatterns.Stream, remaining, lastNote);
                appendCoreMatches(results, EzLeoBlackCorePattern.Chordstream, coreChordstream(remaining), specificPatterns.Chordstream, remaining, lastNote);
                appendCoreMatches(results, EzLeoBlackCorePattern.Jacks, coreJacks(remaining), specificPatterns.Jack, remaining, lastNote);
                appendCoreMatches(results, EzLeoBlackCorePattern.Coordination, coreCoordination(remaining), specificPatterns.Coordination, remaining, lastNote);
                appendCoreMatches(results, EzLeoBlackCorePattern.Density, coreDensity(remaining), specificPatterns.Density, remaining, lastNote);
                appendCoreMatches(results, EzLeoBlackCorePattern.Wildcard, coreWildcard(remaining), specificPatterns.Wildcard, remaining, lastNote);
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
            double lastNote)
        {
            if (coreN == 0)
                return;

            // hub ENABLE_MULTI_LABEL_SAME_WINDOW is true; single-label arm omitted.
            var matched = pickSpecificAll(specificList, remaining);

            if (matched.Count == 0)
            {
                appendFoundPattern(results, pattern, null, coreN, remaining, lastNote);
                return;
            }

            foreach (var (m, specificType) in matched)
                appendFoundPattern(results, pattern, specificType, Math.Max(coreN, m), remaining, lastNote);
        }

        private static List<(int N, string SpecificType)> pickSpecificAll(
            List<(string Name, EzLeoBlackPatternMatcher Match)> specificList,
            List<EzLeoBlackPrimitive> remaining)
        {
            var matched = new List<(int, string)>();

            foreach (var (name, match) in specificList)
            {
                int n = match(remaining);

                if (n != 0)
                    matched.Add((n, name));
            }

            return matched;
        }

        private static void appendFoundPattern(
            List<EzLeoBlackFoundPattern> results,
            string pattern,
            string? specificType,
            int n2,
            List<EzLeoBlackPrimitive> remaining,
            double lastNote)
        {
            var d = remaining.GetRange(0, Math.Min(n2, remaining.Count));
            double meanMspb = d.Sum(x => x.MsPerBeat) / d.Count;
            bool mixed = !d.TrueForAll(x => Math.Abs(x.MsPerBeat - meanMspb) < EzLeoBlackConfig.PATTERN_STABILITY_THRESHOLD);

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
