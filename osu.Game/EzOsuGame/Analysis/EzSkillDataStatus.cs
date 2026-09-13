// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// Completeness report for one facet of the Ez chart skill chain, counted over every mania chart
    /// in Realm. Derived purely from the persisted <see cref="EzAnalysisRevision"/> stamps, so it needs
    /// no extra bookkeeping column and cannot drift from what the backfill actually reads.
    /// </summary>
    public sealed class EzFacetStatus
    {
        /// <summary>Revision this facet's rows are compared against.</summary>
        public required int CurrentRevision { get; init; }

        /// <summary>Rows already carrying <see cref="CurrentRevision"/> and holding a real result.</summary>
        public required int Ready { get; init; }

        /// <summary>Rows carrying the current revision that settled as unrateable.</summary>
        public required int Unrateable { get; init; }

        /// <summary>Rows left behind by an older revision - an incremental pass will recompute them.</summary>
        public required int Stale { get; init; }

        /// <summary>Charts with no row for this facet at all.</summary>
        public required int Missing { get; init; }

        /// <summary>Work the next incremental pass would pick up.</summary>
        public int Pending => Stale + Missing;

        public int Settled => Ready + Unrateable;
    }

    /// <summary>
    /// Snapshot of how complete / how current the Ez chart skill chain is. Answers "is data missing or
    /// outdated?" without having to guess or force a rebuild.
    /// </summary>
    public sealed class EzSkillDataStatus
    {
        /// <summary>Mania charts considered (the same candidate universe the backfill walks).</summary>
        public required int TotalCharts { get; init; }

        public required EzFacetStatus Msd { get; init; }

        public required EzFacetStatus ChartSkillInfo { get; init; }

        public required EzFacetStatus ChartDan { get; init; }

        public required DateTimeOffset MeasuredAt { get; init; }

        public bool HasWorkToDo => Msd.Pending > 0 || ChartSkillInfo.Pending > 0 || ChartDan.Pending > 0;

        /// <summary>Total rows that a backfill would (re)compute.</summary>
        public int TotalPending => Msd.Pending + ChartSkillInfo.Pending + ChartDan.Pending;

        /// <summary>
        /// One line per facet for notifications / logs, e.g.
        /// <c>MSD v2: ready 812, unrateable 40, stale 0, missing 12</c>.
        /// </summary>
        public string Describe()
            => string.Join('\n', new[]
            {
                describe("MSD", Msd),
                describe("CSI", ChartSkillInfo),
                describe("Dan", ChartDan),
            });

        private static string describe(string name, EzFacetStatus facet)
            => $"{name} v{facet.CurrentRevision}: ready {facet.Ready}, unrateable {facet.Unrateable}, stale {facet.Stale}, missing {facet.Missing}";
    }

    internal static class EzSkillDataStatusCounting
    {
        /// <summary>
        /// Counts one facet's rows against <paramref name="currentRevision"/>. Rows for hashes outside
        /// <paramref name="chartHashes"/> are ignored (a removed beatmap's stale row is not "work").
        /// </summary>
        /// <param name="settledByUpstream">
        /// Hashes this facet will never produce a row for because an upstream one already settled them as
        /// unrateable. They count as <see cref="EzFacetStatus.Unrateable"/> - settled, so not work - which keeps
        /// "Pending &gt; 0" meaning "the next pass has something to do".
        /// </param>
        public static EzFacetStatus Count<T>(
            IReadOnlyCollection<string> chartHashes,
            IEnumerable<IGrouping<string, T>> rowsByHash,
            int currentRevision,
            Func<T, int> revisionOf,
            Func<T, bool> isSettledStub,
            IReadOnlyCollection<string>? settledByUpstream = null)
        {
            int ready = 0;
            int unrateable = 0;
            int stale = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in rowsByHash)
            {
                if (!chartHashes.Contains(group.Key))
                    continue;

                seen.Add(group.Key);

                int latest = group.Max(revisionOf);

                if (latest != currentRevision)
                {
                    ++stale;
                    continue;
                }

                if (group.Any(row => revisionOf(row) == currentRevision && isSettledStub(row)))
                    ++unrateable;
                else
                    ++ready;
            }

            int settledWithoutRow = 0;

            if (settledByUpstream != null)
            {
                foreach (string hash in settledByUpstream)
                {
                    if (chartHashes.Contains(hash) && !seen.Contains(hash))
                        ++settledWithoutRow;
                }
            }

            return new EzFacetStatus
            {
                CurrentRevision = currentRevision,
                Ready = ready,
                Unrateable = unrateable + settledWithoutRow,
                Stale = stale,
                Missing = Math.Max(0, chartHashes.Count - seen.Count - settledWithoutRow),
            };
        }

        public static bool IsMsdStub(EzBeatmapSkillValue row) => row.SkillId == EzSkillSystems.MsdUnrateableSkillId;

        public static bool IsChartSkillInfoStub(EzBeatmapChartSkillInfo row) => row.KeyCount < 0;
    }
}
