// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Chart-side dan verdict for song select / credit input. Not a Realm row.
    /// MVP uses MSD→rawDan heuristic; LeoBlack can replace the estimator without changing this shape.
    /// </summary>
    public sealed class EzChartDanVerdict
    {
        public double RawDan { get; init; }

        public string Label { get; init; } = string.Empty;

        public int KeyCount { get; init; }

        /// <summary><see cref="DanSkillSystem.SIDE_RC"/> or <see cref="DanSkillSystem.SIDE_LN"/> from chart hold ratio.</summary>
        public string Side { get; init; } = DanSkillSystem.SIDE_RC;

        /// <summary>Dominant MSD family used for the SR→rawDan means table.</summary>
        public string Family { get; init; } = "stream";

        public double OverallMsd { get; init; }

        public double HoldRatio { get; init; }

        public int AlgorithmVersion { get; init; } = EzDanAlgorithm.VERSION;
    }
}
