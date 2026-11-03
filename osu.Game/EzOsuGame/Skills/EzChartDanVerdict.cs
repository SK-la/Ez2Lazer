// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Chart-side dan verdict for song select / credit input. Not a Realm row.
    /// </summary>
    public sealed class EzChartDanVerdict
    {
        public double RawDan { get; init; }

        public string Label { get; init; } = string.Empty;

        public int KeyCount { get; init; }

        public EzDanSide Side { get; init; } = EzDanSide.Rc;

        /// <summary>
        /// Dominant RcMina radar axis for skill chip / SR→rawDan means (when not using Sunny table).
        /// </summary>
        public EzMinaSkillAxis DominantAxis { get; init; } = EzMinaSkillAxis.Stream;

        public double OverallMsd { get; init; }

        public double HoldRatio { get; init; }

        public int AlgorithmVersion { get; init; } = EzDanAlgorithm.VERSION;
    }
}
