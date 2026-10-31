// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.EzMania.Audio;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Audio
{
    /// <summary>
    /// 按键取样对 LN 的取值：头在按下时刻前后都应作为键音来源。
    /// </summary>
    /// <remarks>
    /// 回归场景：LN 的键音只挂在头部，而上游取「结束时间晚于参考时间的最早嵌套对象」+「物件自身 miss 窗口」，
    /// 这两条对 <see cref="HoldNote"/> 都退化（头结束时间 == 头开始时间、父物件窗口为空），
    /// 结果按早落进退回分支取上一颗 note 的键音、按晚改取无样本的尾/体。
    /// </remarks>
    [TestFixture]
    public partial class EzGameplaySampleTriggerSourceTest
    {
        private TestEzGameplaySampleTriggerSource source = null!;

        [SetUp]
        public void SetUp()
        {
            source = new TestEzGameplaySampleTriggerSource();
        }

        [Test]
        public void TestHoldSampleWindowUsesHeadWindow()
        {
            var hold = createHoldNote();

            Assert.That(hold.HitWindows!.WindowFor(HitResult.Miss), Is.Zero,
                "前提：HoldNote 自身没有判定窗口，上游据此认为按早的 LN 还「离得太远」");

            Assert.That(source.WindowOf(hold), Is.EqualTo(hold.Head!.HitWindows!.WindowFor(HitResult.Miss)).Within(1e-9));
            Assert.That(source.WindowOf(hold), Is.GreaterThan(0));
        }

        [Test]
        public void TestHoldSelectsHeadWithinHeadWindow()
        {
            var hold = createHoldNote();
            var entry = new HitObjectLifetimeEntry(hold);

            Assert.That(source.SourceFor(entry, hold.StartTime - 50), Is.SameAs(hold.Head), "按早：仍取头");
            Assert.That(source.SourceFor(entry, hold.StartTime), Is.SameAs(hold.Head), "准时：取头");
            Assert.That(source.SourceFor(entry, hold.StartTime + 50), Is.SameAs(hold.Head), "按晚：仍取头");
        }

        [Test]
        public void TestNoteUnaffected()
        {
            var note = new Note { StartTime = 1000, Column = 0 };
            note.ApplyDefaults(createControlPointInfo(), new BeatmapDifficulty { OverallDifficulty = 8 });

            Assert.That(source.WindowOf(note), Is.EqualTo(note.HitWindows!.WindowFor(HitResult.Miss)));
            Assert.That(source.SourceFor(new HitObjectLifetimeEntry(note), note.StartTime - 50), Is.SameAs(note));
        }

        private static HoldNote createHoldNote()
        {
            var hold = new HoldNote
            {
                StartTime = 1000,
                Duration = 1500,
                Column = 0,
            };

            hold.ApplyDefaults(createControlPointInfo(), new BeatmapDifficulty { OverallDifficulty = 8 });

            return hold;
        }

        private static ControlPointInfo createControlPointInfo()
        {
            var controlPoints = new ControlPointInfo();
            controlPoints.Add(0, new TimingControlPoint { BeatLength = 500 });
            return controlPoints;
        }

        private partial class TestEzGameplaySampleTriggerSource : EzGameplaySampleTriggerSource
        {
            public TestEzGameplaySampleTriggerSource()
                : base(new HitObjectContainer())
            {
            }

            public double WindowOf(HitObject hitObject) => SampleWindowFor(hitObject);

            public HitObject? SourceFor(HitObjectLifetimeEntry entry, double referenceTime) => SelectSampleSource(entry, referenceTime);
        }
    }
}
