// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Beatmaps
{
    public class KpsCalculator
    {
        public static (double averageKps, double maxKps, List<double> kpsList) CalculateKps(Beatmap beatmap)
        {
            var hitObjects = beatmap.HitObjects;
            if (hitObjects.Count == 0)
                return (0, 0, new List<double>());

            double totalDuration = double.Abs(hitObjects.Last().StartTime - hitObjects.First().StartTime) / 1000.0;
            double totalHits = hitObjects.Count;

            double averageKps = totalHits / totalDuration;

            double interval = 60000.0 / (beatmap.BeatmapInfo.BPM);
            List<double> kpsList = new List<double>();

            const double song_start_time = 0;
            double songEndTime = hitObjects.Last().StartTime;

            for (double currentTime = song_start_time; currentTime < songEndTime; currentTime += interval)
            {
                double endTime = currentTime + interval;
                int hitsInInterval = hitObjects.Count(h => h.StartTime >= currentTime && h.StartTime < endTime);

                double kps = hitsInInterval / (interval / 1000.0);
                kpsList.Add(kps);
            }

            double maxKps = kpsList.Max();

            return (averageKps, maxKps, kpsList);
        }
    }
}
