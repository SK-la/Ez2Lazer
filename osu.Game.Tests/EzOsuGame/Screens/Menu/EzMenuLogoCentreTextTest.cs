// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Fonts;
using osu.Game.EzOsuGame.Screens.Menu;
using osu.Game.Graphics;

namespace osu.Game.Tests.EzOsuGame.Screens.Menu
{
    [TestFixture]
    public class EzMenuLogoCentreTextTest
    {
        [TestCase("123,12", "123", 12f)]
        [TestCase("123，12", "123", 12f)]
        [TestCase("osu!,160", "osu!", 160f)]
        [TestCase("(´・ω・｀),80", "(´・ω・｀)", 80f)]
        [TestCase("hello,world,24", "hello,world", 24f)]
        [TestCase("  123, 12.5  ", "123", 12.5f)]
        public void TestParseTextAndSize(string raw, string expectedText, float expectedSize)
        {
            Assert.That(EzMenuLogoCentreText.TryParse(raw, out string text, out float size), Is.True);
            Assert.That(text, Is.EqualTo(expectedText));
            Assert.That(size, Is.EqualTo(expectedSize).Within(0.001f));
        }

        [Test]
        public void TestParseWithoutSizeUsesDefault()
        {
            Assert.That(EzMenuLogoCentreText.TryParse("(´・ω・｀)", out string text, out float size), Is.True);
            Assert.That(text, Is.EqualTo("(´・ω・｀)"));
            Assert.That(size, Is.EqualTo(EzMenuLogoCentreText.DEFAULT_FONT_SIZE));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(",12")]
        [TestCase("，80")]
        public void TestParseEmptyOrSizeOnlyFails(string? raw)
        {
            Assert.That(EzMenuLogoCentreText.TryParse(raw, out _, out _), Is.False);
        }

        [Test]
        public void TestParseClampsOversizedFont()
        {
            Assert.That(EzMenuLogoCentreText.TryParse("big,9999", out string text, out float size), Is.True);
            Assert.That(text, Is.EqualTo("big"));
            Assert.That(size, Is.EqualTo(EzMenuLogoCentreText.MAX_FONT_SIZE));
        }

        [Test]
        public void TestResolveFontPrefersLocalizedSlotForKaomoji()
        {
            var font = EzMenuLogoCentreText.ResolveFont(80, "Segoe UI", "微软雅黑");
            Assert.That(font.Family, Is.EqualTo(EzUiFontIds.UI_DEFAULT_LOCALIZED));
            Assert.That(font.Size, Is.EqualTo(80));
        }

        [Test]
        public void TestResolveFontUsesDefaultUiRemapWhenLocalizedEmpty()
        {
            string? previous = OsuFont.HasFamilyOverride(Typeface.Torus) ? OsuFont.GetFamilyString(Typeface.Torus) : null;
            OsuFont.SetFamilyOverride(Typeface.Torus, EzUiFontIds.UI_DEFAULT);

            try
            {
                var font = EzMenuLogoCentreText.ResolveFont(48, "Segoe UI", string.Empty);
                Assert.That(font.Family, Is.EqualTo(EzUiFontIds.UI_DEFAULT));
            }
            finally
            {
                OsuFont.SetFamilyOverride(Typeface.Torus, previous);
            }
        }

        [Test]
        public void TestResolveFontFallsBackToBuiltInTorus()
        {
            string? previous = OsuFont.HasFamilyOverride(Typeface.Torus) ? OsuFont.GetFamilyString(Typeface.Torus) : null;
            OsuFont.SetFamilyOverride(Typeface.Torus, null);

            try
            {
                var font = EzMenuLogoCentreText.ResolveFont(80, string.Empty, null);
                Assert.That(font.Family, Is.EqualTo("Torus"));
            }
            finally
            {
                OsuFont.SetFamilyOverride(Typeface.Torus, previous);
            }
        }
    }
}
