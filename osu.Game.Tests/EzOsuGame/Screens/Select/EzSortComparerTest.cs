// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.Tests.EzOsuGame.Screens.Select
{
    [TestFixture]
    public class EzSortComparerTest
    {
        private CultureInfo originalCulture = null!;

        [SetUp]
        public void SetUp()
        {
            originalCulture = CultureInfo.CurrentUICulture;
        }

        [TearDown]
        public void TearDown()
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }

        [Test]
        public void TestChineseUiSortsHanByPinyin()
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");

            var sorted = new List<string> { "词典", "阿爸", "波纹", "测试", "爱" };
            sorted.Sort(EzSortComparer.ForCurrentUiCulture);

            Assert.That(sorted, Is.EqualTo(new[] { "阿爸", "爱", "波纹", "测试", "词典" }));
        }

        [Test]
        public void TestNonChineseUiKeepsOrdinalOrder()
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            var sorted = new List<string> { "阿", "zzz", "AAA" };
            sorted.Sort(EzSortComparer.ForCurrentUiCulture);

            Assert.That(sorted, Is.EqualTo(new[] { "AAA", "zzz", "阿" }));
        }

        [Test]
        public void TestChineseUiGroupsHanByPinyinInitial()
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");

            Assert.That(EzSortComparer.TryGetPinyinGroupLetter("阿爸", out char letter) && letter == 'A', Is.True);
            Assert.That(EzSortComparer.TryGetPinyinGroupLetter("波纹", out letter) && letter == 'B', Is.True);
            Assert.That(EzSortComparer.TryGetPinyinGroupLetter("测试", out letter) && letter == 'C', Is.True);
            Assert.That(EzSortComparer.TryGetPinyinGroupLetter("在", out letter) && letter == 'Z', Is.True);

            Assert.That(EzSortComparer.TryGetPinyinGroupLetter("abc", out _), Is.False);
            Assert.That(EzSortComparer.TryGetPinyinGroupLetter("の", out _), Is.False);
        }

        [Test]
        public void TestNonChineseUiHasNoPinyinGrouping()
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            Assert.That(EzSortComparer.TryGetPinyinGroupLetter("波纹", out _), Is.False);
        }
    }
}
