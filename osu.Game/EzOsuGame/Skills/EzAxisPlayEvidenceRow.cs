// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.EzOsuGame.Skills.Mina;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One per-play rating-axis contribution, kept as thin evidence behind an aggregate.
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
        public int AlgorithmVersion { get; init; } = EzMinaEngine.VERSION;
    }
}
