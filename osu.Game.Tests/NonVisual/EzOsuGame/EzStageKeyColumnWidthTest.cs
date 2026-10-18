// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using NUnit.Framework;
using osu.Game.EzOsuGame;

namespace osu.Game.Tests.NonVisual.EzOsuGame
{
    /// <summary>
    /// 锁定 <c>keysColumnWidth</c> 的解析：只有显式开启的键数按列宽缩放，其余保持源图尺寸。
    /// </summary>
    [TestFixture]
    public sealed class EzStageKeyColumnWidthTest
    {
        [Test]
        public void TestDeclaredModeUsesColumnWidth()
        {
            EzStagePlateLayout layout = parse(@"{ ""keysColumnWidth"": { ""7"": true, ""9"": true, ""10"": true } }");

            Assert.That(layout.UsesColumnWidth(7), Is.True);
            Assert.That(layout.UsesColumnWidth(9), Is.True);
            Assert.That(layout.UsesColumnWidth(10), Is.True);
        }

        [Test]
        public void TestUndeclaredModeKeepsSourceSize()
        {
            EzStagePlateLayout layout = parse(@"{ ""keysColumnWidth"": { ""7"": true } }");

            Assert.That(layout.UsesColumnWidth(4), Is.False);
            Assert.That(layout.UsesColumnWidth(5), Is.False);
            Assert.That(layout.UsesColumnWidth(8), Is.False);
        }

        [Test]
        public void TestDisabledModeKeepsSourceSize()
        {
            EzStagePlateLayout layout = parse(@"{ ""keysColumnWidth"": { ""7"": false } }");

            Assert.That(layout.UsesColumnWidth(7), Is.False);
        }

        [Test]
        public void TestMissingSectionKeepsSourceSize()
        {
            EzStagePlateLayout layout = parse(@"{ ""designWidth"": 412 }");

            Assert.That(layout.UsesColumnWidth(7), Is.False);
        }

        private static EzStagePlateLayout parse(string json)
            => JsonSerializer.Deserialize<EzStagePlateLayout>(json) ?? new EzStagePlateLayout();
    }
}
