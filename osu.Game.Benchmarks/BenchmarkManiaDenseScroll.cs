// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using NUnit.Framework;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Benchmarks
{
    /// <summary>
    /// DENSE-SCROLL：headless 手动步进的密集下落吞吐基准。
    /// <para>
    /// 密度口径与 <see cref="ManiaDenseScrollWorkload"/> 一致：列数 4 / 10 / 16，每列 10 / 15 / 20 KPS，
    /// 驻留 300 / 500 / 700 ms，tap-only 与 50% hold 两组。每次调用跑 60 帧（固定 1/60 s 步长，即 1 s 模拟时间），
    /// <c>[MemoryDiagnoser]</c> 的分配结果除以 60 即为每帧分配。
    /// </para>
    /// <para>
    /// 这里只驱动 update 线程，不测 GPU/FBO；绘制成本请用 <c>TestSceneManiaDenseScrollCost</c>。
    /// </para>
    /// </summary>
    [MemoryDiagnoser]
    public partial class BenchmarkManiaDenseScroll : BenchmarkTest
    {
        private const int frames_per_invoke = 60;

        [Params(4, 10, 16)]
        public int Columns { get; set; }

        [Params(10, 15, 20)]
        public int PerColumnKps { get; set; }

        [Params(300, 500, 700)]
        public double DwellMs { get; set; }

        [Params(0.0, 0.5)]
        public double HoldRatio { get; set; }

        private ManualGameHost host = null!;
        private DenseScrollBenchmarkGame game = null!;
        private ManiaDenseScrollWorkload workload = null!;

        public override void SetUp()
        {
            base.SetUp();

            workload = new ManiaDenseScrollWorkload
            {
                ColumnCount = Columns,
                PerColumnKps = PerColumnKps,
                DwellMs = DwellMs,
                HoldRatio = HoldRatio,
            };

            host = new ManualGameHost(game = new DenseScrollBenchmarkGame(workload));
        }

        [GlobalCleanup]
        [OneTimeTearDown]
        public void Cleanup()
        {
            if (game?.Field != null)
            {
                Console.WriteLine($"[DENSE-SCROLL-BENCH] {workload.Describe()} "
                                  + $"frames={game.Field.SimulatedFrames} alive={game.Field.AliveNoteCount} "
                                  + ManiaDrawablePoolSnapshot.FormatSummary());
            }

            host?.Exit();
            host?.Dispose();
        }

        [Benchmark]
        public void RunFrames()
        {
            for (int i = 0; i < frames_per_invoke; i++)
                host.RunSingleFrame();
        }

        private partial class DenseScrollBenchmarkGame : OsuTestScene.OsuTestSceneTestRunner
        {
            public DenseScrollBenchmarkField Field { get; private set; } = null!;

            private readonly ManiaDenseScrollWorkload workload;

            public DenseScrollBenchmarkGame(ManiaDenseScrollWorkload workload)
            {
                this.workload = workload;
            }

            protected override void LoadAsyncComplete()
            {
                base.LoadAsyncComplete();

                Add(Field = new DenseScrollBenchmarkField(workload)
                {
                    RelativeSizeAxes = Axes.Both,
                });
            }
        }

        /// <summary>
        /// 真实 drawable 的滚动字段：固定步长推进模拟时钟，并按驻留窗口流式补齐 note。
        /// </summary>
        private partial class DenseScrollBenchmarkField : Container
        {
            private const double frame_step_ms = 1000.0 / 60;

            public ManiaDenseScrollWorkload Workload { get; }
            public long SimulatedFrames { get; private set; }

            public int AliveNoteCount
            {
                get
                {
                    int count = 0;

                    foreach (var column in columns)
                        count += column.HitObjectContainer.AliveObjects.Count();

                    return count;
                }
            }

            private readonly ManualClock manualClock = new ManualClock();
            private readonly FramedClock framedClock;
            private readonly List<Column> columns = new List<Column>();
            private readonly double[] nextStart;

            public DenseScrollBenchmarkField(ManiaDenseScrollWorkload workload)
            {
                Workload = workload;
                Workload.Validate();

                framedClock = new FramedClock(manualClock);
                Clock = framedClock;

                nextStart = new double[workload.ColumnCount];
            }

            [BackgroundDependencyLoader]
            private void load(SkinManager skins)
            {
                var skin = skins.GetSkin(TrianglesSkin.CreateInfo());

                var beatmap = new ManiaBeatmap(new StageDefinition(Workload.ColumnCount))
                {
                    BeatmapInfo = new BeatmapInfo
                    {
                        Ruleset = new ManiaRuleset().RulesetInfo,
                        Difficulty = new BeatmapDifficulty(),
                    },
                };

                var transformed = new ManiaRuleset().CreateSkinTransformer(skin, beatmap) ?? skin;

                var columnFlow = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                };

                AddInternal(new SkinProvidingContainer(transformed)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = columnFlow,
                });

                for (int i = 0; i < Workload.ColumnCount; i++)
                {
                    var column = new Column(i, false)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Height = 0.85f,
                    };

                    columns.Add(column);

                    columnFlow.Add(new ScrollingTestContainer(ScrollingDirection.Down)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Y,
                        AutoSizeAxes = Axes.X,
                        TimeRange = Workload.DwellMs,
                        Child = column,
                    });

                    nextStart[i] = Workload.GetNextStartTime(i, 0);
                }
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.Cache(new StageDefinition(Workload.ColumnCount));
                return dependencies;
            }

            protected override void Update()
            {
                base.Update();

                SimulatedFrames++;
                manualClock.CurrentTime += frame_step_ms;
                framedClock.ProcessFrame();

                double now = Time.Current;
                double limit = now + Workload.DwellMs + Workload.IntervalMs;

                for (int column = 0; column < columns.Count; column++)
                {
                    double start = nextStart[column];

                    while (start <= limit)
                    {
                        spawn(column, start);
                        start += Workload.IntervalMs;
                    }

                    nextStart[column] = start;
                }
            }

            private void spawn(int column, double startTimeMs)
            {
                if (Workload.IsHoldAt(column, startTimeMs))
                    columns[column].Add(Workload.CreateHoldNote(column, startTimeMs));
                else
                    columns[column].Add(Workload.CreateNote(column, startTimeMs));
            }
        }

        /// <summary>
        /// framework <c>GameBenchmark</c> 的手动宿主模式：单线程、按需跑一帧。
        /// 不引用 <c>osu.Framework.Benchmarks</c> 项目，避免和 NuGet 的 framework 包产生两份程序集。
        /// </summary>
        private class ManualGameHost : HeadlessGameHost
        {
            private ManualThreadRunner threadRunner = null!;

            public ManualGameHost(osu.Framework.Game game)
                : base("mania-dense-scroll", new HostOptions(), realtime: false)
            {
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Run(game);
                    }
                    catch
                    {
                        // 未观测异常会拖垮 benchmark 进程，这里与框架的 GameBenchmark 保持一致。
                    }
                }, TaskCreationOptions.LongRunning);

                while (threadRunner?.HasRunOnce != true)
                    Thread.Sleep(10);
            }

            public void RunSingleFrame() => threadRunner.RunSingleFrame();

            protected override void Dispose(bool isDisposing)
            {
                // 放行 RunMainLoop 的最后一次等待，否则 Exit 会一直等主循环返回。
                threadRunner.RunOnce.Set();
                base.Dispose(isDisposing);
            }

            protected override ThreadRunner CreateThreadRunner(InputThread mainThread) => threadRunner = new ManualThreadRunner(mainThread);
        }

        private class ManualThreadRunner : ThreadRunner
        {
            public bool HasRunOnce { get; private set; }

            public readonly ManualResetEventSlim RunOnce = new ManualResetEventSlim();

            public ManualThreadRunner(InputThread mainThread)
                : base(mainThread)
            {
                RunOnce.Set();
            }

            public void RunSingleFrame()
            {
                ExecutionMode = ExecutionMode.SingleThread;
                base.RunMainLoop();
            }

            public override void RunMainLoop()
            {
#pragma warning disable RS0030
                RunOnce.Wait();
#pragma warning restore RS0030

                RunSingleFrame();
                RunOnce.Reset();
                HasRunOnce = true;
            }
        }
    }
}
