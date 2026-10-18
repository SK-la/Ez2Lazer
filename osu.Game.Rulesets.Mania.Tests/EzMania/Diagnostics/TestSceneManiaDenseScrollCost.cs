// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.EzOsuGame.Diagnostics;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;

#if DEBUG
namespace osu.Game.Rulesets.Mania.Tests.EzMania.Diagnostics
{
    /// <summary>
    /// 密集下落标定场景：真实 drawable、真实皮肤组件、固定模拟步长。
    /// <para>
    /// 密度口径来自 <see cref="ManiaDenseScrollWorkload"/>：每列 10 / 15 / 20 KPS，驻留 300 / 500 / 700 ms。
    /// 每秒（模拟时间）输出一行 <c>[DENSE-SCROLL]</c>，包含 update/draw 帧时、每帧分配、GC、稳态堆、
    /// 存活 drawable 数、池占用与判定计数器。
    /// </para>
    /// <para>
    /// 只在 DEBUG 下提供；Release 不编译。场景本身没有 <c>DrawableManiaRuleset</c>，因此没有 explosion，
    /// 按帧落 Miss 走的是 <c>DrawableNote.CheckForResult</c> 普通路径；判定下限以
    /// <c>BenchmarkManiaLaneHotPath</c> 的每列口径结果为准。
    /// </para>
    /// </summary>
    [TestFixture]
    public partial class TestSceneManiaDenseScrollCost : OsuTestScene
    {
        private const double simulated_frame_ms = 1000.0 / 60;
        private const double log_interval_ms = 1000;

        [Cached(typeof(IReadOnlyList<Mod>))]
        private IReadOnlyList<Mod> mods { get; set; } = Array.Empty<Mod>();

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private readonly ManualClock manualClock = new ManualClock();
        private readonly FramedClock playfieldClock;

        private ManiaDenseScrollField? field;
        private OsuSpriteText? summaryText;

        private int columnCount = 10;
        private double perColumnKps = 20;
        private double dwellMs = 700;
        private double holdRatio;
        private SkinInfo skinInfo = TrianglesSkin.CreateInfo();

        private bool freezeClock;
        private bool restoreDiagnostics;

        private double nextLogAt;
        private long lastAllocated;
        private int lastGen0;
        private int lastGen1;
        private int lastGen2;
        private long lastFrames;
        private long frameSamples;
        private double updateMsSum;
        private double drawMsSum;

        public TestSceneManiaDenseScrollCost()
        {
            playfieldClock = new FramedClock(manualClock);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            skins.CurrentSkinInfo.Value = skins.GetSkin(skinInfo).SkinInfo;

            Add(summaryText = new OsuSpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Font = OsuFont.GetFont(size: 12),
                Margin = new MarginPadding(8),
                Text = "waiting for dense scroll…",
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            restoreDiagnostics = EzJudgmentDiagnostics.Enabled;
            EzJudgmentDiagnostics.Enabled = true;

            rebuildField();
        }

        protected override void Update()
        {
            base.Update();

            if (!freezeClock)
                manualClock.CurrentTime += simulated_frame_ms;

            playfieldClock.ProcessFrame();

            if (field == null)
                return;

            // 单帧 ElapsedFrameTime 抖动大，采样窗口内累加后取平均。
            frameSamples++;
            updateMsSum += host.UpdateThread.Clock.ElapsedFrameTime;
            drawMsSum += host.DrawThread?.Clock.ElapsedFrameTime ?? 0;

            if (field.Time.Current < nextLogAt)
                return;

            nextLogAt = field.Time.Current + log_interval_ms;
            logSample("steady");
        }

        protected override void Dispose(bool isDisposing)
        {
            EzJudgmentDiagnostics.Enabled = restoreDiagnostics;
            ManiaHoldAblation.Reset();
            ManiaDenseScrollAblation.Reset();
            base.Dispose(isDisposing);
        }

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            EzJudgmentDiagnostics.Enabled = true;
            ManiaHoldAblation.Reset();
            ManiaDenseScrollAblation.Reset();
            ManiaJudgeHotPathTrace.Clear();
            freezeClock = false;
            rebuildField();
        });

        [Test]
        public void TestInteractiveDensitySweep()
        {
            AddToggleStep("freeze clock", v => freezeClock = v);

            AddStep("4 columns", () => setColumns(4));
            AddStep("10 columns", () => setColumns(10));
            AddStep("16 columns", () => setColumns(16));

            AddStep("10 KPS / 300 ms", () => setDensity(10, 300));
            AddStep("10 KPS / 500 ms", () => setDensity(10, 500));
            AddStep("10 KPS / 700 ms", () => setDensity(10, 700));
            AddStep("15 KPS / 500 ms", () => setDensity(15, 500));
            AddStep("20 KPS / 300 ms", () => setDensity(20, 300));
            AddStep("20 KPS / 500 ms", () => setDensity(20, 500));
            AddStep("20 KPS / 700 ms", () => setDensity(20, 700));

            AddStep("tap only", () => setHoldRatio(0));
            AddStep("50% hold", () => setHoldRatio(0.5));

            AddStep("skin triangles", () => setSkin(TrianglesSkin.CreateInfo()));
            AddStep("skin ez2", () => setSkin(Ez2Skin.CreateInfo()));
            AddStep("skin ez pro", () => setSkin(EzStyleProSkin.CreateInfo()));
            AddStep("skin sbI", () => setSkin(SbISkin.CreateInfo()));

            AddToggleStep("column watcher subscriptions", v =>
            {
                ManiaDenseScrollAblation.DisableColumnWatcherSubscriptions = !v;
                rebuildField();
            });
            AddToggleStep("per-frame layout writes", v => ManiaDenseScrollAblation.DisablePerFrameLayoutWrites = !v);
            AddToggleStep("subtraction stroke", v => ManiaHoldAblation.DisableSubtractionStroke = !v);
            AddToggleStep("hold tick scan", v => ManiaHoldAblation.DisableHoldTickScan = !v);

            AddStep("restart scene", rebuildField);
            AddStep("log sample now", () => logSample("manual"));
        }

        [Test]
        public void TestCalibrationSweep()
        {
            // 标定顺序 1-3：先核对最重档，再分别扫密度和驻留；最后叠加 hold、皮肤和两个订阅/布局消融。
            samplePreset("calib 10col 20kps 700ms tap", () => setPreset(10, 20, 700, 0));
            samplePreset("calib 10col 10kps 700ms tap", () => setPreset(10, 10, 700, 0));
            samplePreset("calib 10col 15kps 700ms tap", () => setPreset(10, 15, 700, 0));
            samplePreset("calib 10col 20kps 300ms tap", () => setPreset(10, 20, 300, 0));
            samplePreset("calib 10col 20kps 500ms tap", () => setPreset(10, 20, 500, 0));
            samplePreset("calib 10col 20kps 700ms 50% hold", () => setPreset(10, 20, 700, 0.5));
            samplePreset("calib 4col 20kps 700ms tap", () => setPreset(4, 20, 700, 0));
            samplePreset("calib 16col 20kps 700ms tap", () => setPreset(16, 20, 700, 0));
            samplePreset("calib skin ez2", () => setPreset(10, 20, 700, 0, Ez2Skin.CreateInfo()));
            samplePreset("calib skin ez pro", () => setPreset(10, 20, 700, 0, EzStyleProSkin.CreateInfo()));
            samplePreset("calib skin triangles (restore)", () => setPreset(10, 20, 700, 0, TrianglesSkin.CreateInfo()));

            // ColumnWatcher 与每帧布局写入只存在于 Ez 皮肤组件，消融必须在 Ez2 下测。
            samplePreset("calib ez2 baseline a", () => setPreset(10, 20, 700, 0, Ez2Skin.CreateInfo()));
            samplePreset("calib ez2 no column watcher subscriptions", () =>
            {
                ManiaDenseScrollAblation.DisableColumnWatcherSubscriptions = true;
                setPreset(10, 20, 700, 0, Ez2Skin.CreateInfo());
            });
            samplePreset("calib ez2 baseline b", () =>
            {
                ManiaDenseScrollAblation.DisableColumnWatcherSubscriptions = false;
                setPreset(10, 20, 700, 0, Ez2Skin.CreateInfo());
            });
            samplePreset("calib ez2 no per-frame layout writes", () =>
            {
                ManiaDenseScrollAblation.DisablePerFrameLayoutWrites = true;
                setPreset(10, 20, 700, 0, Ez2Skin.CreateInfo());
            });
            samplePreset("calib ez2 baseline c", () =>
            {
                ManiaDenseScrollAblation.DisablePerFrameLayoutWrites = false;
                setPreset(10, 20, 700, 0, Ez2Skin.CreateInfo());
            });
        }

        private void samplePreset(string label, Action apply)
        {
            AddStep(label, apply);

            // 每个配置取三次，便于在分析时用中位数压掉测试宿主自身的抖动。
            for (int i = 1; i <= 3; i++)
            {
                int sampleIndex = i;
                AddUntilStep($"{label} warm {sampleIndex}", () => field != null && field.Time.Current > 4000 + sampleIndex * 1000);
                AddStep($"{label} log {sampleIndex}", () => logSample($"{label} #{sampleIndex}"));
            }
        }

        [Test]
        public void TestAliveCountMatchesDensityFormula()
        {
            AddStep("settle at 20 KPS / 700 ms / 10 columns", () => setPreset(10, 20, 700, 0));
            AddUntilStep("alive notes settle", () => field != null && field.Time.Current > 4000);
            AddAssert("alive count within density formula", () =>
            {
                var workload = field!.Workload;
                int alive = field.AliveNoteCount;

                // 滚动驻留之外的 note 还会留在容器里等被动 Miss：存活数还应加上这个尾巴。
                double passiveMissTailMs = field.PassiveMissTailMs;
                double resident = workload.AlivePerColumn * workload.ColumnCount;
                double tail = passiveMissTailMs / workload.IntervalMs * workload.ColumnCount;

                // 边界上各列可能多/少一个 interval，按列数放宽。
                double lower = resident - workload.ColumnCount;
                double upper = resident + tail + workload.ColumnCount;

                return alive >= lower && alive <= upper;
            });
            AddStep("log pool summary", () => Logger.Log($"[DENSE-SCROLL-POOL] {ManiaDrawablePoolSnapshot.FormatSummary()}", LoggingTarget.Runtime, LogLevel.Important));
            AddAssert("no pool excess", () => ManiaDrawablePoolSnapshot.TotalExcess() == 0);
        }

        private void setPreset(int columns, double kps, double dwell, double holds)
        {
            columnCount = columns;
            perColumnKps = kps;
            dwellMs = dwell;
            holdRatio = holds;
            rebuildField();
        }

        private void setPreset(int columns, double kps, double dwell, double holds, SkinInfo info)
        {
            columnCount = columns;
            perColumnKps = kps;
            dwellMs = dwell;
            holdRatio = holds;
            skinInfo = info;
            skins.CurrentSkinInfo.Value = skins.GetSkin(info).SkinInfo;
            rebuildField();
        }

        private void setColumns(int columns)
        {
            columnCount = columns;
            rebuildField();
        }

        private void setDensity(double kps, double dwell)
        {
            perColumnKps = kps;
            dwellMs = dwell;
            rebuildField();
        }

        private void setHoldRatio(double holds)
        {
            holdRatio = holds;
            rebuildField();
        }

        private void setSkin(SkinInfo info)
        {
            skinInfo = info;
            skins.CurrentSkinInfo.Value = skins.GetSkin(info).SkinInfo;
            rebuildField();
        }

        private void rebuildField()
        {
            if (field != null)
                Remove(field, true);

            manualClock.CurrentTime = 0;

            field = new ManiaDenseScrollField(
                new ManiaDenseScrollWorkload
                {
                    ColumnCount = columnCount,
                    PerColumnKps = perColumnKps,
                    DwellMs = dwellMs,
                    HoldRatio = holdRatio,
                },
                skins.GetSkin(skinInfo),
                playfieldClock)
            {
                RelativeSizeAxes = Axes.Both,
            };

            Add(field);

            nextLogAt = 0;
            lastAllocated = GC.GetAllocatedBytesForCurrentThread();
            lastGen0 = GC.CollectionCount(0);
            lastGen1 = GC.CollectionCount(1);
            lastGen2 = GC.CollectionCount(2);
            lastFrames = 0;
            frameSamples = 0;
            updateMsSum = 0;
            drawMsSum = 0;

            logSample("spawn");
        }

        private void logSample(string variant)
        {
            if (field == null)
                return;

            long allocatedNow = GC.GetAllocatedBytesForCurrentThread();
            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);

            long frameDelta = Math.Max(1, field.SimulatedFrames - lastFrames);
            long allocDelta = allocatedNow - lastAllocated;
            int gen0Delta = gen0 - lastGen0;
            int gen1Delta = gen1 - lastGen1;
            int gen2Delta = gen2 - lastGen2;

            lastAllocated = allocatedNow;
            lastGen0 = gen0;
            lastGen1 = gen1;
            lastGen2 = gen2;
            lastFrames = field.SimulatedFrames;

            int samples = (int)Math.Max(1, frameSamples);
            double updateMs = updateMsSum / samples;
            double drawMs = drawMsSum / samples;
            frameSamples = 0;
            updateMsSum = 0;
            drawMsSum = 0;
            long fboRedraw = GlobalStatistics.Get<long>("Draw", "FBORedraw").Value;

            int drawableNotes = this.ChildrenOfType<DrawableNote>().Count();
            int drawableHolds = this.ChildrenOfType<DrawableHoldNote>().Count();
            int ticks = this.ChildrenOfType<DrawableHoldNoteTick>().Count();
            int judged = this.ChildrenOfType<DrawableNote>().Count(n => n.AllJudged);

            string line =
                $"variant={variant} {field.Workload.Describe()} " +
                $"frames={field.SimulatedFrames} alive={field.AliveNoteCount} " +
                $"notes={drawableNotes} holds={drawableHolds} ticks={ticks} judged={judged} " +
                $"updMs={updateMs:F2} drawMs={drawMs:F2} FBORedraw={fboRedraw} " +
                $"alloc/frame={allocDelta / (double)frameDelta:F0}B gen0/1/2=+{gen0Delta}/{gen1Delta}/{gen2Delta} " +
                $"heap={GC.GetTotalMemory(false) / 1024}KiB " +
                $"{ManiaDrawablePoolSnapshot.FormatSummary()} " +
                ManiaJudgeHotPathTrace.FormatSummary();

            if (summaryText != null)
                summaryText.Text = line.Replace(' ', '\n');

            Logger.Log($"[DENSE-SCROLL] {line}", LoggingTarget.Runtime, LogLevel.Important);
        }
    }
}
#endif
