// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.Objects;

#if DEBUG
namespace osu.Game.Rulesets.Mania.Tests.EzMania.Diagnostics
{
    /// <summary>
    /// 消融开关里与绘制无关的部分：tick 生成。
    /// <para>
    /// 「head/tail 是否进非位置输入队列」不在此处断言：<c>HandleNonPositionalInput</c> 在框架里由
    /// <c>Drawable.HandleInputCache.RequestsNonPositionalInput</c> 决定，而该值只在 drawable 完成 load
    /// 时才写入（未挂树的实例恒为 false），裸 new 出来的 drawable 断言不出开关效果。
    /// 那三条已由挂树场景 <see cref="TestSceneManiaHoldDrawCost"/> 覆盖。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ManiaHoldAblationTest
    {
        [TearDown]
        public void TearDown() => ManiaHoldAblation.Reset();

        [Test]
        public void TestForceTickGenerationCreatesSixteenthTicks()
        {
            ManiaHoldAblation.ForceHoldTickGeneration = true;

            var note = createHold(duration: 1000);

            Assert.That(note.Ticks, Is.Not.Null);
            Assert.That(note.Ticks.Count, Is.GreaterThan(0));
        }

        [Test]
        public void TestDisableTickGenerationSuppressesForcedTicks()
        {
            ManiaHoldAblation.ForceHoldTickGeneration = true;
            ManiaHoldAblation.DisableHoldTickGeneration = true;

            var note = createHold(duration: 1000);

            Assert.That(note.Ticks, Is.Empty);
        }

        private static HoldNote createHold(double duration)
        {
            var note = new HoldNote { StartTime = 0, Duration = duration };
            note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return note;
        }
    }
}
#endif
