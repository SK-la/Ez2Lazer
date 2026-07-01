// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Scoring
{
    // TODO: 考虑找地方增加实时速度倍率快照
    public readonly struct EzScoreTimelineSnapshot
    {
        public double ClockTime { get; init; }
        public long TotalScore { get; init; }
        public double Accuracy { get; init; }
        public int Combo { get; init; }
        public int HighestCombo { get; init; }
        public int MissCount { get; init; }

        public static EzScoreTimelineSnapshot Empty { get; } = new EzScoreTimelineSnapshot();
    }
}
