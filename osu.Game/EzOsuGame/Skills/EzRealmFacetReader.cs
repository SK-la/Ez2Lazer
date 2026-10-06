// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Collapses one Realm table's rows into the per-key row states a facet is declared from. Any facet that stores
    /// one row set per key with a revision column reads the same way, so the tables do not each re-state it.
    /// </summary>
    internal static class EzRealmFacetReader
    {
        /// <param name="keyOf">The chart (or other key) a row belongs to.</param>
        /// <param name="revisionOf">The revision stamped when the row was produced.</param>
        /// <param name="isStubOf">Whether a row is the facet's settled-miss stub. Only rows at
        /// <paramref name="currentRevision"/> are consulted, so a reader can test revision and stub together.</param>
        public static Dictionary<string, EzFacetRowState> Read<T>(
            IEnumerable<T> rows,
            Func<T, string> keyOf,
            Func<T, int> revisionOf,
            int currentRevision,
            Func<T, bool> isStubOf)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var states = new Dictionary<string, EzFacetRowState>(StringComparer.Ordinal);

            foreach (var group in rows.GroupBy(keyOf, StringComparer.Ordinal))
            {
                int latest = 0;
                bool stub = false;

                foreach (var row in group)
                {
                    int revision = revisionOf(row);

                    if (revision > latest)
                        latest = revision;

                    if (revision == currentRevision && isStubOf(row))
                        stub = true;
                }

                states[group.Key] = new EzFacetRowState(latest, stub);
            }

            return states;
        }
    }
}
