// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// The revision each analysis system last ran at. One row per system, so adding a system adds a row, not a column.
    /// </summary>
    /// <remarks>
    /// A missing row means the system has never been applied, which is read as "out of date": a fresh install and an
    /// upgraded library therefore take the same path, and no migration has to seed this table.
    /// </remarks>
    [MapTo("EzAnalysisState")]
    public class EzAnalysisState : RealmObject
    {
        /// <summary>Declared id of the <see cref="Analysis.EzAnalysisSystem"/> this row belongs to.</summary>
        [PrimaryKey]
        public string SystemId { get; set; } = string.Empty;

        public int AppliedVersion { get; set; }
    }
}
