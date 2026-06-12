// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Background.Pixiv;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.Tests.EzOsuGame.Background.Pixiv
{
    [TestFixture]
    public class PixivLandscapeFilterTest
    {
        [Test]
        public void TestLandscapeOnlyRejectsPortraitWhenEnabled()
        {
            using var host = new CleanRunHeadlessGameHost();
            var config = new Ez2ConfigManager(host.Storage);
            config.GetBindable<bool>(Ez2Setting.PixivLandscapeOnly).Value = true;
            var filters = new PixivFilterService(config);

            var portrait = new PixivIllustInfo("artist", 1, 0, "https://example.test/a.jpg", width: 1000, height: 1500);
            Assert.That(filters.TryGetContentFilterRejection(portrait, out string? reason), Is.False);
            Assert.That(reason, Is.EqualTo("landscape"));

            var landscape = new PixivIllustInfo("artist", 2, 0, "https://example.test/b.jpg", width: 1920, height: 1080);
            Assert.That(filters.PassesContentFilter(landscape), Is.True);

            host.Exit();
        }

        [Test]
        public void TestLandscapeOnlyAllowsPortraitWhenDisabled()
        {
            using var host = new CleanRunHeadlessGameHost();
            var config = new Ez2ConfigManager(host.Storage);
            var filters = new PixivFilterService(config);

            var portrait = new PixivIllustInfo("artist", 1, 0, "https://example.test/a.jpg", width: 1000, height: 1500);
            Assert.That(filters.PassesContentFilter(portrait), Is.True);

            host.Exit();
        }
    }
}
