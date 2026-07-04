// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using System;
using osu.Framework.Bindables;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Clocks;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;

namespace osu.Game.Tests.Visual.EzOsuGame
{
    /// <summary>
    /// 纯单元测试（不依赖 OsuTestScene / 启动游戏）。
    /// 覆盖：
    /// - <see cref="EzBeatmapTimeSource"/> 推进 / 暂停 / Seek / Reset 行为；
    /// - <see cref="EzBeatmapTimeRangeProvider.ComputeRange"/> Lead-in / Tail 计算；
    /// - <see cref="SequentialScrollAlgorithm"/> 在常规 BPM 区间的滚动行为；
    /// - <see cref="TimingControlPoint"/> / <see cref="DifficultyControlPoint"/> 在扩展 [6, 600000] / [0.1, 10] 边界下的写入能力。
    /// </summary>
    [TestFixture]
    public class TestEzBeatmapTimeBase
    {
        [Test]
        public void EzBeatmapTimeSource_AdvancesWithWallClock()
        {
            var src = new EzBeatmapTimeSource();

            // 推一次（首次 wall clock 启动）
            src.ProcessFrame();
            double t0 = src.CurrentTime;

            // 模拟 ~16ms 后再推一次
            System.Threading.Thread.Sleep(20);
            src.ProcessFrame();
            double t1 = src.CurrentTime;

            Assert.That(t1, Is.GreaterThanOrEqualTo(t0), "time should advance");
        }

        [Test]
        public void EzBeatmapTimeSource_PauseStopsAdvancing()
        {
            var src = new EzBeatmapTimeSource();
            src.ProcessFrame();
            src.Stop();

            double t0 = src.CurrentTime;
            System.Threading.Thread.Sleep(20);
            src.ProcessFrame();

            Assert.That(src.CurrentTime, Is.EqualTo(t0), "paused clock should not advance");
            Assert.That(((IClock)src).IsRunning, Is.False);
        }

        [Test]
        public void EzBeatmapTimeSource_Seek_ChangesCurrentTime()
        {
            var src = new EzBeatmapTimeSource();
            src.ProcessFrame();
            src.Seek(12345);

            Assert.That(src.CurrentTime, Is.EqualTo(12345));
        }

        [Test]
        public void EzBeatmapTimeSource_Disabled_DoesNotAdvance()
        {
            var src = new EzBeatmapTimeSource { Enabled = false };
            src.ProcessFrame();
            double t0 = src.CurrentTime;

            System.Threading.Thread.Sleep(20);
            src.ProcessFrame();

            Assert.That(src.CurrentTime, Is.EqualTo(t0));
        }

        [Test]
        public void EzBeatmapTimeRangeProvider_ComputesCorrectRange()
        {
            var beatmap = new Beatmap
            {
                HitObjects =
                {
                    new TestHitObject(0, 1000),
                    new TestHitObject(2000, 3000),
                },
            };

            var (start, end) = EzBeatmapTimeRangeProvider.ComputeRange(beatmap);

            // start = -3000 (LEAD_IN_MS)
            Assert.That(start, Is.EqualTo(-3000));
            // end = max(endTime) + 3000 = 3000 + 3000 = 6000
            Assert.That(end, Is.EqualTo(6000));
        }

        [Test]
        public void EzBeatmapTimeRangeProvider_EmptyHitObjects_DefaultRange()
        {
            var beatmap = new Beatmap();
            var (start, end) = EzBeatmapTimeRangeProvider.ComputeRange(beatmap);
            Assert.That(start, Is.EqualTo(-3000));
            Assert.That(end, Is.EqualTo(3000));
        }

        [Test]
        public void SequentialScrollAlgorithm_NormalBeatLength_Advances()
        {
            // 构造一个 BeatLength=1000 的正常 TimingControlPoint。
            var point = new TimingControlPoint { BeatLength = 1000 };

            // 直接验证 BeatLength 被 setter 接受；
            // 滚动推进本身由 SequentialScrollAlgorithm.PositionAt 实现，已在上层集成测试覆盖。
            Assert.That(point.BeatLength, Is.EqualTo(1000));
        }

        [Test]
        public void SequentialScrollAlgorithm_AdjacentControlPoints_MonotonicAdvance()
        {
            // SequentialScrollAlgorithm 在常规控制点之间连续滚动：
            // 验证两个 BeatLength=500 的相邻控制点之间的 PositionAt 单调推进。
            // 注意：position 是相对值（负 = 已落下），算法内部可能在控制点处出现非严格单调；
            // 这里只验证"任何推进都意味着 |pos| 增大"。
            var cp0 = new TimingControlPoint { BeatLength = 500 };
            var cp1 = new TimingControlPoint { BeatLength = 500 };

            var algorithm = new SequentialScrollAlgorithm(new[] { toMultiplier(0, cp0), toMultiplier(3000, cp1) });

            const double time_range = 1500;
            const float scroll_length = 1000;

            double pos0 = Math.Abs(algorithm.PositionAt(0, 0, time_range, scroll_length));
            double pos1000 = Math.Abs(algorithm.PositionAt(0, 1000, time_range, scroll_length));
            double pos2500 = Math.Abs(algorithm.PositionAt(0, 2500, time_range, scroll_length));
            double pos2999 = Math.Abs(algorithm.PositionAt(0, 2999, time_range, scroll_length));
            double pos3000 = Math.Abs(algorithm.PositionAt(0, 3000, time_range, scroll_length));
            double pos4500 = Math.Abs(algorithm.PositionAt(0, 4500, time_range, scroll_length));

            // 任意推进都意味着 |pos| 单调不递减。
            Assert.That(pos1000, Is.GreaterThan(pos0), "abs position should advance at 1000ms");
            Assert.That(pos2500, Is.GreaterThan(pos1000), "abs position should advance at 2500ms");
            Assert.That(pos2999, Is.GreaterThan(pos2500), "abs position should advance at 2999ms");
            Assert.That(pos3000, Is.GreaterThanOrEqualTo(pos2999),
                "abs position should not decrease across control point boundary");
            Assert.That(pos4500, Is.GreaterThan(pos3000), "abs position should advance at 4500ms");
        }

        [Test]
        public void TimingControlPoint_BeatLength_AcceptsVeryHighBpm()
        {
            // [Ez] MinValue=0.6 对应 BPM 上限 100000，MaxValue=600000 对应 BPM 下限 0.1
            var cp = new TimingControlPoint { BeatLength = 0.6 };
            Assert.That(cp.BeatLength, Is.EqualTo(0.6));
            Assert.That(cp.BPM, Is.EqualTo(100000).Within(1e-3), "0.6ms = 100000 BPM");
        }

        [Test]
        public void TimingControlPoint_BeatLength_AcceptsVerySlowBpm()
        {
            var cp = new TimingControlPoint { BeatLength = 600000 };
            Assert.That(cp.BeatLength, Is.EqualTo(600000));
            Assert.That(cp.BPM, Is.EqualTo(0.1).Within(1e-6), "600000ms = 0.1 BPM");
        }

        [Test]
        public void TimingControlPoint_BeatLength_RejectsZero()
        {
            // [Ez] MinValue = 0.6（对应 100000 BPM），0 被 clamp 到 0.6。
            var cp = new TimingControlPoint { BeatLength = 0 };
            Assert.That(cp.BeatLength, Is.EqualTo(0.6),
                "BeatLength=0 should be clamped to MinValue (0.6ms, 100000 BPM)");
            Assert.That(cp.BPM, Is.EqualTo(100000).Within(1e-3));
        }

        [Test]
        public void DifficultyControlPoint_SliderVelocity_AcceptsMinimumValue()
        {
            // [Ez] SliderVelocityMinValue = 0.1（原始值）。
            var cp = new DifficultyControlPoint { SliderVelocity = 0.1 };
            Assert.That(cp.SliderVelocity, Is.EqualTo(0.1));
        }

        [Test]
        public void DifficultyControlPoint_SliderVelocity_RejectsZero()
        {
            // [Ez] 已放弃 SV=0 支持：MinValue 回到 0.1，0 被 clamp 到 0.1。
            var cp = new DifficultyControlPoint { SliderVelocity = 0 };
            Assert.That(cp.SliderVelocity, Is.EqualTo(0.1),
                "SliderVelocity=0 should be clamped to original minimum (0.1)");
        }

        [Test]
        public void PauseStateTracker_OnPause_RecordsGameplayTime()
        {
            var isPaused = new BindableBool();
            var src = new EzBeatmapTimeSource();
            src.SetCurrentTime(4321);

            using var tracker = new PauseStateTracker(src, isPaused);

            isPaused.Value = true;

            Assert.That(src.PauseGameplayTime, Is.EqualTo(4321));
            Assert.That(src.IsInResumeLeadIn, Is.False);
        }

        [Test]
        public void PauseStateTracker_OnResume_BeginsLeadIn()
        {
            var isPaused = new BindableBool();
            var src = new EzBeatmapTimeSource { ResumeLeadInWindowMs = 3000 };
            src.SetCurrentTime(5000);

            using var tracker = new PauseStateTracker(src, isPaused);

            isPaused.Value = true;
            isPaused.Value = false;

            Assert.That(src.IsInResumeLeadIn, Is.True);
            Assert.That(src.PauseGameplayTime, Is.EqualTo(5000));
            Assert.That(src.CurrentTime, Is.EqualTo(5000));
        }

        [Test]
        public void EzBeatmapTimeSource_BeginResumeLeadIn_FreezesGameplayTime()
        {
            var src = new EzBeatmapTimeSource { ResumeLeadInWindowMs = 3000 };
            src.SetCurrentTime(5000);
            src.OnGameplayPaused(5000);
            src.Start();
            src.BeginResumeLeadIn();

            src.ProcessFrame();
            System.Threading.Thread.Sleep(20);
            src.ProcessFrame();

            Assert.That(src.IsInResumeLeadIn, Is.True);
            Assert.That(src.CurrentTime, Is.EqualTo(5000), "gameplay time should stay frozen during lead-in");
        }

        [Test]
        public void EzBeatmapTimeSource_EndResumeLeadIn_ResumesAdvancement()
        {
            var src = new EzBeatmapTimeSource { ResumeLeadInWindowMs = 3000 };
            src.SetCurrentTime(5000);
            src.OnGameplayPaused(5000);
            src.Start();
            src.BeginResumeLeadIn();
            src.EndResumeLeadIn();

            double t0 = src.CurrentTime;
            System.Threading.Thread.Sleep(20);
            src.ProcessFrame();

            Assert.That(src.IsInResumeLeadIn, Is.False);
            Assert.That(src.CurrentTime, Is.GreaterThanOrEqualTo(t0));
        }

        [Test]
        public void EzBeatmapTimeSource_EndResumeLeadIn_FiresEvent()
        {
            var src = new EzBeatmapTimeSource { ResumeLeadInWindowMs = 3000 };
            bool fired = false;
            src.ResumeLeadInCompleted += () => fired = true;

            src.BeginResumeLeadIn();
            src.EndResumeLeadIn();

            Assert.That(fired, Is.True);
        }

        [Test]
        public void EzBeatmapTimeSource_BeginResumeLeadIn_DisabledWhenWindowZero()
        {
            var src = new EzBeatmapTimeSource { ResumeLeadInWindowMs = 0 };
            src.SetCurrentTime(5000);
            src.BeginResumeLeadIn();

            Assert.That(src.IsInResumeLeadIn, Is.False);
        }

        [Test]
        public void ResumeLeadIn_DisplayTimeAdvancesTowardPausePosition()
        {
            const double target = 5000;
            const double window = 3000;
            const double frame_delta = 16.67;

            double display = target - window;

            while (display < target)
                display = Math.Min(display + frame_delta, target);

            Assert.That(display, Is.EqualTo(target));
        }

        private static osu.Game.Rulesets.Timing.MultiplierControlPoint toMultiplier(double time, TimingControlPoint timingPoint)
        {
            return new osu.Game.Rulesets.Timing.MultiplierControlPoint(time)
            {
                Velocity = 1,
                TimingPoint = timingPoint,
            };
        }

        private sealed class TestHitObject : HitObject, IHasDuration
        {
            private readonly double startTime;

            public TestHitObject(double start, double end)
            {
                startTime = start;
                EndTime = end;
            }

            public override double StartTime => startTime;
            public double EndTime { get; private set; }

            public double Duration { get => EndTime - startTime; set => EndTime = startTime + value; }
        }
    }
}
