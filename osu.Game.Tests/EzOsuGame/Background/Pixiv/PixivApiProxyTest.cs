// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Background.Pixiv;

namespace osu.Game.Tests.EzOsuGame.Background.Pixiv
{
    [TestFixture]
    public class PixivApiProxyTest
    {
        private const string official_follow = "https://app-api.pixiv.net/v2/illust/follow?restrict=public";

        [Test]
        public void TestEmptyProxyLeavesOfficialUrl()
        {
            Assert.That(PixivApiProxy.RewriteApiUrl(official_follow, null), Is.EqualTo(official_follow));
            Assert.That(PixivApiProxy.RewriteApiUrl(official_follow, "   "), Is.EqualTo(official_follow));
        }

        [Test]
        public void TestCloudflareWorkerStyleProxy()
        {
            const string proxy = "https://pixiv.example.com";

            Assert.That(
                PixivApiProxy.RewriteApiUrl(official_follow, proxy),
                Is.EqualTo("https://pixiv.example.com/v2/illust/follow?restrict=public"));

            Assert.That(
                PixivApiProxy.RewriteApiUrl("https://app-api.pixiv.net/v1/user/detail?user_id=1", proxy),
                Is.EqualTo("https://pixiv.example.com/v1/user/detail?user_id=1"));
        }

        [Test]
        public void TestVercelApiPrefixProxy()
        {
            const string proxy = "https://pixiv-proxy-ivory.vercel.app/api";

            Assert.That(
                PixivApiProxy.RewriteApiUrl(official_follow, proxy),
                Is.EqualTo("https://pixiv-proxy-ivory.vercel.app/api/v2/illust/follow?restrict=public"));
        }

        [Test]
        public void TestNextUrlFromOfficialResponseIsRewritten()
        {
            const string proxy = "https://pixiv.example.com";
            const string next = "https://app-api.pixiv.net/v2/illust/follow?restrict=public&max_id=123";

            Assert.That(
                PixivApiProxy.RewriteApiUrl(next, proxy),
                Is.EqualTo("https://pixiv.example.com/v2/illust/follow?restrict=public&max_id=123"));
        }

        [Test]
        public void TestNonOfficialApiUrlIsUntouched()
        {
            const string oauth = "https://oauth.secure.pixiv.net/auth/token";

            Assert.That(
                PixivApiProxy.RewriteApiUrl(oauth, "https://pixiv.example.com"),
                Is.EqualTo(oauth));
        }

        [Test]
        public void TestCloudflareWorkerStyleImageProxy()
        {
            const string proxy = "https://pixiv.example.com";
            const string image = "https://i.pximg.net/img-original/img/2024/01/01/12345_p0.png";

            Assert.That(
                PixivApiProxy.RewriteImageUrl(image, proxy),
                Is.EqualTo("https://pixiv.example.com/image/img-original/img/2024/01/01/12345_p0.png"));
        }

        [Test]
        public void TestVercelImageProxy()
        {
            const string proxy = "https://pixiv-proxy-ivory.vercel.app/api";
            const string image = "https://i.pximg.net/img-original/img/2024/01/01/12345_p0.png";

            Assert.That(
                PixivApiProxy.RewriteImageUrl(image, proxy),
                Is.EqualTo("https://pixiv-proxy-ivory.vercel.app/api/image/img-original/img/2024/01/01/12345_p0.png"));
        }

        [Test]
        public void TestEmptyProxyLeavesImageUrl()
        {
            const string image = "https://i.pximg.net/img-original/img/2024/01/01/12345_p0.png";

            Assert.That(PixivApiProxy.RewriteImageUrl(image, null), Is.EqualTo(image));
        }

        [Test]
        public void TestNonPixivImageHostIsUntouched()
        {
            const string image = "https://example.com/foo.png";

            Assert.That(
                PixivApiProxy.RewriteImageUrl(image, "https://pixiv.example.com"),
                Is.EqualTo(image));
        }

        [Test]
        public void TestNormalizeBaseTrimsTrailingSlash()
        {
            Assert.That(PixivApiProxy.NormalizeBase("https://pixiv.example.com/"), Is.EqualTo("https://pixiv.example.com"));
        }
    }
}
