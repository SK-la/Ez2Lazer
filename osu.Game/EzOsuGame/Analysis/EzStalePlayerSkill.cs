// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// One player whose stored skill rows are flagged stale, summarised for the status readout. The flag means the
    /// player's SQLite slice moved on after these rows were written, so the numbers on screen are the previous pass's.
    /// </summary>
    /// <param name="Rows">Flagged rows the player holds (SSR / pattern rows across their keymodes).</param>
    /// <param name="ComputedAt">Newest write time among those rows.</param>
    public readonly record struct EzStalePlayerSkill(string Username, int Rows, DateTimeOffset ComputedAt);
}
