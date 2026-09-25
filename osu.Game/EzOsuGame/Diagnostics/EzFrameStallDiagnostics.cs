// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using osu.Framework.Audio.Wasapi;
using osu.Framework.Platform;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Diagnostics
{
    /// <summary>
    /// 帧耗时（update 线程 stall）探针，挂在 <c>FrameStabilityContainer.UpdateSubTree</c> 末尾的帧边界上。
    /// <para>
    /// 与 <see cref="EzPressLatencyDiagnostics"/> 的关系：那个探针只采样「有按键的帧」，
    /// 于是在没有按键落下时帧有多慢是看不见的——而按键延迟的上界正是「它落在的那一帧的时长」。
    /// 本探针补上这条基线：每帧都记一次耗时，只在超过阈值时留明细。
    /// </para>
    /// <para>
    /// 直方图分两张：**含按键的帧**与**不含按键的帧**。这是本探针最要紧的一个判据——
    /// 实测发现含按键的帧占了近半的 stall，而按键帧本身只占全部帧的 0.5%，量级上无法用
    /// 「按键只多花 0.1ms」解释。只有把两组各自的完整分布摊开，才能读出「一次按键究竟把帧拉长了多少」，
    /// 而不是靠「≥阈值」这个截断后的条件分布去猜。
    /// </para>
    /// <para>
    /// 明细行里带该帧的 <c>GcPauseDeltaMs</c>（跨该帧的 <c>GC.GetTotalPauseDuration</c> 增量）与
    /// <c>ThreadAllocDeltaBytes</c>（update 线程在该帧的分配量）。GC 是否为主因靠前者判断：
    /// 若一次 20ms 的 stall 里 <c>GcPauseDeltaMs</c> 也接近 20ms，GC 就是原因；若接近 0，GC 即可排除。
    /// 后者则回答「高 gen0 频率（实测 ~24/s）是不是 update 线程自己在分配」。
    /// </para>
    /// <para>
    /// 注意 <c>GcPauseDeltaMs</c> 是**全线程**暂停时长之和（后台 GC 并发时它可能超过帧长），
    /// 所以它是「update 线程被挂起」的上界：数值远小于帧长时能可靠排除 GC，数值大时只能算强嫌疑。
    /// </para>
    /// <para>
    /// 同一个探针还顺手采 present（draw 线程）侧：本 fork 里 <c>Game</c> 是 <c>Container</c> 而不是 <c>GameHost</c>，
    /// <c>osu.Game</c> 没法 override <c>GameHost.DrawFrame</c>，所以 draw 线程上放不了打点，
    /// 只能从 update 线程读 <c>DrawThread.Clock</c>——它的 <c>ElapsedFrameTime</c> 就是「相邻两次 present 的间隔」。
    /// update 侧的帧一直很稳，但 draw 线程单独卡一下这个探针是看不见的，所以那一行要看的是尾部。
    /// </para>
    /// 热路径不做 IO、不产生字符串；落盘在局末。
    /// </summary>
    public static class EzFrameStallDiagnostics
    {
        /// <summary>是否采集。启动时由 <see cref="EzDiagnosticSwitches.Apply"/> 写一次。</summary>
        public static bool Enabled { get; private set; }

        /// <summary>唯一的生产写入口；测试可直接调用。</summary>
        public static void SetEnabled(bool enabled) => Enabled = enabled;

        /// <summary>超过该时长的帧才留明细；直方图则覆盖所有帧。可用 <c>EZ_FRAME_PROBE_MS</c> 覆盖。</summary>
        public static double ThresholdMs { get; set; } = 0.5;

        /// <summary>
        /// 是否每帧读取 GC 计数与分配量。可用 <c>EZ_FRAME_PROBE_LIGHT=1</c> 关掉。
        /// <para>
        /// 那几个 GC API（尤其 <c>GetTotalPauseDuration</c> / <c>GetTotalAllocatedBytes</c>）不是免费的，
        /// 而 <c>RecordFrame</c> 跑在输入派发**之前**，所以它的开销会落进同帧按键的 <c>PreColumnMs</c>。
        /// 直方图（按键膨胀的判据）不需要这些读数，于是给出这个开关：
        /// 关掉后 <c>PreColumnMs</c> 若回到无探针时的水平，就说明开销确实在这里。
        /// </para>
        /// </summary>
        public static bool Deep { get; set; } = true;

        /// <summary>
        /// 帧采样是否开启。热路径（<c>FrameStabilityContainer.UpdateSubTree</c>）只在它上面分一个可预测的分支，
        /// 关闭时一次 <c>Stopwatch</c> / GC 查询都不发。
        /// </summary>
        public static bool Sampling => Enabled;

        /// <summary>是否连分配量一起采样。关闭时跳过每帧两次 <c>GC.GetAllocatedBytesForCurrentThread</c>。</summary>
        public static bool SamplingAlloc => Enabled && Deep;

        // 「FSC 子树 vs 其余」归因：FrameStabilityContainer 在每轮 UpdateSubTree 末尾回报一次，
        // 本探针在随后同帧的 RecordFrame 里读。
        private static double subtreeMs;
        private static double clockMs;
        private static long loopAllocBytes;
        private static bool frameValid;
        private static int lastUpdateIterations;

        /// <summary>[Ez] 上一轮 FSC 子树（<c>base.UpdateSubTree</c>）耗时；供帧探针与按键探针读，不参与游戏逻辑。</summary>
        public static double SubtreeMs => subtreeMs;

        /// <summary>[Ez] 上一轮 <c>FrameStabilityContainer.updateClock</c> 累计耗时（catch-up 时为多次之和）。</summary>
        public static double ClockMs => clockMs;

        /// <summary>[Ez] 上一轮 FSC 全程（时钟 + 子树 + masking）的分配字节数；仅 Deep 模式有值。</summary>
        public static long LoopAllocBytes => loopAllocBytes;

        /// <summary>[Ez] 上一轮是否真的跑了子树。暂停时 <c>updateClock</c> 提前返回、子树不跑，
        /// 这种帧的耗时不能算进归因均值，否则会把子树占比拉低。</summary>
        public static bool FrameValid => frameValid;

        /// <summary>
        /// [Ez] 上一轮 FSC <c>UpdateSubTree</c> 的子树趟数；大于 1 表示 catch-up。
        /// 帧探针写明细、按键探针记按键落在第几趟，都只读不改。不参与游戏逻辑。
        /// </summary>
        public static int LastUpdateIterations => lastUpdateIterations;

        /// <summary>
        /// FSC 每轮 <c>UpdateSubTree</c> 末尾的归因回报（耗时/分配/趟数）。
        /// 参数里的耗时在帧采样关闭时为 0；趟数与 <paramref name="ranSubtree"/> 任何时候都有效，因为按键探针独立于帧探针。
        /// </summary>
        internal static void ReportLoop(double subtree, double clock, long allocBytes, bool ranSubtree, int iterations)
        {
            subtreeMs = subtree;
            clockMs = clock;
            loopAllocBytes = allocBytes;
            frameValid = ranSubtree;
            lastUpdateIterations = iterations;
        }

        /// <summary>
        /// 一轮 <c>FrameStabilityContainer.UpdateSubTree</c> 的归因脚手架：把「时钟推进」「drawable 子树」
        /// 分段计时、本趟分配量与帧边界处的两个时钟采样收在这里，使上游文件只留几个 mark 调用。
        /// <para>
        /// 关闭时不发 <c>Stopwatch</c> / GC 查询、不产生字符串；每个 mark 至多一次静态 bool 判断。
        /// 用法：<see cref="Begin"/> → 循环内 <see cref="BeforeClock"/> / <see cref="AfterClock"/> /
        /// <see cref="BeforeSubtree"/> / <see cref="AfterSubtree"/> → 循环外 <see cref="Complete"/>。
        /// </para>
        /// </summary>
        internal struct LoopScope
        {
            private readonly bool sampling;
            private readonly bool samplingAlloc;
            private readonly bool report;

            private readonly long allocBefore;

            private long clockStart;
            private long subtreeStart;
            private double clockTicks;
            private double subtreeTicks;
            private bool ranSubtree;

            private LoopScope(bool sampling, bool samplingAlloc, bool report)
            {
                this.sampling = sampling;
                this.samplingAlloc = samplingAlloc;
                this.report = report;

                allocBefore = samplingAlloc ? GC.GetAllocatedBytesForCurrentThread() : 0;
            }

            /// <summary>开始一轮归因；在 <c>UpdateSubTree</c> 最前面调用一次。</summary>
            public static LoopScope Begin() => new LoopScope(Sampling, SamplingAlloc, EzDiagnosticSwitches.FrameLoopAttribution);

            public void BeforeClock()
            {
                if (sampling)
                    clockStart = Stopwatch.GetTimestamp();
            }

            public void AfterClock()
            {
                if (sampling)
                    clockTicks += Stopwatch.GetTimestamp() - clockStart;
            }

            public void BeforeSubtree()
            {
                if (sampling)
                    subtreeStart = Stopwatch.GetTimestamp();
            }

            public void AfterSubtree()
            {
                ranSubtree = true;

                if (sampling)
                    subtreeTicks += Stopwatch.GetTimestamp() - subtreeStart;
            }

            /// <summary>
            /// 本趟结束：回报逐轮归因与趟数，并在帧边界采两个时钟。
            /// <paramref name="clock"/> 传 <c>FrameStabilityContainer.ParentGameplayClock</c>：
            /// 取 <c>BassSourceCurrentTime</c>（音频源时钟，实测 10ms 阶梯）与 <c>CurrentTime</c>
            /// （插值时钟，note 位置实际读的那个）。后者决定「下落顺不顺滑」，而判定/按键探针只有 ~10Hz，
            /// 看不见 100Hz 的阶梯 —— 这里是唯一能按帧看的地方。
            /// <para>
            /// 归因闸门是「帧探针或按键探针任一开启」（趟数按键探针也要）；两者都关时只剩一次静态 bool 判断。
            /// 帧边界必须落在每一趟的同一位置，否则两次调用之间的差不是一个整帧。
            /// </para>
            /// </summary>
            public void Complete(int iterations, GameplayClockContainer? clock)
            {
                if (report)
                {
                    double tickToMs = 1000.0 / Stopwatch.Frequency;

                    ReportLoop(
                        sampling ? subtreeTicks * tickToMs : 0,
                        sampling ? clockTicks * tickToMs : 0,
                        samplingAlloc ? GC.GetAllocatedBytesForCurrentThread() - allocBefore : 0,
                        ranSubtree,
                        iterations);
                }

                if (!sampling)
                    return;

                double audioSrcMs = double.NaN;
                double interpMs = double.NaN;

                if (clock != null)
                {
                    audioSrcMs = clock.BassSourceCurrentTime;
                    interpMs = clock.CurrentTime;
                }

                RecordFrame(audioSrcMs, interpMs);
            }
        }

        private const int bucket_count = 1024;

        /// <summary>
        /// 明细容量。<c>EZ_FRAME_PROBE_MS=0</c> 抓全集时需要容纳一局的所有帧
        /// （2000 fps × 100 s ≈ 20 万帧；单条约 80 B ⇒ 约 21 MB），故留到 26 万。
        /// </summary>
        private const int detail_capacity = 262144;

        // 桶宽 0.1ms，桶数 1024 → 覆盖到 102.3ms；超出者并入末桶。
        // 用 int 计数：一个 play 的帧数在十万量级，不会溢出。
        private static readonly Histogram withoutPress = new Histogram();
        private static readonly Histogram withPress = new Histogram();

        private static FrameSample[] details = new FrameSample[detail_capacity];
        private static int detailCursor;
        private static int detailCount;
        private static long detailOverwritten;

        // 帧边界状态
        private static long lastFrameTimestamp;
        private static long frameIndex;
        private static long lastGcPauseTicks = -1;
        private static long lastThreadAllocated;
        private static long lastProcessAllocated;
        private static int lastGen0;
        private static int lastGen1;

        private static long pressFrameCount;
        private static double sincePrevFrameTotalMs;
        private static double pressColumnTotalMs;

        // 「FSC 子树 vs 其余」归因用的累计量（仅 Deep 模式）。
        private static double subtreeMsTotal;
        private static double clockMsTotal;
        private static double elapsedProbeTotalMs;
        private static long loopAllocTotal;
        private static long probeFrames;

        private static int pressesInFrame;
        private static double pressColumnMsInFrame;
        private static long currentFrameStartTimestamp;
        private static double sincePrevFrameMinMs = double.MaxValue;

        // present（draw 线程）侧。update 线程内能测的都测干净了，剩下的只有「屏幕上送出去的那份内容」，
        // 而它只能在 update 侧**读 draw 线程的时钟**来观测：这个 fork 里 Game 是 Container 而不是 GameHost，
        // osu.Game 无法 override GameHost.DrawFrame，所以 draw 线程上放不了打点。
        //
        // 好在 <c>DrawThread.Clock</c> 自己就是 draw 线程的帧时钟，三样东西都直接读得到：
        //   ElapsedFrameTime —— **上一次 present 的整周期**，也就是「相邻两次 present 的间隔」，正是要量的东西；
        //   TimeSlept        —— 其中被帧率限制器 sleep 掉的部分；
        //   CurrentTime      —— draw 线程当前帧的边界时刻（与 update 时钟同源同频，只需标定一个原点偏移）。
        //
        // 两个判据：
        //   1) drawPeriod —— present 间隔的分布。update 侧看到的帧一直很稳，但**draw 线程自己卡一下
        //      是 update 探针看不见的**：那才是屏幕上直接的一次跳帧。
        //   2) presentAge —— present 那一刻，屏幕上那份内容已经多旧（相对它在 update 侧被算出来的时刻）。
        //      均值会被输入延迟吸收，**抖动才是眼睛看到的顿挫**。
        //
        // 采样口径（踩过两次坑，改之前先读）：
        //   * 探针跑在 update 边界上，而 update 比 draw 快，所以**必须按 draw 帧去重**。逐次采样拿到的不是
        //     「相邻两次 present 的间隔」：某帧的值只在这一帧结束到下一帧结束之间可见，被采到的次数 ∝ **下一帧**
        //     的长度，于是长帧被少采、短帧被多采，均值与分位一起被拉低（实测 0.622ms vs draw 自报 0.826ms）。
        //   * CurrentTime 是阶梯式刷新的，wall − CurrentTime 带一个幅度 = 一个 draw 周期的锯齿，一次性标定会
        //     锁在随机相位上（表现为 presentAge 出现不可能的负值）⇒ 取**运行最小值**。
        //   * coverage = ΣdrawPeriod / 采样首末壁钟跨度：一次合格的自检，≈1 才说明每个 draw 帧恰好采到一次。
        //   * presentAge 的分辨率是**一个 draw 周期**（读不到被绘制的 buffer 帧号，只能拿「上一次 update 帧边界」
        //     当零点，而 draw 手上的 buffer 可能更早），所以它只能判约 1ms 以上的滞后，判不了亚毫秒配对抖动。
        private static GameHost? gameHost;
        private static readonly Histogram drawPeriod = new Histogram(0.02);
        private static readonly Histogram presentAge = new Histogram(0.02);
        private static double drawOriginOffsetMs = double.NaN;
        private static double lastDrawClockMs = double.NaN;
        private static double drawPeriodSumMs;
        private static double presentFirstWallMs = double.NaN;
        private static double presentLastWallMs;
        private static long presentFrames;
        private static long presentDuplicates;
        private static long presentSkipped;
        private static long presentFrozenSkipped;
        private static double drawSleptTotalMs;

        /// <summary>落盘前抓一次的 NAudio 拉取统计（<see cref="WasapiReadStats"/> 在该局开测时启用、清空）。</summary>
        private static WasapiReadStats.Snapshot? wasapiReadSnapshot;

        // 「音频时钟量化 / 插值纹波」：note 位置取自插值时钟，而插值时钟追的是音频源时钟。
        // 实测音频源是精确 10ms 阶梯（见 docs/EZ-PERFORMANCE.md §2.4.11），所以这里逐帧记录两个时钟。
        //
        // 关键：判「顺不顺滑」要看的是**位置**精度，不是逐帧速率。音频源每 10ms 跳一格，插值器要抹平它
        // 就必须让逐帧速率上下摆（实测 std 0.28，p25 0.82 / p75 1.17）—— 那是抹平**成功**的表现，
        // 拿它当「抖动」会得出完全相反的结论。真正的判据是 interp 相对「去量化后的连续音频」的偏差：
        // 把报告值 + 距上次跳变的时长（缓冲内已播进度）还原成连续位置，再看 interp 离它多远。
        private static double prevAudioSrcMs = double.NaN;
        private static double prevInterpMs = double.NaN;
        private static readonly Histogram audioStep = new Histogram();
        private static readonly Histogram interpErr = new Histogram(0.01);
        private static double lastAudioStepWallMs = double.NaN;

        /// <summary>源位置「停走超过一个正常步进周期再补上」的次数与停走总时长。NAudio 漏拉一次 buffer 就是这个形状。</summary>
        private static long pullMissCount;
        private static double pullMissHoldMs;

        /// <summary>判定漏拉的停走窗口；正常步进周期是 10ms，一次漏拉停 ~20ms，再长的停走是暂停/seek，不算。</summary>
        private const double PULL_MISS_HOLD_MS = 15;
        private const double PULL_MISS_MAX_MS = 60;

        private static long rateSkipped;
        private static long clockStoppedFrames;
        private static double clockStoppedMs;

        /// <summary>本帧的 gameplay 时钟是否停走（歌曲结束 / 暂停）。</summary>
        /// <remarks>
        /// 歌曲结束后的退场 / 结算段是**另一个工况**：子树已拆（`subtree=0`）、update 掉到 ~120 Hz。
        /// 实测一次 39 s 的采集里 327 个 >5 ms 帧有 316 个落在这一段。present 采样要排除它，
        /// 否则 update 采样频率比 draw 还低，会整段整段漏掉 draw 帧，把 `coverage` 拉到 0.87 以下
        /// （该数只能由 <see cref="FormatSummary"/> 的 present 行自检看出来）。帧耗时直方图不排除
        /// （跨局可比性优先），但读 `over5` 时要记得减去这 300 帧量级的退场段。
        /// </remarks>
        private static bool clockFrozen;
        private static long errCount;
        private static double errSum;
        private static double errSqSum;
        private static double errMaxAbs;
        private static long errOver05;
        private static long errOver1;
        private static long errOver2;
        private static double errBaseline = double.NaN;
        private static long driftCount;
        /// <summary>时钟误差基线的 EMA 时间常数：估掉「音频输出延迟」那个常数，只留抖动。</summary>
        private const double ERR_BASELINE_MS = 500;
        private static double driftMin = double.MaxValue;
        private static double driftMax = double.MinValue;
        private static double driftSum;
        private static double driftSqSum;

        /// <summary>探针读一次「非位置输入队列」的长度，用于给出键绑定派发的 O(n)。只在整局开始时读一次。</summary>
        public static void ReportInputQueueCount(int count) => inputQueueCount = count;

        private static long inputQueueCount = -1;

        // 会话累计
        private static double firstFrameWallMs = double.NaN;
        private static double lastFrameWallMs;
        private static long stallFrames;
        private static long stallFramesWithPress;
        private static long threadAllocatedTotal;
        private static long processAllocatedTotal;
        private static double gcPauseTotalMs;

        public readonly record struct FrameSample(
            double WallMs,
            long FrameIndex,
            double ElapsedMs,
            double GcPauseDeltaMs,
            long ThreadAllocDeltaBytes,
            int PressesInFrame,
            double PressColumnMs,
            double SincePrevFrameMs,
            int FscIterations,
            int Gen0Delta,
            int Gen1Delta,
            double SubtreeMs,
            long LoopAllocBytes,
            double AudioSrcMs,
            double InterpMs);

        /// <summary>
        /// [Ez] 挂 host：present 探针靠它读 draw 线程时钟与显示器刷新率。由 <c>OsuGameBase.SetHost</c> 调用。
        /// </summary>
        public static void AttachHost(GameHost host)
        {
            gameHost = host;
            // 换了 host 就是换了一组线程时钟，原点标定作废。
            drawOriginOffsetMs = double.NaN;
        }

        /// <summary>
        /// 采一次 present（draw 线程）侧的读数。只从 update 线程调用，读的是 draw 线程时钟的属性；
        /// 那两个 double 由 draw 线程写、这里读，最坏读到上/下一次的值，但不会读到撕裂值——
        /// 对这个探针来说那正好是「±1 帧」的噪声，落在它自己的分辨率以内。
        /// </summary>
        /// <param name="nowTicks">本帧边界戳。</param>
        /// <param name="prevTicks">上一帧边界戳，也就是「正被绘制的那份内容」在 update 侧算出来的时刻。</param>
        private static void samplePresent(long nowTicks, long prevTicks)
        {
            var host = gameHost;

            if (host == null)
                return;

            var drawClock = host.DrawThread.Clock;
            double periodMs = drawClock.ElapsedFrameTime;

            // 失焦 / 最小化时 draw 线程降频甚至根本不画，那些帧不是 present，算进去只会把分布摊开。
            if (!host.IsActive.Value || periodMs <= 0 || periodMs > 50)
            {
                presentSkipped++;
                return;
            }

            double drawNowMs = drawClock.CurrentTime;

            // 去重：同一个 draw 帧被多个 update 帧读到，只记一次（理由见字段区注释）。
            if (drawNowMs == lastDrawClockMs)
            {
                presentDuplicates++;
                return;
            }

            lastDrawClockMs = drawNowMs;

            double wallMs = nowTicks * 1000.0 / Stopwatch.Frequency;

            // 两个时钟各自以自己 Start() 的时刻为零点（StopwatchClock 用的是 ElapsedTicks），要标定原点差；
            // 而 CurrentTime 只在 draw 帧边界刷新，读数是阶梯的，所以取运行最小值（刚刷新完的那个瞬间）才是真值。
            double originGapMs = wallMs - drawNowMs;

            if (double.IsNaN(drawOriginOffsetMs) || originGapMs < drawOriginOffsetMs)
                drawOriginOffsetMs = originGapMs;

            presentFrames++;
            drawPeriod.Add(periodMs);
            drawPeriodSumMs += periodMs;
            drawSleptTotalMs += drawClock.TimeSlept;

            if (double.IsNaN(presentFirstWallMs))
                presentFirstWallMs = wallMs;

            presentLastWallMs = wallMs;

            // 正被绘制的那份内容是上一次 update 帧算出来的（本帧还没发布），它会在本帧的 Swap 处上屏。
            // 「本帧什么时候上屏」估计为「本帧边界 + 本帧自身耗时」，而自身耗时只能拿上一帧的：
            //   ElapsedFrameTime = 上一帧的整周期 = 上一帧耗时 + 上一帧被限制器 sleep 的部分，
            // 所以上一帧耗时 = period − TimeSlept（两个读数恰好都来自上一帧）。用 period 会把它高估一个 sleep。
            presentAge.Add(drawNowMs + drawOriginOffsetMs + periodMs - drawClock.TimeSlept - prevTicks * 1000.0 / Stopwatch.Frequency);
        }

        /// <summary>
        /// 本帧内已处理（进入列入口）的按键，由 <c>Column.OnPressed</c> 调用。
        /// </summary>
        /// <param name="pressEnterTs">该次按键进入本列的 wall 戳（<c>Stopwatch</c> 计时单位）。</param>
        /// <param name="columnMs">该次按键在本列花掉的时长。</param>
        /// <remarks>
        /// <paramref name="pressEnterTs"/> 只用来算 <c>SincePrevFrameMs</c>（距上一帧边界多远）。
        /// **它不是「本帧按键前的工作量」**：零点取的是上一帧的 FSC 调用点，所以这个间隔里是
        /// 上一帧剩余时间、draw、present 与帧间等待。真正属于按键自身工作的只有 <paramref name="columnMs"/>。
        /// </remarks>
        public static void NotifyPress(long pressEnterTs, double columnMs)
        {
            if (!Enabled)
                return;

            pressesInFrame++;
            pressColumnMsInFrame += columnMs;

            if (currentFrameStartTimestamp != 0)
            {
                double sincePrevFrameMs = (pressEnterTs - currentFrameStartTimestamp) * 1000.0 / Stopwatch.Frequency;

                if (sincePrevFrameMs < sincePrevFrameMinMs)
                    sincePrevFrameMinMs = sincePrevFrameMs;
            }
        }

        /// <summary>
        /// 记一次帧边界。调用点必须是每帧同一位置，否则相邻两次之差不是整帧时长。
        /// 首帧只用于播种各计数器基线，不计入统计。
        /// </summary>
        /// <param name="audioSrcMs">该帧音频源时钟（<c>GameplayClockContainer.BassSourceCurrentTime</c>）。</param>
        /// <param name="interpMs">该帧插值时钟（note 位置实际取值）。取不到时钟时两者都传 <see cref="double.NaN"/>。</param>
        public static void RecordFrame(double audioSrcMs, double interpMs)
        {
            if (!Enabled)
                return;

            long now = Stopwatch.GetTimestamp();
            double wallMs = EzProbeOutput.WallClockMs;

            long prev = lastFrameTimestamp;
            lastFrameTimestamp = now;
            // 必须记 now：NotifyPress 在本帧的 RecordFrame 之前执行，此刻能读到的最新边界就是
            // 上一次 RecordFrame 写下的值。写 prev 会让它滞后一帧，SincePrevFrameMs 虚增一整个帧长。
            currentFrameStartTimestamp = now;
            frameIndex++;

            // light 模式只留直方图，跳掉每帧的 GC/分配读数（那几个 API 会落在同帧按键的等待里）。
            long gcPauseTicks = 0;
            long threadAllocated = 0;
            long processAllocated = 0;
            int gen0 = 0, gen1 = 0;

            if (Deep)
            {
                gcPauseTicks = GC.GetTotalPauseDuration().Ticks;
                threadAllocated = GC.GetAllocatedBytesForCurrentThread();
                processAllocated = GC.GetTotalAllocatedBytes(false);
                gen0 = GC.CollectionCount(0);
                gen1 = GC.CollectionCount(1);
            }

            if (prev == 0)
            {
                seed(gcPauseTicks, threadAllocated, processAllocated, gen0, gen1);
                pressesInFrame = 0;
                pressColumnMsInFrame = 0;
                sincePrevFrameMinMs = double.MaxValue;
                firstFrameWallMs = wallMs;
                prevAudioSrcMs = audioSrcMs;
                prevInterpMs = interpMs;
                return;
            }

            double elapsedMs = (now - prev) * 1000.0 / Stopwatch.Frequency;
            lastFrameWallMs = wallMs;
            accumulateClocks(audioSrcMs, interpMs, elapsedMs);

            // 时钟停走（歌曲结束后的退场段）不采 present：那段 update 会掉到 ~120Hz 而 draw 还在千帧以上，
            // 逐 update 帧采样会整段漏掉 draw 帧，coverage 自检立刻掉下来（实测 0.869）。
            if (clockFrozen)
                presentFrozenSkipped++;
            else
                samplePresent(now, prev);

            if (pressesInFrame > 0)
            {
                withPress.Add(elapsedMs);
                pressFrameCount++;

                if (sincePrevFrameMinMs != double.MaxValue)
                    sincePrevFrameTotalMs += sincePrevFrameMinMs;

                pressColumnTotalMs += pressColumnMsInFrame;
            }
            else
                withoutPress.Add(elapsedMs);

            double gcPauseDeltaMs = Deep ? (gcPauseTicks - lastGcPauseTicks) / (double)TimeSpan.TicksPerMillisecond : 0;
            long threadAllocDelta = Deep ? threadAllocated - lastThreadAllocated : 0;

            if (Deep)
            {
                gcPauseTotalMs += gcPauseDeltaMs;
                threadAllocatedTotal += threadAllocDelta;
                processAllocatedTotal += processAllocated - lastProcessAllocated;

                // 暂停帧没有子树，算进来会把子树占比拉低。
                if (frameValid)
                {
                    subtreeMsTotal += subtreeMs;
                    clockMsTotal += clockMs;
                    loopAllocTotal += loopAllocBytes;
                    // 帧长必须用同一批帧，否则子树占比的分子分母不同源。
                    elapsedProbeTotalMs += elapsedMs;
                    probeFrames++;
                }
            }

            if (elapsedMs >= ThresholdMs)
            {
                stallFrames++;

                if (pressesInFrame > 0)
                    stallFramesWithPress++;

                details[detailCursor] = new FrameSample(
                    wallMs,
                    frameIndex,
                    elapsedMs,
                    gcPauseDeltaMs,
                    threadAllocDelta,
                    pressesInFrame,
                    pressColumnMsInFrame,
                    pressesInFrame > 0 && sincePrevFrameMinMs != double.MaxValue ? sincePrevFrameMinMs : double.NaN,
                    lastUpdateIterations,
                    gen0 - lastGen0,
                    gen1 - lastGen1,
                    subtreeMs,
                    loopAllocBytes,
                    audioSrcMs,
                    interpMs);

                detailCursor++;

                if (detailCursor >= detail_capacity)
                {
                    detailCursor = 0;
                    Interlocked.Increment(ref detailOverwritten);
                }

                if (detailCount < detail_capacity)
                    detailCount++;
            }

            seed(gcPauseTicks, threadAllocated, processAllocated, gen0, gen1);
            pressesInFrame = 0;
            pressColumnMsInFrame = 0;
            sincePrevFrameMinMs = double.MaxValue;
        }

        /// <summary>
        /// 累计音频时钟量化相关的读数。拿不到时钟（非 GameplayClockContainer）时传 NaN，跳过。
        /// </summary>
        private static void accumulateClocks(double audioSrcMs, double interpMs, double elapsedMs)
        {
            if (!double.IsFinite(audioSrcMs) || !double.IsFinite(interpMs))
            {
                prevAudioSrcMs = prevInterpMs = double.NaN;
                clockFrozen = false;
                return;
            }

            if (double.IsFinite(prevAudioSrcMs) && double.IsFinite(prevInterpMs))
            {
                double step = audioSrcMs - prevAudioSrcMs;

                // 反向步进只可能来自 seek，不计入步长分布（Histogram 会把负值塞进第 0 桶，污染判读）。
                if (step >= 0)
                    audioStep.Add(step);

                // 音频报告值跳变 ⇒ 一个新的缓冲被播完，缓冲内已播进度清零。
                if (step != 0)
                {
                    if (double.IsFinite(lastAudioStepWallMs))
                    {
                        double holdMs = EzProbeOutput.WallClockMs - lastAudioStepWallMs;

                        if (holdMs > PULL_MISS_HOLD_MS && holdMs < PULL_MISS_MAX_MS)
                        {
                            pullMissCount++;
                            pullMissHoldMs += holdMs;
                        }
                    }

                    lastAudioStepWallMs = EzProbeOutput.WallClockMs;
                }

                // 插值完全没走 = 时钟停走（歌曲结束 / 暂停），与「走得慢」是两回事，单独计数。
                if (interpMs == prevInterpMs)
                {
                    clockStoppedFrames++;
                    clockStoppedMs += elapsedMs;
                    clockFrozen = true;
                }
                else
                {
                    clockFrozen = false;
                }

                if (interpMs != prevInterpMs && double.IsFinite(lastAudioStepWallMs))
                {
                    double age = EzProbeOutput.WallClockMs - lastAudioStepWallMs;

                    // age 只在一个缓冲周期内可信；超出说明中途有跳变没被采到（帧太长），丢弃以免污染。
                    if (age >= 0 && age <= 50)
                    {
                        double err = interpMs - (audioSrcMs + age);
                        double abs = Math.Abs(err);

                        errCount++;
                        errSum += err;
                        errSqSum += err * err;

                        // err 含一个常数：音频报告位置领先「听到的声音」约一个缓冲，量级 ~10ms。
                        // 那是输出延迟，会被音频偏移吸收，不是抖动；用慢 EMA 在线估掉它，阈值才判得动。
                        // std 本来就是平移不变的，所以直接取原始 err 的 std。
                        if (double.IsNaN(errBaseline))
                            errBaseline = err;
                        else
                            errBaseline += (err - errBaseline) * Math.Min(1.0, elapsedMs / ERR_BASELINE_MS);

                        double dev = Math.Abs(err - errBaseline);

                        if (dev > errMaxAbs)
                            errMaxAbs = dev;

                        interpErr.Add(dev);

                        if (dev > 0.5) errOver05++;
                        if (dev > 1) errOver1++;
                        if (dev > 2) errOver2++;
                    }
                }

                if (elapsedMs > 5)
                    rateSkipped++;

                double drift = interpMs - audioSrcMs;
                driftCount++;
                driftSum += drift;
                driftSqSum += drift * drift;

                if (drift < driftMin)
                    driftMin = drift;

                if (drift > driftMax)
                    driftMax = drift;
            }

            prevAudioSrcMs = audioSrcMs;
            prevInterpMs = interpMs;
        }

        private static void seed(long gcPauseTicks, long threadAllocated, long processAllocated, int gen0, int gen1)
        {
            lastGcPauseTicks = gcPauseTicks;
            lastThreadAllocated = threadAllocated;
            lastProcessAllocated = processAllocated;
            lastGen0 = gen0;
            lastGen1 = gen1;
        }

        public static void Clear()
        {
            withoutPress.Reset();
            withPress.Reset();

            Interlocked.Exchange(ref details, new FrameSample[detail_capacity]);
            detailCursor = 0;
            detailCount = 0;
            Interlocked.Exchange(ref detailOverwritten, 0);

            lastFrameTimestamp = 0;
            frameIndex = 0;
            drawPeriod.Reset();
            presentAge.Reset();
            presentFrames = 0;
            presentSkipped = 0;
            presentDuplicates = 0;
            drawSleptTotalMs = 0;
            drawOriginOffsetMs = double.NaN;
            lastDrawClockMs = double.NaN;
            drawPeriodSumMs = 0;
            presentFirstWallMs = double.NaN;
            presentLastWallMs = 0;
            lastGcPauseTicks = -1;
            lastThreadAllocated = 0;
            lastProcessAllocated = 0;
            lastGen0 = lastGen1 = 0;
            pressesInFrame = 0;
            pressColumnMsInFrame = 0;
            sincePrevFrameMinMs = double.MaxValue;
            pressFrameCount = 0;
            sincePrevFrameTotalMs = 0;
            pressColumnTotalMs = 0;
            subtreeMsTotal = 0;
            clockMsTotal = 0;
            elapsedProbeTotalMs = 0;
            loopAllocTotal = 0;
            probeFrames = 0;
            inputQueueCount = -1;

            prevAudioSrcMs = double.NaN;
            prevInterpMs = double.NaN;
            audioStep.Reset();
            interpErr.Reset();
            wasapiReadSnapshot = null;
            clockFrozen = false;
            lastAudioStepWallMs = double.NaN;
            pullMissCount = 0;
            pullMissHoldMs = 0;
            rateSkipped = 0;
            clockStoppedFrames = 0;
            clockStoppedMs = 0;
            errCount = 0;
            errSum = 0;
            errSqSum = 0;
            errMaxAbs = 0;
            errOver05 = 0;
            errOver1 = 0;
            errOver2 = 0;
            errBaseline = double.NaN;
            driftCount = 0;
            driftMin = double.MaxValue;
            driftMax = double.MinValue;
            driftSum = 0;
            driftSqSum = 0;

            firstFrameWallMs = double.NaN;
            lastFrameWallMs = 0;
            stallFrames = 0;
            stallFramesWithPress = 0;
            threadAllocatedTotal = 0;
            processAllocatedTotal = 0;
            gcPauseTotalMs = 0;
        }

        /// <summary>把明细与直方图摘要落盘，返回 CSV 路径。IO 在后台线程执行；调用方负责随后 <see cref="Clear"/>。</summary>
        public static string Flush()
        {
            int sampleCount = detailCount;
            int start = sampleCount < detail_capacity ? 0 : detailCursor;
            var snapshot = details;

            wasapiReadSnapshot = WasapiReadStats.GetSnapshot();

            var sb = new StringBuilder();
            sb.AppendLine("WallMs,FrameIndex,ElapsedMs,GcPauseDeltaMs,ThreadAllocDeltaBytes,PressesInFrame,PressColumnMs,SincePrevFrameMs,FscIter,Gen0Delta,Gen1Delta,SubtreeMs,LoopAllocBytes,AudioSrcMs,InterpMs");

            for (int i = 0; i < sampleCount; i++)
            {
                var s = snapshot[(start + i) % detail_capacity];

                sb.Append(EzProbeOutput.Csv(s.WallMs)).Append(',');
                sb.Append(s.FrameIndex).Append(',');
                sb.Append(EzProbeOutput.Csv(s.ElapsedMs)).Append(',');
                sb.Append(EzProbeOutput.Csv(s.GcPauseDeltaMs)).Append(',');
                sb.Append(s.ThreadAllocDeltaBytes).Append(',');
                sb.Append(s.PressesInFrame).Append(',');
                sb.Append(EzProbeOutput.Csv(s.PressColumnMs)).Append(',');
                sb.Append(EzProbeOutput.Csv(s.SincePrevFrameMs)).Append(',');
                sb.Append(s.FscIterations).Append(',');
                sb.Append(s.Gen0Delta).Append(',');
                sb.Append(s.Gen1Delta).Append(',');
                sb.Append(EzProbeOutput.Csv(s.SubtreeMs)).Append(',');
                sb.Append(s.LoopAllocBytes).Append(',');
                sb.Append(EzProbeOutput.Csv(s.AudioSrcMs)).Append(',');
                sb.Append(EzProbeOutput.Csv(s.InterpMs));
                sb.AppendLine();
            }

            // 全帧分布只存在于摘要里（CSV 按定义只有尾部），所以摘要必须和 CSV 一起落盘，
            // 不能只丢进日志——日志路径随运行方式变化，分析脚本没法可靠地找到它。
            return EzProbeOutput.WriteAsync("framestall", sb.ToString(), sampleCount, "EzFrameStall", FormatSummary());
        }

        private static long sumBuckets(long[] buckets)
        {
            long total = 0;

            for (int i = 0; i < buckets.Length; i++)
                total += buckets[i];

            return total;
        }

        private static double bucketMean(long[] buckets, double bucketSize, long total)
        {
            if (total <= 0)
                return 0;

            double weighted = 0;

            for (int i = 0; i < buckets.Length; i++)
                weighted += buckets[i] * (i + 0.5) * bucketSize;

            return weighted / total;
        }

        private static double bucketPercentile(long[] buckets, double bucketSize, long total, double percentile)
        {
            if (total <= 0)
                return 0;

            long target = (long)Math.Ceiling(total * percentile);
            long seen = 0;

            for (int i = 0; i < buckets.Length; i++)
            {
                seen += buckets[i];

                if (seen >= target)
                    return (i + 0.5) * bucketSize;
            }

            return buckets.Length * bucketSize;
        }

        private static long bucketOver(long[] buckets, double bucketSize, double threshold)
        {
            long count = 0;

            for (int i = 0; i < buckets.Length; i++)
            {
                if ((i + 0.5) * bucketSize >= threshold)
                    count += buckets[i];
            }

            return count;
        }

        private static string FormatSummary()
        {
            long frames = withoutPress.Count + withPress.Count;

            if (frames == 0)
                return "[EzFrameStall] no frames";

            double wallSeconds = (lastFrameWallMs - firstFrameWallMs) / 1000.0;

            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture,
                $"[EzFrameStall] frames={frames} span={wallSeconds:F1}s threshold={ThresholdMs:F2}ms "
                + $"mode={(Deep ? "deep" : "light")} "
                + $"stallFrames={stallFrames} (withPress={stallFramesWithPress}) "
                + $"overwritten={Interlocked.Read(ref detailOverwritten)} ");

            if (!Deep)
            {
                sb.Append(CultureInfo.InvariantCulture, $" (light mode: GC/alloc readings skipped)");
            }
            else
            {
                sb.Append(CultureInfo.InvariantCulture, $"alloc(updateThread)={threadAllocatedTotal / 1024.0 / 1024.0:F1}MB ");
                sb.Append(CultureInfo.InvariantCulture, $"process={processAllocatedTotal / 1024.0 / 1024.0:F1}MB ");
                sb.Append(CultureInfo.InvariantCulture, $"gcPause={gcPauseTotalMs:F0}ms");

                if (wallSeconds > 0)
                    sb.Append(CultureInfo.InvariantCulture, $" ({100 * gcPauseTotalMs / (wallSeconds * 1000):F2}%)");
            }

            // 「FSC 子树 vs 其余」：帧长的大头在 ruleset 的 drawable 层级里，还是在 HUD / 框架调度里。
            // 这是决定往哪优化的一行——子树占比低就说明问题不在 mania playfield 内。
            if (probeFrames > 0)
            {
                double meanElapsedAll = elapsedProbeTotalMs / probeFrames;
                double meanSubtree = subtreeMsTotal / probeFrames;
                double meanClock = clockMsTotal / probeFrames;
                double meanRest = meanElapsedAll - meanSubtree - meanClock;

                sb.Append(Environment.NewLine);
                sb.Append(CultureInfo.InvariantCulture,
                    $"frameSplit frames={probeFrames} elapsedMean={meanElapsedAll:F3}ms "
                    + $"subtreeMsMean={meanSubtree:F3}ms "
                    + $"clockMsMean={meanClock:F3}ms "
                    + $"restMsMean={meanRest:F3}ms "
                    + $"subtreeShareMs={100 * meanSubtree / meanElapsedAll:F1}% "
                    + $"loopAllocMean={loopAllocTotal / (double)probeFrames:F0}B");
            }

            // present 侧：往屏幕上送的那份内容「多旧」，以及 draw 线程自己的节拍。note 位置在 update 线程里就定好了，
            // 之后只剩渲染与送屏——这是 update 线程之外唯一还没量的一环。
            if (presentFrames > 0)
            {
                sb.Append(Environment.NewLine);
                sb.Append("present frames=").Append(presentFrames).Append(" skipped=").Append(presentSkipped)
                  .Append(" dup=").Append(presentDuplicates)
                  .Append(" frozenSkipped=").Append(presentFrozenSkipped)
                  .Append(CultureInfo.InvariantCulture, $" coverage={drawPeriodSumMs / Math.Max(1, presentLastWallMs - presentFirstWallMs):F3}")
                  .Append(" drawPeriod");
                sb.Append(drawPeriod.Format());

                // 「相邻两次 present 的间隔」由 draw 线程自己的时钟测。update 侧看到的帧一直很稳，
                // 但 draw 线程单独卡一下 update 探针是看不见的——那一下就是屏幕上的一次跳帧，所以这里要看的是尾部。
                // presentAge 是上屏那一刻那份内容的年龄；零点只能取「上一次 update 帧边界」（读不到被绘制的
                // buffer 帧号），所以带一个 draw 周期量级的配对不确定度，只用来判毫秒级以上的滞后。
                sb.Append(Environment.NewLine);
                sb.Append("  presentAge");
                sb.Append(presentAge.Format());

                var host = gameHost;

                if (host != null)
                {
                    var dc = host.DrawThread.Clock;
                    var uc = host.UpdateThread.Clock;
                    var window = host.Window;

                    sb.Append(Environment.NewLine);
                    sb.Append(CultureInfo.InvariantCulture,
                        $"  clocks draw(fps={dc.FramesPerSecond} jitter={dc.Jitter:F3}ms sleptMean={drawSleptTotalMs / presentFrames:F3}ms maxHz={dc.MaximumUpdateHz:F0} throttling={dc.Throttling}) "
                        + $"update(fps={uc.FramesPerSecond} jitter={uc.Jitter:F3}ms slept={uc.TimeSlept:F3}ms maxHz={uc.MaximumUpdateHz:F0} throttling={uc.Throttling})");

                    if (window != null)
                        sb.Append(CultureInfo.InvariantCulture, $" window={window.WindowState} refresh={window.CurrentDisplayMode.Value.RefreshRate:F0}Hz");
                }
            }

            // 音频时钟：源是不是阶梯、插值有没有把阶梯抹平。note 位置取自插值时钟，所以这是「下落顺不顺滑」的直接判据；
            // 也是 ~10Hz 的判定/按键探针看不见 100Hz 结构的原因（见 docs/EZ-PERFORMANCE.md §2.4.11）。
            if (audioStep.Count > 0)
            {
                long stepFrames = audioStep.Count;
                long stepNonZero = audioStep.CountOver(0.1);

                sb.Append(Environment.NewLine);
                sb.Append(CultureInfo.InvariantCulture,
                    $"clockQuant frames={stepFrames} rateSkipped={rateSkipped}(帧长>5ms) "
                    + $"clockStopped={clockStoppedFrames}帧/{clockStoppedMs:F1}ms");

                sb.Append(Environment.NewLine);
                sb.Append(CultureInfo.InvariantCulture,
                    $"  audioStep nonzero={100 * stepNonZero / (double)stepFrames:F2}% "
                    + $"mean={audioStep.Mean:F4}ms top:");
                sb.Append(audioStep.FormatBuckets(5));

                if (errCount > 0)
                {
                    double errMean = errSum / errCount;
                    double errStd = Math.Sqrt(Math.Max(0, errSqSum / errCount - errMean * errMean));
                    double driftMean = driftSum / driftCount;
                    double driftStd = Math.Sqrt(Math.Max(0, driftSqSum / driftCount - driftMean * driftMean));

                    // 位置精度：interp 相对「报告值 + 缓冲内已播时长」的偏差去掉常数后的抖动。这是「顺不顺滑」的判据。
                    // offset ≈ 音频报告位置领先「听到的声音」的时长（约一个缓冲），会被音频偏移吸收，不用管它绝对值。
                    // pullMiss 必须一起读：漏拉会让源停走 ~20ms 再补上，插值追赶期间的偏差全部计进 std，
                    // 于是「源在卡」会被误判成「插值不干净」（实测同一台机两次采集 std 0.41 → 1.08，pullMiss 5 → 147）。
                    double spanS = (lastFrameWallMs - firstFrameWallMs) / 1000.0;

                    sb.Append(Environment.NewLine);
                    sb.Append("  interpErr");

                    if (pullMissCount > 0)
                        sb.Append(CultureInfo.InvariantCulture, $" pullMiss={pullMissCount}({pullMissCount / Math.Max(1, spanS):F1}/s,hold{pullMissHoldMs:F0}ms)");

                    sb.Append(CultureInfo.InvariantCulture,
                        $" std={errStd:F3}ms p99={interpErr.Percentile(0.99):F3}ms maxdev={errMaxAbs:F3}ms "
                        + $"offset={errMean:F2}ms 超[0.5/1/2]ms={100 * errOver05 / (double)errCount:F2}/{100 * errOver1 / (double)errCount:F2}/{100 * errOver2 / (double)errCount:F2}%");

                    // drift 只是 interp − 音频报告值：含音频偏移常数与缓冲锯齿，幅度天然≈缓冲步长，别单独当抖动读。
                    sb.Append(Environment.NewLine);
                    sb.Append(CultureInfo.InvariantCulture,
                        $"  drift(含偏移+锯齿) mean={driftMean:F3}ms range=[{driftMin:F3},{driftMax:F3}]ms std={driftStd:F3}ms");
                }
            }

            // NAudio 渲染线程的拉取节拍。位置序列只能看出「源停走过」，看不出是晚醒还是生产慢：
            // NAudio 每次要的是「当时所有空位」而不是一拍，所以两次拉取的壁钟间隔 + 请求的拍数才能分开这两者。
            if (wasapiReadSnapshot is { Reads: > 0 } reads)
            {
                long gapTotal = sumBuckets(reads.Gaps) + reads.GapOverflow;
                long periodTotal = sumBuckets(reads.Periods) + reads.PeriodOverflow;

                sb.Append(Environment.NewLine);
                sb.Append(CultureInfo.InvariantCulture,
                    $"wasapiRead reads={reads.Reads} ({reads.Reads / Math.Max(0.001, wallSeconds):F1}/s) "
                    + $"gapsMs mean={bucketMean(reads.Gaps, WasapiReadStats.GAP_BUCKET_MS, gapTotal):F3} "
                    + $"p50={bucketPercentile(reads.Gaps, WasapiReadStats.GAP_BUCKET_MS, gapTotal, 0.50):F2} "
                    + $"p90={bucketPercentile(reads.Gaps, WasapiReadStats.GAP_BUCKET_MS, gapTotal, 0.90):F2} "
                    + $"p99={bucketPercentile(reads.Gaps, WasapiReadStats.GAP_BUCKET_MS, gapTotal, 0.99):F2} "
                    + $"max={reads.MaxGapMs:F2} over15={bucketOver(reads.Gaps, WasapiReadStats.GAP_BUCKET_MS, 15) + reads.GapOverflow}");
                sb.Append(Environment.NewLine);
                sb.Append(CultureInfo.InvariantCulture,
                    $"  wasapiPull periods/pull mean={reads.TotalPeriods / Math.Max(1, periodTotal):F3} "
                    + $"median={bucketPercentile(reads.Periods, WasapiReadStats.PERIOD_BUCKET, periodTotal, 0.50):F2} "
                    + $"max={reads.MaxPeriods:F2} over1.5={bucketOver(reads.Periods, WasapiReadStats.PERIOD_BUCKET, 1.5) + reads.PeriodOverflow}"
                    + $" totalPeriods={reads.TotalPeriods:F0}");
            }

            // 两组分布并排，是「一次按键把帧拉长了多少」的直接答案。
            sb.Append(Environment.NewLine);
            sb.Append("noPress  ").Append(withoutPress.Format());
            sb.Append(Environment.NewLine);
            sb.Append("withPress").Append(withPress.Format());

            if (pressFrameCount > 0)
            {
                double meanElapsed = withPress.Sum / pressFrameCount;
                double meanSincePrev = sincePrevFrameTotalMs / pressFrameCount;
                double meanColumn = pressColumnTotalMs / pressFrameCount;

                // 只有 pressColumnMean 是按键自身的工作量。sincePrevFrameMean 的零点在上一帧边界，
                // 里面是上一帧剩余时间 + draw/present + 帧间等待，**不是**本帧按键前的工作。
                sb.Append(Environment.NewLine);
                sb.Append(CultureInfo.InvariantCulture,
                    $"pressSplit n={pressFrameCount} elapsedMean={meanElapsed:F3}ms "
                    + $"sincePrevFrameMean={meanSincePrev:F3}ms pressColumnMean={meanColumn:F3}ms "
                    + $"afterPressMean={meanElapsed - meanSincePrev - meanColumn:F3}ms "
                    + $"inputQueue={inputQueueCount}");
            }

            return sb.ToString();
        }

        /// <summary>定长直方图：只做加法与读数，热路径零分配。</summary>
        private sealed class Histogram
        {
            private const int bucket_count = 1024;

            private readonly double bucketWidthMs;
            private readonly int[] buckets = new int[bucket_count];

            /// <param name="bucketWidthMs">
            /// 桶宽，也就是读数分辨率。默认 0.1ms 够看帧耗时；看亚毫秒级的时钟偏差要传更细的（量程 = 桶宽 × 1024）。
            /// </param>
            public Histogram(double bucketWidthMs = 0.1)
            {
                this.bucketWidthMs = bucketWidthMs;
            }

            public long Count { get; private set; }
            public double Sum { get; private set; }
            public double SumSquares { get; private set; }
            public double Min { get; private set; } = double.MaxValue;
            public double Max { get; private set; }

            public void Add(double ms)
            {
                Count++;
                Sum += ms;
                SumSquares += ms * ms;

                if (ms < Min)
                    Min = ms;

                if (ms > Max)
                    Max = ms;

                int bucket = (int)(ms / bucketWidthMs);
                buckets[bucket >= bucket_count ? bucket_count - 1 : bucket < 0 ? 0 : bucket]++;
            }

            public void Reset()
            {
                Array.Clear(buckets);
                Count = 0;
                Sum = 0;
                SumSquares = 0;
                Min = double.MaxValue;
                Max = 0;
            }

            public double Mean => Count == 0 ? double.NaN : Sum / Count;

            /// <summary>
            /// 标准差，精确值（不是桶中心近似的）。判「抖不抖」用它，别用分位差——分位差会被量化桶抹平，
            /// 而这个探针要量的恰恰是亚毫秒级的抖动。
            /// </summary>
            public double Std
            {
                get
                {
                    if (Count < 2)
                        return double.NaN;

                    double mean = Sum / Count;
                    return Math.Sqrt(Math.Max(0, SumSquares / Count - mean * mean));
                }
            }

            public double Percentile(double p)
            {
                if (Count == 0)
                    return double.NaN;

                long target = (long)Math.Floor((Count - 1) * p);
                long cumulative = 0;

                for (int i = 0; i < bucket_count; i++)
                {
                    cumulative += buckets[i];

                    if (cumulative > target)
                        return (i + 0.5) * bucketWidthMs;
                }

                return bucket_count * bucketWidthMs;
            }

            public long CountOver(double ms)
            {
                int first = (int)Math.Ceiling(ms / bucketWidthMs);
                long count = 0;

                for (int i = first < 0 ? 0 : first; i < bucket_count; i++)
                    count += buckets[i];

                return count;
            }

            /// <summary>该组的占比：P(帧耗时 &gt; 阈值)。两组对比即可知道按键是否真的把帧推过了阈值。</summary>
            public double FractionOver(double ms) => Count == 0 ? double.NaN : CountOver(ms) / (double)Count;

            public string Format()
                => string.Create(CultureInfo.InvariantCulture,
                    $" n={Count} mean={Mean:F3} std={Std:F3} min={(Count == 0 ? double.NaN : Min):F3} "
                    + $"p50={Percentile(0.5):F3} p90={Percentile(0.9):F3} "
                    + $"p99={Percentile(0.99):F3} p99.9={Percentile(0.999):F3} max={Max:F3} "
                    + $"over0.5={CountOver(0.5)} over1={CountOver(1.0)} over2={CountOver(2.0)} over5={CountOver(5.0)}");

            /// <summary>
            /// 非空桶按计数降序，用于展示「取值只落在哪几个点上」这类量化特征 ——
            /// 音频源时钟的逐帧步进就是这种形状（几乎全是 0 与一个固定步长）。
            /// </summary>
            public string FormatBuckets(int top)
            {
                var nonEmpty = new List<(double ms, int count)>();

                for (int i = 0; i < bucket_count; i++)
                {
                    if (buckets[i] > 0)
                        nonEmpty.Add(((i + 0.5) * bucketWidthMs, buckets[i]));
                }

                nonEmpty.Sort((a, b) => b.count.CompareTo(a.count));

                var sb = new StringBuilder();

                for (int i = 0; i < nonEmpty.Count && i < top; i++)
                    sb.Append(CultureInfo.InvariantCulture, $" {nonEmpty[i].ms:F2}ms={nonEmpty[i].count}");

                return sb.ToString();
            }
        }
    }
}
