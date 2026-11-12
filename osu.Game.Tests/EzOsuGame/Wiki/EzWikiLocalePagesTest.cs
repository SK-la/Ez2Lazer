// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Changelog;
using osu.Game.EzOsuGame.Wiki;

namespace osu.Game.Tests.EzOsuGame.Wiki
{
    [TestFixture]
    public class EzWikiLocalePagesTest
    {
        [Test]
        public void TestResolvePhysicalByLogicalKey()
        {
            var pages = EzWikiLocalePages.FromJson("""
                {
                  "home": { "zh": "Home-zh", "en": "Home-en" },
                  "song-select": { "zh": "选歌界面-中文", "en": "Song-Select-English" }
                }
                """);

            Assert.That(pages.ResolvePhysical("song-select", EzContentLocale.Zh), Is.EqualTo("选歌界面-中文"));
            Assert.That(pages.ResolvePhysical("song-select", EzContentLocale.En), Is.EqualTo("Song-Select-English"));
        }

        [Test]
        public void TestTryGetLogicalKeyFromPhysical()
        {
            var pages = EzWikiLocalePages.FromJson("""
                {
                  "song-select": { "zh": "选歌界面-中文", "en": "Song-Select-English" }
                }
                """);

            Assert.That(pages.TryGetLogicalKey("选歌界面-中文"), Is.EqualTo("song-select"));
        }

        [Test]
        public void TestSidebarExtractsZhSection()
        {
            const string sidebar = """
                # Ez2Lazer Wiki

                ## 中文
                - [选歌](./选歌界面-中文)

                ## English
                - [Song Select](./Song-Select-English)
                """;

            string zh = EzWikiSidebarParser.ExtractLocaleSection(sidebar, EzContentLocale.Zh);
            string en = EzWikiSidebarParser.ExtractLocaleSection(sidebar, EzContentLocale.En);

            Assert.That(zh, Does.Contain("选歌界面-中文"));
            Assert.That(zh, Does.Not.Contain("Song-Select-English"));
            Assert.That(en, Does.Contain("Song-Select-English"));
            Assert.That(en, Does.Not.Contain("选歌界面-中文"));
        }

        [Test]
        public void TestSidebarExtractsH3LocaleHeadings()
        {
            const string sidebar = """
                ### 中文
                - [FAQ](./FAQ)

                ### English
                - [FAQ](./FAQ)
                """;

            string zh = EzWikiSidebarParser.ExtractLocaleSection(sidebar, EzContentLocale.Zh);

            Assert.That(zh, Does.Contain("[FAQ](./FAQ)"));
            Assert.That(zh, Does.Not.Contain("### English"));
        }

        [Test]
        public void TestRewritePhysicalLinkToLogical()
        {
            var pages = EzWikiLocalePages.FromJson("""
                {
                  "song-select": { "zh": "选歌界面-中文", "en": "Song-Select-English" }
                }
                """);

            const string source = "See [选歌](./选歌界面-中文) for details.";
            string rewritten = EzWikiMarkdownRewrite.RewritePhysicalLinks(source, pages);

            Assert.That(rewritten, Does.Contain("./@song-select"));
        }

        [Test]
        public void TestLinkParserLogicalAtPath()
        {
            var pages = EzWikiLocalePages.FromJson("""
                { "home": { "zh": "Home-zh", "en": "Home-en" } }
                """);

            Assert.That(EzWikiLinkParser.TryGetInGamePath("https://ez2lazer.local/wiki/home/@home", pages, out string path), Is.True);
            Assert.That(path, Is.EqualTo("home"));
        }
    }
}
