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

        [Test]
        public void TestOfficialAndRewrittenApiUrlsAreAllowed()
        {
            const string proxy = "https://pixiv.example.com";
            const string vercel = "https://pixiv-proxy-ivory.vercel.app/api";

            Assert.That(PixivApiProxy.IsAllowedApiUrl(official_follow, null), Is.True);
            Assert.That(PixivApiProxy.IsAllowedApiUrl(official_follow, "   "), Is.True);
            Assert.That(PixivApiProxy.IsAllowedApiUrl(PixivApiProxy.RewriteApiUrl(official_follow, proxy), proxy), Is.True);
            Assert.That(PixivApiProxy.IsAllowedApiUrl(PixivApiProxy.RewriteApiUrl(official_follow, vercel), vercel), Is.True);
            Assert.That(PixivApiProxy.IsAllowedApiUrl("https://pixiv.example.com/v2/illust/follow?restrict=public&max_id=123", proxy), Is.True);
        }

        [Test]
        public void TestAttackerNextUrlIsNotRewrittenAndIsRejected()
        {
            const string proxy = "https://pixiv.example.com";
            const string next = "https://attacker.example/collect";

            Assert.That(PixivApiProxy.RewriteApiUrl(next, proxy), Is.EqualTo(next));
            Assert.That(PixivApiProxy.IsAllowedApiUrl(next, proxy), Is.False);
            Assert.That(PixivApiProxy.IsAllowedApiUrl(next, null), Is.False);
        }

        [Test]
        public void TestApiAllowlistRejectsUntrustedHosts()
        {
            const string proxy = "https://pixiv.example.com";

            Assert.That(PixivApiProxy.IsAllowedApiUrl("http://app-api.pixiv.net/v2/illust/follow", null), Is.False);
            Assert.That(PixivApiProxy.IsAllowedApiUrl("https://app-api.pixiv.net:4443/v2/illust/follow", null), Is.False);
            Assert.That(PixivApiProxy.IsAllowedApiUrl("https://app-api.pixiv.net.attacker.example/v2", null), Is.False);
            Assert.That(PixivApiProxy.IsAllowedApiUrl("https://oauth.secure.pixiv.net/auth/token", proxy), Is.False);
            Assert.That(PixivApiProxy.IsAllowedApiUrl("http://pixiv.example.com/v2/illust/follow", proxy), Is.False);
            Assert.That(PixivApiProxy.IsAllowedApiUrl("https://pixiv.example.com:8443/v2/illust/follow", proxy), Is.False);
            Assert.That(PixivApiProxy.IsAllowedApiUrl("https://evil.pixiv.example.com/v2/illust/follow", proxy), Is.False);
            Assert.That(PixivApiProxy.IsAllowedApiUrl("https://pixiv.example.com.attacker.example/v2", proxy), Is.False);
            Assert.That(PixivApiProxy.IsAllowedApiUrl("/v2/illust/follow", proxy), Is.False);
            Assert.That(PixivApiProxy.IsAllowedApiUrl(string.Empty, proxy), Is.False);
            Assert.That(PixivApiProxy.IsAllowedApiUrl("https://app-api.pixiv.net@attacker.example/collect", proxy), Is.False);
            Assert.That(PixivApiProxy.IsAllowedApiUrl("https://attacker.example@app-api.pixiv.net/v2/illust/follow", null), Is.False);
        }

        [Test]
        public void TestImageAllowlist()
        {
            const string proxy = "https://pixiv.example.com";
            const string image = "https://i.pximg.net/img-original/img/2024/01/01/12345_p0.png";

            Assert.That(PixivApiProxy.IsAllowedImageUrl(image, null), Is.True);
            Assert.That(PixivApiProxy.IsAllowedImageUrl(PixivApiProxy.RewriteImageUrl(image, proxy), proxy), Is.True);
            Assert.That(PixivApiProxy.IsAllowedImageUrl("https://pixiv.example.com/image/img-original/img/a.png", proxy), Is.True);
            Assert.That(PixivApiProxy.IsAllowedImageUrl("https://example.com/foo.png", proxy), Is.False);
            Assert.That(PixivApiProxy.IsAllowedImageUrl("http://169.254.169.254/latest/meta-data/", proxy), Is.False);
            Assert.That(PixivApiProxy.IsAllowedImageUrl("http://127.0.0.1/", null), Is.False);
            Assert.That(PixivApiProxy.IsAllowedImageUrl("file:///etc/passwd", null), Is.False);
            Assert.That(PixivApiProxy.IsAllowedImageUrl("http://i.pximg.net/a.png", null), Is.False);
            Assert.That(PixivApiProxy.IsAllowedImageUrl("https://i.pximg.net.attacker.example/a.png", null), Is.False);
            Assert.That(PixivApiProxy.RewriteImageUrl("https://example.com/foo.png", proxy), Is.EqualTo("https://example.com/foo.png"));
        }

        [Test]
        public void TestLocalProxyDoesNotAllowOtherLoopbackTargets()
        {
            const string proxy = "http://127.0.0.1:8080";

            Assert.That(PixivApiProxy.IsAllowedImageUrl("http://127.0.0.1:8080/image/a.png", proxy), Is.True);
            Assert.That(PixivApiProxy.IsAllowedImageUrl("http://127.0.0.1/a.png", proxy), Is.False);
            Assert.That(PixivApiProxy.IsAllowedImageUrl("http://127.0.0.1:8081/a.png", proxy), Is.False);
            Assert.That(PixivApiProxy.IsAllowedApiUrl("http://127.0.0.1:8080/v2/illust/follow", proxy), Is.True);
            Assert.That(PixivApiProxy.IsAllowedApiUrl("http://169.254.169.254/collect", proxy), Is.False);
        }
    }
}
