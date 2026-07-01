// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    public readonly struct EzScoreTimelineSnapshot
    {
        public double ClockTime { get; init; }
        public long TotalScore { get; init; }
        public double Accuracy { get; init; }
        public int Combo { get; init; }
        public int HighestCombo { get; init; }
        public int MissCount { get; init; }

        /// <summary>
        /// 实时速度倍率快照（Mod 倍率 × 时间倍率）。
        /// </summary>
        public double GameplayRate { get; init; }

        public static EzScoreTimelineSnapshot Empty { get; } = new EzScoreTimelineSnapshot();

        /// <summary>
        /// 从 <see cref="ScoreProcessor"/> 当前状态创建快照，MissCount 与统计量统一读取自 SP。
        /// </summary>
        public static EzScoreTimelineSnapshot Create(double clockTime, ScoreProcessor scoreProcessor, double gameplayRate = 1.0)
        {
            return new EzScoreTimelineSnapshot
            {
                ClockTime = clockTime,
                TotalScore = scoreProcessor.TotalScore.Value,
                Accuracy = scoreProcessor.Accuracy.Value,
                Combo = scoreProcessor.Combo.Value,
                HighestCombo = scoreProcessor.HighestCombo.Value,
                MissCount = scoreProcessor.Statistics.GetValueOrDefault(HitResult.Miss),
                GameplayRate = gameplayRate,
            };
        }
    }
}
