// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Scoring
{
    public sealed class EzScoreTimeline
    {
        private readonly EzScoreTimelineSnapshot[] snapshots;

        public long FinalTotalScore { get; }

        public EzScoreTimeline(IReadOnlyList<EzScoreTimelineSnapshot> snapshots)
        {
            if (snapshots.Count == 0)
            {
                this.snapshots = Array.Empty<EzScoreTimelineSnapshot>();
                FinalTotalScore = 0;
                return;
            }

            this.snapshots = new EzScoreTimelineSnapshot[snapshots.Count];
            for (int i = 0; i < snapshots.Count; i++)
                this.snapshots[i] = snapshots[i];

            FinalTotalScore = this.snapshots[^1].TotalScore;
        }

        public EzScoreTimelineSnapshot QueryAtTime(double clockTime)
        {
            if (snapshots.Length == 0)
                return EzScoreTimelineSnapshot.Empty;

            if (clockTime <= snapshots[0].ClockTime)
                return snapshots[0];

            int left = 0;
            int right = snapshots.Length - 1;

            while (left < right)
            {
                int mid = left + (right - left + 1) / 2;

                if (snapshots[mid].ClockTime <= clockTime)
                    left = mid;
                else
                    right = mid - 1;
            }

            return snapshots[left];
        }
    }
}
