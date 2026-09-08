// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One credited dan clear stored in <c>ez-local-profile.sqlite</c> (DATA-3).
    /// </summary>
    public sealed class EzDanClearEvidenceRow
    {
        public string Username { get; init; } = string.Empty;
        public int KeyCount { get; init; }
        public string Side { get; init; } = DanSkillSystem.SIDE_RC;
        public string BeatmapHash { get; init; } = string.Empty;
        public double Rate { get; init; } = 1;
        public double CreditedDan { get; init; }
        public double Accuracy { get; init; }
        public DateTimeOffset ScoredAt { get; init; }
    }

    /// <summary>
    /// One per-play SSR axis contribution stored in <c>ez-local-profile.sqlite</c> (DATA-3).
    /// </summary>
    public sealed class EzAxisPlayEvidenceRow
    {
        public string Username { get; init; } = string.Empty;
        public int KeyCount { get; init; }
        public string SkillId { get; init; } = string.Empty;
        public string BeatmapHash { get; init; } = string.Empty;
        public double AxisValue { get; init; }
        public double Accuracy { get; init; }
        public double Rate { get; init; } = 1;
        public DateTimeOffset ScoredAt { get; init; }
    }
}
