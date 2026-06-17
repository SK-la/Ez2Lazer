// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    internal static class EzScoreTimelineJudgementTime
    {
        public static double Get(HitEvent hitEvent, bool offsetsRelativeToEnd, HitObject? beatmapHitObject = null, double fallbackMissWindow = 0)
        {
            double rate = hitEvent.GameplayRate ?? 1.0;
            double offset = hitEvent.TimeOffset / rate;

            var timingObject = beatmapHitObject ?? hitEvent.HitObject;
            var windowObject = beatmapHitObject ?? hitEvent.HitObject;

            double startTime = timingObject.StartTime;
            double endTime = timingObject.GetEndTime();

            double judgementTime = offsetsRelativeToEnd
                ? endTime + offset
                : startTime + offset;

            double missWindow = windowObject.HitWindows != null && windowObject.HitWindows != HitWindows.Empty
                ? windowObject.HitWindows.WindowFor(HitResult.Miss)
                : 0;

            if (missWindow <= 0 && fallbackMissWindow > 0)
                missWindow = fallbackMissWindow;

            if (missWindow > 0)
                judgementTime = Math.Max(startTime - missWindow, judgementTime);

            return judgementTime;
        }
    }
}
