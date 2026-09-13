// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One MinaCalc input row: a column bitmask plus its row time in seconds.
    /// Same-time notes share one row. Holds contribute their head only unless the
    /// caller also emits the release row.
    /// </summary>
    /// <param name="Notes">Bitmask of active columns (bit N = column N, up to 32 columns).</param>
    /// <param name="RowTime">Row time in seconds. Charts must be non-negative; use the
    /// note converter's offset handling rather than passing negative times.</param>
    public readonly record struct EzCalcNote(uint Notes, float RowTime);
}
