// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Timing;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Diagnostics
{
    /// <summary>
    /// 密集下落标定的 CI 守卫：只断言存活数、池 excess 与每帧分配的量级，不断言绝对耗时。
    /// <para>
    /// 场景本身（真实 drawable + Triangles 皮肤）与 <c>TestSceneManiaDenseScrollCost</c> 相同，
    /// 差别是不带消融开关，因此可以在 Release 下编译并进入常规测试流程。
    /// </para>
    /// </summary>
    [TestFixture]
    public partial class ManiaDenseScrollAllocTest : OsuTestScene
    {
        private const double simulated_frame_ms = 1000.0 / 60;
        private const double warmup_ms = 4000;
        private const int sample_frames = 120;

        /// <summary>纯 tap、10 列、每列 20 KPS × 700 ms 的实测每帧分配约 24 KiB；此处留一个数量级的余量。</summary>
        private const long max_bytes_per_frame = 256 * 1024;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private readonly ManualClock manualClock = new ManualClock();
        private readonly FramedClock playfieldClock;

        private ManiaDenseScrollField field = null!;
        private long sampleStartBytes;
        private long sampleStartFrames;
        private bool sampling;

        public ManiaDenseScrollAllocTest()
        {
            playfieldClock = new FramedClock(manualClock);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            field = new ManiaDenseScrollField(
                new ManiaDenseScrollWorkload
                {
                    ColumnCount = 10,
                    PerColumnKps = 20,
                    DwellMs = 700,
                    HoldRatio = 0,
                },
                skins.GetSkin(TrianglesSkin.CreateInfo()),
                playfieldClock)
            {
                RelativeSizeAxes = Axes.Both,
            };

            Add(field);
        }

        protected override void Update()
        {
            base.Update();

            manualClock.CurrentTime += simulated_frame_ms;
            playfieldClock.ProcessFrame();

            if (!sampling)
                return;

            if (field.SimulatedFrames - sampleStartFrames < sample_frames)
                return;

            sampling = false;

            long bytes = GC.GetAllocatedBytesForCurrentThread() - sampleStartBytes;
            long frames = field.SimulatedFrames - sampleStartFrames;
            double bytesPerFrame = bytes / (double)frames;

            Logger.Log($"[DENSE-SCROLL-ALLOC] alive={field.AliveNoteCount} frames={frames} "
                       + $"alloc/frame={bytesPerFrame:F0}B {ManiaDrawablePoolSnapshot.FormatSummary()}", LoggingTarget.Runtime, LogLevel.Important);

            Assert.That(bytesPerFrame, Is.LessThan(max_bytes_per_frame), "per-frame allocation regressed");
        }

        [Test]
        public void TestSteadyStateFootprint()
        {
            AddUntilStep("warm up past pool expansion", () => field.Time.Current > warmup_ms);
            AddAssert("alive count within density formula", () =>
            {
                var workload = field.Workload;
                double resident = workload.AlivePerColumn * workload.ColumnCount;
                double tail = field.PassiveMissTailMs / workload.IntervalMs * workload.ColumnCount;

                return field.AliveNoteCount >= resident - workload.ColumnCount
                       && field.AliveNoteCount <= resident + tail + workload.ColumnCount;
            });
            AddAssert("no pool excess", () => ManiaDrawablePoolSnapshot.TotalExcess() == 0);
            AddStep("start sampling", () =>
            {
                sampleStartBytes = GC.GetAllocatedBytesForCurrentThread();
                sampleStartFrames = field.SimulatedFrames;
                sampling = true;
            });
            AddUntilStep("sampling finished", () => !sampling);
        }
    }
}
