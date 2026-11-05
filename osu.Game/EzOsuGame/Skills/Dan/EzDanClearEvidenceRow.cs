// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Skills.Dan
{
    /// <summary>
    /// One credited dan clear, kept as thin evidence behind a dan aggregate.
    /// </summary>
    public sealed class EzDanClearEvidenceRow
    {
        public string Username { get; init; } = string.Empty;
        public int KeyCount { get; init; }
        public string Side { get; init; } = EzDanSide.Rc.ToId();
        public string BeatmapHash { get; init; } = string.Empty;
        public double Rate { get; init; } = 1;
        public double CreditedDan { get; init; }
        public double Accuracy { get; init; }
        public DateTimeOffset ScoredAt { get; init; }
        public int AlgorithmVersion { get; init; } = EzDanAlgorithm.VERSION;
    }
}
