// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Changelog;

namespace osu.Game.Tests.EzOsuGame.Changelog
{
    [TestFixture]
    public class EzChangelogParserTest
    {
        private const string bilingual_sample = """
            版本摘要（语言无关）

            ## zh

            ## 🎨 皮肤与 Stage

            - Ez Pro Skin 的 Stage 图纸移到前面板。
            - 内置 NoteSideLine 改用资源包（#207）

            ## en

            ## 🎨 Skins and Stage

            - Stage artwork now draws on the front panel.
            """;

        [Test]
        public void TestLocaleSliceChinese()
        {
            var result = EzChangelogParser.Parse(bilingual_sample, EzContentLocale.Zh);

            Assert.That(result.Markdown, Does.Contain("版本摘要"));
            Assert.That(result.Markdown, Does.Contain("皮肤与 Stage"));
            Assert.That(result.Markdown, Does.Contain("前面板"));
            Assert.That(result.Markdown, Does.Not.Contain("front panel"));
        }

        [Test]
        public void TestLocaleSliceEnglish()
        {
            var result = EzChangelogParser.Parse(bilingual_sample, EzContentLocale.En);

            Assert.That(result.Markdown, Does.Contain("版本摘要"));
            Assert.That(result.Markdown, Does.Contain("front panel"));
            Assert.That(result.Markdown, Does.Not.Contain("前面板"));
        }

        [Test]
        public void TestWholeBodyWithoutLocaleHeadings()
        {
            var result = EzChangelogParser.Parse("# Old release\n\n- something", EzContentLocale.Zh);

            Assert.That(result.Markdown, Does.Contain("Old release"));
            Assert.That(result.Markdown, Does.Contain("something"));
        }

        [Test]
        public void TestStripsLegacyFormatMarker()
        {
            const string body = """
                摘要

                <!-- ez-changelog-format: 1 -->

                ## zh

                - 自由排版，无需 [add]
                """;

            var result = EzChangelogParser.Parse(body, EzContentLocale.Zh);

            Assert.That(result.Markdown, Does.Not.Contain("ez-changelog-format"));
            Assert.That(result.Markdown, Does.Contain("摘要"));
            Assert.That(result.Markdown, Does.Contain("自由排版"));
        }
    }
}
