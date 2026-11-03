// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.Scoring;

namespace osu.Game.Tests.EzOsuGame.LocalProfile
{
    [TestFixture]
    public class EzLocalProfileInsightsCalculatorTest
    {
        [Test]
        public void WeightedMedianUsesDecayWeights()
        {
            double? median = EzLocalProfileInsightsCalculator.GetWeightedMedian(new List<(double, double)>
            {
                (100, 1),
                (200, 0.05),
            });

            Assert.That(median, Is.EqualTo(100).Within(0.001));
        }

        [Test]
        public void CalculateBuildsKeySplitAndPpRange()
        {
            var plays = new List<EzLocalProfileInsightPlay>
            {
                play(4, 500, "DT", bpm: 180, daysAgo: 1),
                play(4, 400, "DT", bpm: 170, daysAgo: 10),
                play(7, 300, "", bpm: 150, daysAgo: 5),
                play(7, 200, "HT", bpm: 140, daysAgo: 20, convert: true),
            };

            var insights = EzLocalProfileInsightsCalculator.Calculate(plays);

            Assert.That(insights.SampleSize, Is.EqualTo(4));
            Assert.That(insights.KeySplit[0].KeyCount, Is.EqualTo(4));
            Assert.That(insights.KeySplit[0].Count, Is.EqualTo(2));
            Assert.That(insights.MostUsedMod!.Label, Is.EqualTo("DT"));
            Assert.That(insights.PpRange!.Top, Is.EqualTo(500));
            Assert.That(insights.PpRange.Bottom, Is.EqualTo(200));
            Assert.That(insights.KeyPpConverts, Is.EqualTo(1));
            Assert.That(insights.KeyPp.Count, Is.EqualTo(2)); // 4K + 7K native; convert excluded from 7K key pp
            Assert.That(insights.NewestTopPlay!.Pp, Is.EqualTo(500));
            Assert.That(insights.OldestTopPlay!.Pp, Is.EqualTo(200));
        }

        private static EzLocalProfileInsightPlay play(
            int keyCount,
            double pp,
            string mod,
            double bpm,
            int daysAgo,
            bool convert = false)
        {
            var row = new EzLocalProfileDrillScoreRow
            {
                ScoreId = Guid.NewGuid(),
                RulesetId = EzLocalProfileConstants.MANIA_RULESET_ID,
                Rank = ScoreRank.S,
                PpResolved = pp,
                Title = $"Map {pp}",
                Artist = "Artist",
                DifficultyName = $"{keyCount}K",
                Date = DateTimeOffset.UtcNow.AddDays(-daysAgo),
            };

            return new EzLocalProfileInsightPlay
            {
                Row = row,
                Pp = pp,
                KeyCount = keyCount,
                BeatmapOnlineId = (int)pp,
                IsConvert = convert,
                Bpm = bpm,
                Rate = 1,
                ModAcronyms = string.IsNullOrEmpty(mod) ? Array.Empty<string>() : new[] { mod },
                Date = row.Date,
                Title = row.Title,
                Artist = row.Artist,
                DifficultyName = row.DifficultyName,
                StarRating = 5,
                Rank = "S",
            };
        }
    }
}
