// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// One key's row state for one facet: the newest revision any of its rows carries, and whether a row at the
    /// facet's current revision is the settled-miss stub rather than a result.
    /// </summary>
    public readonly record struct EzFacetRowState(int Revision, bool CurrentStub);

    /// <summary>
    /// One facet of an analysis store, declared as its identity plus the rows it holds - everything the generic
    /// <see cref="EzDataStateChecker"/> needs. A store produces one of these per facet (a Realm table, a SQLite
    /// table, the chart chain's derived verdicts), so the counts and the readout are the same for any store rather
    /// than re-stated per consumer.
    /// </summary>
    public sealed class EzDataStateFacet
    {
        /// <summary>Stable key, also the label a report renders (<c>MSD</c>, <c>CSI</c>, <c>Dan</c>, ...).</summary>
        public required string Id { get; init; }

        /// <summary>The declaration this facet belongs to, tying the readout back to the revision model.</summary>
        public required EzAnalysisFacet Facet { get; init; }

        /// <summary>Revision a row must carry to count as current.</summary>
        public required int CurrentRevision { get; init; }

        /// <summary>How many keys the facet is counted over - the store's candidate universe.</summary>
        public required int UniverseCount { get; init; }

        /// <summary>Newest row state per key, already narrowed to the universe.</summary>
        public required IReadOnlyDictionary<string, EzFacetRowState> Rows { get; init; }

        /// <summary>
        /// Keys this facet can never hold a row for because an upstream one settled them (an unrateable MSD, stored
        /// inputs that resolve to no result). They count as settled - no result, but not work either.
        /// </summary>
        public IReadOnlyCollection<string>? SettledWithoutRow { get; init; }
    }

    /// <summary>
    /// Completeness of one facet: how many of its universe's keys hold a current result, a settled miss, an
    /// out-of-date row, or nothing at all. Derived purely from row revisions, so it needs no bookkeeping column and
    /// cannot drift from what the producing pass reads.
    /// </summary>
    public sealed class EzFacetStatus
    {
        public required string Id { get; init; }

        /// <summary>Revision the facet's rows are compared against.</summary>
        public required int CurrentRevision { get; init; }

        /// <summary>Keys holding a current revision row with a real result.</summary>
        public required int Ready { get; init; }

        /// <summary>Keys settled without a result - the facet's stub row, or an upstream facet settling them.</summary>
        public required int Settled { get; init; }

        /// <summary>Keys whose newest row is from an older revision - an incremental pass will recompute them.</summary>
        public required int Stale { get; init; }

        /// <summary>Keys with no row for this facet at all.</summary>
        public required int Missing { get; init; }

        /// <summary>Work the next incremental pass would pick up.</summary>
        public int Pending => Stale + Missing;
    }

    /// <summary>
    /// Turns a facet's declared rows into its <see cref="EzFacetStatus"/>. The whole "is this data missing or
    /// outdated?" rule lives here once, so every store and every consumer agrees on it.
    /// </summary>
    internal static class EzDataStateChecker
    {
        public static EzFacetStatus Count(EzDataStateFacet facet)
        {
            ArgumentNullException.ThrowIfNull(facet);

            var settledWithoutRow = toSet(facet.SettledWithoutRow);

            int ready = 0;
            int settled = 0;
            int stale = 0;

            foreach (var (key, row) in facet.Rows)
            {
                // An upstream facet settling a key overrides whatever row is here: a row derived from an upstream
                // stub is precisely what must not read as a result.
                if (settledWithoutRow != null && settledWithoutRow.Contains(key))
                {
                    ++settled;
                    continue;
                }

                if (row.Revision != facet.CurrentRevision)
                {
                    ++stale;
                    continue;
                }

                if (row.CurrentStub)
                    ++settled;
                else
                    ++ready;
            }

            int settledKeyless = settledWithoutRow?.Count(key => !facet.Rows.ContainsKey(key)) ?? 0;

            return new EzFacetStatus
            {
                Id = facet.Id,
                CurrentRevision = facet.CurrentRevision,
                Ready = ready,
                Settled = settled + settledKeyless,
                Stale = stale,
                Missing = Math.Max(0, facet.UniverseCount - facet.Rows.Count - settledKeyless),
            };
        }

        private static HashSet<string>? toSet(IReadOnlyCollection<string>? keys)
            => keys == null
                ? null
                : keys as HashSet<string> ?? new HashSet<string>(keys, StringComparer.Ordinal);
    }

    /// <summary>
    /// Snapshot of how complete / how current a store's facets are, as an ordered list rather than fixed
    /// properties - so a facet added to a store shows up in the readout without touching it.
    /// </summary>
    public sealed class EzDataStateReport
    {
        /// <summary>Size of the candidate universe the facets were counted over.</summary>
        public required int UniverseCount { get; init; }

        public required IReadOnlyList<EzFacetStatus> Facets { get; init; }

        public required DateTimeOffset MeasuredAt { get; init; }

        public bool HasWorkToDo => Facets.Any(static f => f.Pending > 0);

        /// <summary>Total rows that a pass would (re)compute.</summary>
        public int TotalPending => Facets.Sum(static f => f.Pending);

        public EzFacetStatus this[string id]
            => Facets.FirstOrDefault(f => f.Id == id)
               ?? throw new KeyNotFoundException($"No facet '{id}' in this report.");

        /// <summary>
        /// One line per facet for notifications / logs, e.g.
        /// <c>MSD v2: ready 812, settled 40, stale 0, missing 12</c>.
        /// </summary>
        public string Describe()
            => string.Join('\n', Facets.Select(static f
                   => $"{f.Id} v{f.CurrentRevision}: ready {f.Ready}, settled {f.Settled}, stale {f.Stale}, missing {f.Missing}"));
    }
}
