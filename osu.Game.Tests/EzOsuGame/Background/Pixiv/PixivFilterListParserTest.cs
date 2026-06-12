// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Background.Pixiv;

namespace osu.Game.Tests.EzOsuGame.Background.Pixiv
{
    [TestFixture]
    public class PixivFilterListParserTest
    {
        [Test]
        public void TestSpaceSeparatedAccounts()
        {
            var parsed = PixivFilterListParser.Parse("foo bar  baz");
            Assert.That(parsed, Is.EqualTo(new[] { "foo", "bar", "baz" }));
        }

        [Test]
        public void TestCommaSeparatedEntries()
        {
            var parsed = PixivFilterListParser.Parse("foo, bar;baz");
            Assert.That(parsed, Is.EqualTo(new[] { "foo", "bar", "baz" }));
        }

        [Test]
        public void TestCommaPreservesTagWithSpaces()
        {
            var parsed = PixivFilterListParser.Parse("女の子,AI Generated,オリジナル");
            Assert.That(parsed, Is.EqualTo(new[] { "女の子", "AI Generated", "オリジナル" }));
        }
    }
}
