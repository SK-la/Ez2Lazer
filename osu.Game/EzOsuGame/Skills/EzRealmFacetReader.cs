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
    /// one row set per key reads the same way, so the tables do not each re-state it.
    /// </summary>
    internal static class EzRealmFacetReader
    {
        /// <param name="keyOf">The chart (or other key) a row belongs to.</param>
        /// <param name="isStubOf">Whether a row is the facet's settled-miss stub.</param>
        public static Dictionary<string, EzFacetRowState> Read<T>(
            IEnumerable<T> rows,
            Func<T, string> keyOf,
            Func<T, bool> isStubOf)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var states = new Dictionary<string, EzFacetRowState>(StringComparer.Ordinal);

            foreach (var group in rows.GroupBy(keyOf, StringComparer.Ordinal))
                states[group.Key] = new EzFacetRowState(group.Any(isStubOf));

            return states;
        }
    }
}
