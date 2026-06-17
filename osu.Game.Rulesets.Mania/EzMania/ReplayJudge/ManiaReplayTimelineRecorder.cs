// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 在 <see cref="ManiaReplaySession"/> 同一遍 SP 判定中采集分数时间线快照。
    /// </summary>
    internal sealed class ManiaReplayTimelineRecorder
    {
        private readonly List<EzScoreTimelineSnapshot> snapshots = new List<EzScoreTimelineSnapshot>();
        private int missCount;
        private double lastClockTime = double.NegativeInfinity;

        public void RecordInitial(ScoreProcessor scoreProcessor)
            => appendSnapshot(0, scoreProcessor, HitResult.None);

        public void Record(ScoreProcessor scoreProcessor, double clockTime, HitResult result)
            => appendSnapshot(clockTime, scoreProcessor, result);

        public EzScoreTimeline Build()
        {
            if (snapshots.Count == 0)
                snapshots.Add(new EzScoreTimelineSnapshot { ClockTime = 0 });
            else if (snapshots[0].ClockTime > 0)
                snapshots.Insert(0, new EzScoreTimelineSnapshot { ClockTime = 0 });

            return new EzScoreTimeline(snapshots);
        }

        private void appendSnapshot(double clockTime, ScoreProcessor scoreProcessor, HitResult result)
        {
            if (result.IsMiss())
                missCount++;

            if (clockTime <= lastClockTime)
                clockTime = lastClockTime + 0.001;

            lastClockTime = clockTime;

            snapshots.Add(new EzScoreTimelineSnapshot
            {
                ClockTime = clockTime,
                TotalScore = scoreProcessor.TotalScore.Value,
                Accuracy = scoreProcessor.Accuracy.Value,
                Combo = scoreProcessor.Combo.Value,
                HighestCombo = scoreProcessor.HighestCombo.Value,
                MissCount = missCount,
            });
        }
    }
}
