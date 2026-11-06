// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One point on a player's per-axis rating history curve.
    /// </summary>
    public readonly record struct EzPlayerSkillHistorySample(string SkillId, double Value, DateTimeOffset RecordedAt);

    /// <summary>
    /// An independent dan estimate for one player slice and side.
    /// </summary>
    public readonly record struct EzPlayerDanEstimateRow(
        string Username,
        int KeyCount,
        string Side,
        double RawDan,
        string Label,
        int Clears,
        bool BeyondTable,
        string CourseName,
        double CourseAccuracy,
        int ClearWindowHave,
        int ClearWindowNeed,
        int AlgorithmVersion,
        DateTimeOffset ComputedAt);

    /// <summary>
    /// One cached skillset-dan tile for a player slice and side.
    /// </summary>
    public readonly record struct EzPlayerDanSkillsetRow(
        string Side,
        string SkillsetId,
        double RawDan,
        string Label,
        int Clears,
        int AlgorithmVersion,
        DateTimeOffset ComputedAt);
}
