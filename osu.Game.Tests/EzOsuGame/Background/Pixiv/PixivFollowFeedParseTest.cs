// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Background.Pixiv;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.Tests.EzOsuGame.Background.Pixiv
{
    [TestFixture]
    public class PixivFollowFeedParseTest
    {
        [Test]
        public void TestExtractIllustTokensFromResponseWrapper()
        {
            const string sample = """
                {
                  "response": {
                    "illusts": [
                      {
                        "id": 1,
                        "type": "illust",
                        "image_urls": { "large": "https://example.test/a.jpg" },
                        "user": { "account": "artist" },
                        "width": 1920,
                        "height": 1080,
                        "visible": true
                      }
                    ]
                  },
                  "next_url": "https://app-api.pixiv.net/v2/illust/follow?restrict=public&offset=30"
                }
                """;

            var json = JObject.Parse(sample);
            Assert.That(PixivJsonHelper.ExtractIllustTokens(json), Has.Count.EqualTo(1));
            Assert.That(PixivJsonHelper.ResolveNextUrl(json), Does.Contain("offset=30"));
        }

        [Test]
        public void TestIllustParsesAndRespectsR18Filter()
        {
            const string sample = """
                {
                  "id": 64419500,
                  "type": "illust",
                  "image_urls": { "large": "https://i.pximg.net/example.jpg" },
                  "user": { "id": 22124330, "account": "swd3e22" },
                  "tags": [{ "name": "オリジナル" }],
                  "page_count": 1,
                  "width": 2126,
                  "height": 1150,
                  "sanity_level": 4,
                  "visible": true
                }
                """;

            var token = JObject.Parse(sample);
            Assert.That(PixivJsonHelper.LongValue(PixivJsonHelper.UnwrapIllust(token), "id"), Is.EqualTo(64419500));
            Assert.That(PixivJsonHelper.IntValue(token, "width"), Is.GreaterThan(PixivJsonHelper.IntValue(token, "height")));

            using var host = new CleanRunHeadlessGameHost();
            var config = new Ez2ConfigManager(host.Storage);
            var filters = new PixivFilterService(config);

            var info = new PixivIllustInfo("swd3e22", 64419500, 0, "https://example.test/a.jpg", 4, new[] { "オリジナル" }, "illust", 2126, 1150);
            Assert.That(filters.PassesContentFilter(info), Is.False, "R-18 illust should be filtered when PixivAllowR18 is off.");

            config.GetBindable<bool>(Ez2Setting.PixivAllowR18).Value = true;
            Assert.That(filters.PassesContentFilter(info), Is.True, "R-18 illust should pass when PixivAllowR18 is on.");

            host.Exit();
        }
    }
}
