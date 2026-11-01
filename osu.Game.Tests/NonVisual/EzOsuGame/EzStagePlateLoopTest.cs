// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using NUnit.Framework;
using osu.Game.EzOsuGame;

namespace osu.Game.Tests.NonVisual.EzOsuGame
{
    /// <summary>
    /// 锁定舞台多帧部件的循环默认值：layout 不写 <c>loop</c> 时按循环处理，只有显式 <c>false</c> 才停住。
    /// </summary>
    /// <remarks>
    /// 旧实现是「多帧即循环」，改成读 layout 后未写 <c>loop</c> 的舞台（如 JIYU 的 overObject）播一遍就停。
    /// </remarks>
    [TestFixture]
    public sealed class EzStagePlateLoopTest
    {
        [Test]
        public void TestUndefinedLoopReportsNull()
        {
            EzStagePlateLayout layout = parse(@"{ ""overObject"": { ""x"": 0, ""y"": -365 } }");

            Assert.That(layout.OverObject!.Loop, Is.Null);
        }

        [Test]
        public void TestExplicitLoopIsParsed()
        {
            Assert.That(parse(@"{ ""overObject"": { ""loop"": true } }").OverObject!.Loop, Is.True);
            Assert.That(parse(@"{ ""overObject"": { ""loop"": false } }").OverObject!.Loop, Is.False);
        }

        private static EzStagePlateLayout parse(string json)
            => JsonSerializer.Deserialize<EzStagePlateLayout>(json) ?? new EzStagePlateLayout();
    }
}
