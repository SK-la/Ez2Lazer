// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// One player whose stored skill rows are behind, summarised for the settings status readout. Staleness is derived
    /// (ledger or superseded algorithm revision), not stored; expired rows are refreshed only by a manual compute pass.
    /// </summary>
    /// <param name="Rows">Out-of-date row count for this player (SunnySR / pattern rows across their keymodes).</param>
    /// <param name="ComputedAt">Newest write time among those rows.</param>
    public readonly record struct EzStalePlayerSkill(string Username, int Rows, DateTimeOffset ComputedAt);
}
