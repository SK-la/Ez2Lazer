// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 各 ruleset Session 共用的 Timeline 快照采集器（上收自 Osu/Mania recorder 形态）。
    /// </summary>
    public sealed class EzReplayTimelineRecorder
    {
        private readonly List<EzScoreTimelineSnapshot> snapshots = new List<EzScoreTimelineSnapshot>();
        private double lastClockTime = double.NegativeInfinity;

        public void RecordInitial(ScoreProcessor scoreProcessor, double gameplayRate)
            => appendSnapshot(0, scoreProcessor, gameplayRate);

        public void Record(ScoreProcessor scoreProcessor, double clockTime, double gameplayRate)
            => appendSnapshot(clockTime, scoreProcessor, gameplayRate);

        public EzScoreTimeline Build()
        {
            if (snapshots.Count == 0)
                snapshots.Add(new EzScoreTimelineSnapshot { ClockTime = 0 });
            else if (snapshots[0].ClockTime > 0)
                snapshots.Insert(0, new EzScoreTimelineSnapshot { ClockTime = 0 });

            return new EzScoreTimeline(snapshots);
        }

        private void appendSnapshot(double clockTime, ScoreProcessor scoreProcessor, double gameplayRate)
        {
            if (clockTime <= lastClockTime)
                clockTime = lastClockTime + 0.001;

            lastClockTime = clockTime;
            snapshots.Add(EzScoreTimelineSnapshot.Create(clockTime, scoreProcessor, gameplayRate));
        }
    }
}
