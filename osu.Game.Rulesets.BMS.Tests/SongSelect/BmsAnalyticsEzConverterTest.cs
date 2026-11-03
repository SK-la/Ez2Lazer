// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;
using osu.Game.Rulesets.BMS.UI.SongSelect;

namespace osu.Game.Rulesets.BMS.Tests.SongSelect
{
    [TestFixture]
    public class BmsAnalyticsEzConverterTest
    {
        [Test]
        public void ToEzAnalysisResult_maps_pp_kps_and_xxy_sr()
        {
            var record = new BmsAnalyticsRecord
            {
                PathKey = "abc",
                Pp = 123.4,
                XxySr = 5.6,
                AvgKps = 7.8,
                MaxKps = 9.0,
                ColumnCountsJson = "{\"1\":10,\"2\":5}",
            };

            Assert.That(BmsAnalyticsEzConverter.ToEzAnalysisResult(record), Is.Not.Null);

            var result = BmsAnalyticsEzConverter.ToEzAnalysisResult(record)!.Value;
            Assert.That(result.Pp, Is.EqualTo(123.4));
            Assert.That(result.AverageKps, Is.EqualTo(7.8));
            Assert.That(result.MaxKps, Is.EqualTo(9.0));
            Assert.That(result.ManiaSummary?.XxySr, Is.EqualTo(5.6));
            Assert.That(result.ManiaSummary?.ColumnCounts[1], Is.EqualTo(10));
            Assert.That(result.ManiaSummary?.ColumnCounts[2], Is.EqualTo(5));
        }

        [Test]
        public void ToEzAnalysisResult_returns_null_for_empty_row()
        {
            var record = new BmsAnalyticsRecord { PathKey = "empty" };
            Assert.That(BmsAnalyticsEzConverter.ToEzAnalysisResult(record), Is.Null);
        }
    }
}
