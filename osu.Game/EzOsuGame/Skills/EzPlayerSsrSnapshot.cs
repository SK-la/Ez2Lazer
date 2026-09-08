// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Detached SSR snapshot for one username + keymode (values + row metadata).
    /// </summary>
    public sealed class EzPlayerSsrSnapshot
    {
        public const int QUALIFYING_PLAYS = 3;

        public IReadOnlyDictionary<string, double> Values { get; init; } =
            new Dictionary<string, double>(StringComparer.Ordinal);

        public int AnalyzedPlays { get; init; }

        public bool Provisional { get; init; }

        public bool Stale { get; init; }

        public DateTimeOffset? ComputedAt { get; init; }

        public double Overall =>
            Values.GetValueOrDefault(EzSkillIds.Ssr(EzSkillIds.OVERALL), 0);

        /// <summary>Hub-style provisional: stored flag or fewer than qualifying plays.</summary>
        public bool IsEffectivelyProvisional => Provisional || AnalyzedPlays < QUALIFYING_PLAYS;
    }
}
