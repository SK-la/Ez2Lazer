// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Background.Pixiv;

namespace osu.Game.Tests.EzOsuGame.Background.Pixiv
{
    [TestFixture]
    public class PixivIllustPageSelectorTest
    {
        [Test]
        public void TestMultiPageWorkAlwaysUsesPageZero()
        {
            var token = JObject.Parse("""
                {
                  "page_count": 3,
                  "width": 1920,
                  "height": 1080,
                  "meta_pages": [
                    { "width": 1000, "height": 1500 },
                    { "width": 2000, "height": 900 }
                  ]
                }
                """);

            Assert.That(PixivIllustPageSelector.TrySelectDisplayPage(token, landscapeOnly: false, out int width, out int height), Is.True);
            Assert.That(width, Is.EqualTo(1920));
            Assert.That(height, Is.EqualTo(1080));
        }

        [Test]
        public void TestLandscapeOnlyJudgesPageZeroOnly()
        {
            var portraitFirstPage = JObject.Parse("""
                {
                  "page_count": 2,
                  "width": 900,
                  "height": 1600,
                  "meta_pages": [
                    { "width": 1920, "height": 1080 }
                  ]
                }
                """);

            Assert.That(PixivIllustPageSelector.TrySelectDisplayPage(portraitFirstPage, landscapeOnly: true, out _, out _), Is.False);

            var landscapeFirstPage = JObject.Parse("""
                {
                  "page_count": 2,
                  "width": 1920,
                  "height": 1080,
                  "meta_pages": [
                    { "width": 900, "height": 1600 }
                  ]
                }
                """);

            Assert.That(PixivIllustPageSelector.TrySelectDisplayPage(landscapeFirstPage, landscapeOnly: true, out int width, out int height), Is.True);
            Assert.That(width, Is.EqualTo(1920));
            Assert.That(height, Is.EqualTo(1080));
        }
    }
}
