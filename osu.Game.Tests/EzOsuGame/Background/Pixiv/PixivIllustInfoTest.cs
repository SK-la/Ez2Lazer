// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Background.Pixiv;

namespace osu.Game.Tests.EzOsuGame.Background.Pixiv
{
    [TestFixture]
    public class PixivIllustInfoTest
    {
        [Test]
        public void TestAttributionLabelPrefersUserName()
        {
            var info = new PixivIllustInfo("swd3e22", 64419500, 0, string.Empty, userName: "サンプル画师");
            Assert.That(info.AttributionLabel, Is.EqualTo("サンプル画师_64419500"));
        }

        [Test]
        public void TestAttributionLabelFallsBackToAccount()
        {
            var info = new PixivIllustInfo("swd3e22", 64419500, 0, string.Empty);
            Assert.That(info.AttributionLabel, Is.EqualTo("swd3e22_64419500"));
        }
    }
}
