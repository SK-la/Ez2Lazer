// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Background.Pixiv;

namespace osu.Game.Tests.EzOsuGame.Background.Pixiv
{
    [TestFixture]
    public class PixivFileNamerTest
    {
        [Test]
        public void TestDownloadFileNameUsesUserDisplayName()
        {
            var illust = new PixivIllustInfo("hitokomoru", 145826326, 0, "https://example.test/a.png", userName: "ヒトこもる");

            Assert.That(PixivFileNamer.BuildDownloadFileName(illust), Is.EqualTo("ヒトこもる_145826326_p0.png"));
            Assert.That(PixivFileNamer.BuildDownloadRelativePath(illust).Replace('\\', '/'), Is.EqualTo("EzResources/BG_PIXIV/ヒトこもる_145826326_p0.png"));
        }

        [Test]
        public void TestIdKeyFileNameForQueueLookup()
        {
            Assert.That(PixivFileNamer.BuildIdKeyFileName(145826326, 0, ".png"), Is.EqualTo("145826326_p0.png"));
        }

        [Test]
        public void TestTryParseDownloadFileName()
        {
            const string fileName = "ヒトこもる_145826326_p0.png";

            Assert.That(PixivFileNamer.TryParseFileName(fileName, out long illustId, out int page), Is.True);
            Assert.That(illustId, Is.EqualTo(145826326));
            Assert.That(page, Is.EqualTo(0));
            Assert.That(PixivFileNamer.TryParseFileLabel(fileName, out string fileLabel), Is.True);
            Assert.That(fileLabel, Is.EqualTo("ヒトこもる"));
        }

        [Test]
        public void TestTryParseIdKeyFileName()
        {
            const string fileName = "145826326_p0.png";

            Assert.That(PixivFileNamer.TryParseFileName(fileName, out long illustId, out int page), Is.True);
            Assert.That(illustId, Is.EqualTo(145826326));
            Assert.That(page, Is.EqualTo(0));
            Assert.That(PixivFileNamer.TryParseFileLabel(fileName, out _), Is.False);
        }
    }
}
