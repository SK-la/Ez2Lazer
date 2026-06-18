// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    public sealed class EzScoreRaceEntry
    {
        public ScoreInfo ScoreInfo { get; }
        public EzScoreTimeline? Timeline { get; internal set; }
        public bool Tracked { get; internal set; }

        public EzScoreRaceEntry(ScoreInfo scoreInfo, EzScoreTimeline? timeline = null, bool tracked = false)
        {
            ScoreInfo = scoreInfo;
            Timeline = timeline;
            Tracked = tracked;
        }
    }
}
