// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using NUnit.Framework;
using osu.Game.EzOsuGame;

namespace osu.Game.Tests.NonVisual.EzOsuGame
{
    /// <summary>
    /// 锁定 <c>keysColumnWidth</c> 的解析：只有显式开启、且按键为平铺型的键数按列宽缩放。
    /// </summary>
    [TestFixture]
    public sealed class EzStageKeyColumnWidthTest
    {
        // 平铺型：每列各自的中心 x。
        private const string tiled_keys = @"""keys"": { ""7"": [ { ""path"": ""KeyBase"", ""x"": -60 }, { ""path"": ""KeyBase"", ""x"": 60 } ] }";

        // 整幅画布型：一张图覆盖整个面板，各列 x 全等。
        private const string canvas_keys = @"""keys"": { ""7"": [ { ""path"": ""1KeyBase"", ""x"": 0 }, { ""path"": ""2KeyBase"", ""x"": 0 } ] }";

        [Test]
        public void TestTiledDeclaredModeUsesColumnWidth()
        {
            EzStagePlateLayout layout = parse(@"{ ""keysColumnWidth"": { ""7"": true }, " + tiled_keys + " }");

            Assert.That(layout.UsesColumnWidth(7), Is.True);
        }

        [Test]
        public void TestCanvasDeclaredModeKeepsSourceSize()
        {
            EzStagePlateLayout layout = parse(@"{ ""keysColumnWidth"": { ""7"": true }, " + canvas_keys + " }");

            Assert.That(layout.UsesColumnWidth(7), Is.False);
        }

        [Test]
        public void TestUndeclaredModeKeepsSourceSize()
        {
            EzStagePlateLayout layout = parse(@"{ ""keysColumnWidth"": { ""7"": true }, " + tiled_keys + " }");

            Assert.That(layout.UsesColumnWidth(4), Is.False);
            Assert.That(layout.UsesColumnWidth(5), Is.False);
            Assert.That(layout.UsesColumnWidth(8), Is.False);
        }

        [Test]
        public void TestDisabledModeKeepsSourceSize()
        {
            EzStagePlateLayout layout = parse(@"{ ""keysColumnWidth"": { ""7"": false }, " + tiled_keys + " }");

            Assert.That(layout.UsesColumnWidth(7), Is.False);
        }

        [Test]
        public void TestMissingSectionKeepsSourceSize()
        {
            EzStagePlateLayout layout = parse(@"{ ""designWidth"": 412, " + tiled_keys + " }");

            Assert.That(layout.UsesColumnWidth(7), Is.False);
        }

        private static EzStagePlateLayout parse(string json)
            => JsonSerializer.Deserialize<EzStagePlateLayout>(json) ?? new EzStagePlateLayout();
    }
}
