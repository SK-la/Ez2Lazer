// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Models;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.EzOsuGame.Statistics
{
    [TestFixture]
    public class EzBeatmapExportUtilsTest
    {
        [Test]
        public void TestApplyExportMetadataRemovesOnlineIdentityFromEncodedBeatmap()
        {
            var beatmap = new Beatmap();
            beatmap.Metadata.Artist = "artist";
            beatmap.Metadata.Title = "title";
            beatmap.Metadata.Author = new RealmUser
            {
                Username = "original_creator",
                OnlineID = 1234,
            };

            beatmap.BeatmapInfo.DifficultyName = "difficulty";
            beatmap.BeatmapInfo.OnlineID = 5678;
            beatmap.BeatmapInfo.Status = BeatmapOnlineStatus.Ranked;
            beatmap.BeatmapInfo.BeatmapSet = new BeatmapSetInfo
            {
                OnlineID = 9876,
                Status = BeatmapOnlineStatus.Loved,
                DateRanked = System.DateTimeOffset.UtcNow,
                DateSubmitted = System.DateTimeOffset.UtcNow,
            };

            BeatmapExportUtils.ApplyExportMetadata(beatmap, new[] { new OsuModHidden() });

            string encoded = encode(beatmap);

            Assert.That(beatmap.BeatmapInfo.OnlineID, Is.EqualTo(-1));
            Assert.That(beatmap.BeatmapInfo.BeatmapSet?.OnlineID, Is.EqualTo(-1));
            Assert.That(beatmap.Metadata.Author.OnlineID, Is.EqualTo(-1));
            Assert.That(beatmap.BeatmapInfo.Status, Is.EqualTo(BeatmapOnlineStatus.None));
            Assert.That(beatmap.BeatmapInfo.BeatmapSet?.Status, Is.EqualTo(BeatmapOnlineStatus.None));
            Assert.That(beatmap.BeatmapInfo.BeatmapSet?.DateRanked, Is.Null);
            Assert.That(beatmap.BeatmapInfo.BeatmapSet?.DateSubmitted, Is.Null);
            Assert.That(encoded, Does.Contain("Creator: Ez2Lazer Mods=HD"));
            Assert.That(encoded, Does.Not.Contain("BeatmapID:"));
            Assert.That(encoded, Does.Not.Contain("BeatmapSetID:"));
        }

        private static string encode(IBeatmap beatmap)
        {
            using var stream = BeatmapExportUtils.EncodeToStream(beatmap, null);

            using var reader = new StreamReader(stream, leaveOpen: true);
            return reader.ReadToEnd();
        }
    }
}
