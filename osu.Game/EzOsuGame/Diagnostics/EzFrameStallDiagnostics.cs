// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.UI;

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
    /// 热路径不做 IO、不产生字符串；落盘在局末。
    /// </summary>
    public static class EzFrameStallDiagnostics
    {
        /// <summary>是否采集。与其它探针同源，由 <c>Player</c> 统一开关。</summary>
        public static bool Enabled { get; set; }

        /// <summary>超过该时长的帧才留明细；直方图则覆盖所有帧。可用 <c>EZ_FRAME_PROBE_MS</c> 覆盖。</summary>
        public static double ThresholdMs { get; set; } = 1.5;

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

        private const int bucket_count = 1024;
        private const int detail_capacity = 32768;

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
        private static int lastGen2;

        private static long pressFrameCount;
        private static double firstPressAtTotalMs;
        private static double pressColumnTotalMs;

        private static int pressesInFrame;
        private static double pressColumnMsInFrame;
        private static long currentFrameStartTimestamp;
        private static double firstPressAtMsInFrame = double.MaxValue;

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
            double FirstPressAtMs,
            int FscIterations,
            int Gen0Delta,
            int Gen1Delta,
            int Gen2Delta);

        /// <summary>
        /// 本帧内已处理（进入列入口）的按键，由 <c>Column.OnPressed</c> 调用。
        /// </summary>
        /// <param name="pressEnterTs">该次按键进入本列的 wall 戳（<c>Stopwatch</c> 计时单位）。</param>
        /// <param name="columnMs">该次按键在本列花掉的时长。</param>
        /// <remarks>
        /// 有了 <paramref name="pressEnterTs"/> 就能把一次按键帧切成三段：
        /// <c>FirstPressAtMs</c>（帧起到按键进入本列） + <c>PressColumnMs</c>（本列工时）
        /// + 余量（<c>ElapsedMs</c> 减前两者，即按键之后的帧内工作）。
        /// 判读：若 <c>FirstPressAtMs</c> 占了帧长的大头，说明时间花在「按键被派发到本列之前」的机器上；
        /// 若余量占大头，说明是判定落地**之后**的帧内工作（结果扇出、容器增删、布局失效等）。
        /// </remarks>
        public static void NotifyPress(long pressEnterTs, double columnMs)
        {
            if (!Enabled)
                return;

            pressesInFrame++;
            pressColumnMsInFrame += columnMs;

            if (currentFrameStartTimestamp != 0)
            {
                double intoFrameMs = (pressEnterTs - currentFrameStartTimestamp) * 1000.0 / Stopwatch.Frequency;

                if (intoFrameMs < firstPressAtMsInFrame)
                    firstPressAtMsInFrame = intoFrameMs;
            }
        }

        /// <summary>
        /// 记一次帧边界。调用点必须是每帧同一位置，否则相邻两次之差不是整帧时长。
        /// 首帧只用于播种各计数器基线，不计入统计。
        /// </summary>
        public static void RecordFrame()
        {
            if (!Enabled)
                return;

            long now = Stopwatch.GetTimestamp();
            double wallMs = EzJudgmentDiagnostics.WallClockMs;

            long prev = lastFrameTimestamp;
            lastFrameTimestamp = now;
            currentFrameStartTimestamp = prev;
            frameIndex++;

            // light 模式只留直方图，跳掉每帧的 GC/分配读数（那几个 API 会落在同帧按键的等待里）。
            long gcPauseTicks = 0;
            long threadAllocated = 0;
            long processAllocated = 0;
            int gen0 = 0, gen1 = 0, gen2 = 0;

            if (Deep)
            {
                gcPauseTicks = GC.GetTotalPauseDuration().Ticks;
                threadAllocated = GC.GetAllocatedBytesForCurrentThread();
                processAllocated = GC.GetTotalAllocatedBytes(false);
                gen0 = GC.CollectionCount(0);
                gen1 = GC.CollectionCount(1);
                gen2 = GC.CollectionCount(2);
            }

            if (prev == 0)
            {
                seed(gcPauseTicks, threadAllocated, processAllocated, gen0, gen1, gen2);
                pressesInFrame = 0;
                pressColumnMsInFrame = 0;
                firstPressAtMsInFrame = double.MaxValue;
                firstFrameWallMs = wallMs;
                return;
            }

            double elapsedMs = (now - prev) * 1000.0 / Stopwatch.Frequency;
            lastFrameWallMs = wallMs;

            if (pressesInFrame > 0)
            {
                withPress.Add(elapsedMs);
                pressFrameCount++;

                if (firstPressAtMsInFrame != double.MaxValue)
                    firstPressAtTotalMs += firstPressAtMsInFrame;

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
                    pressesInFrame > 0 && firstPressAtMsInFrame != double.MaxValue ? firstPressAtMsInFrame : double.NaN,
                    FrameStabilityContainer.EzLastUpdateIterations,
                    gen0 - lastGen0,
                    gen1 - lastGen1,
                    gen2 - lastGen2);

                detailCursor++;

                if (detailCursor >= detail_capacity)
                {
                    detailCursor = 0;
                    Interlocked.Increment(ref detailOverwritten);
                }

                if (detailCount < detail_capacity)
                    detailCount++;
            }

            seed(gcPauseTicks, threadAllocated, processAllocated, gen0, gen1, gen2);
            pressesInFrame = 0;
            pressColumnMsInFrame = 0;
            firstPressAtMsInFrame = double.MaxValue;
        }

        private static void seed(long gcPauseTicks, long threadAllocated, long processAllocated, int gen0, int gen1, int gen2)
        {
            lastGcPauseTicks = gcPauseTicks;
            lastThreadAllocated = threadAllocated;
            lastProcessAllocated = processAllocated;
            lastGen0 = gen0;
            lastGen1 = gen1;
            lastGen2 = gen2;
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
            lastGcPauseTicks = -1;
            lastThreadAllocated = 0;
            lastProcessAllocated = 0;
            lastGen0 = lastGen1 = lastGen2 = 0;
            pressesInFrame = 0;
            pressColumnMsInFrame = 0;
            firstPressAtMsInFrame = double.MaxValue;
            pressFrameCount = 0;
            firstPressAtTotalMs = 0;
            pressColumnTotalMs = 0;
            inputQueueCount = -1;

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

            var sb = new StringBuilder();
            sb.AppendLine("WallMs,FrameIndex,ElapsedMs,GcPauseDeltaMs,ThreadAllocDeltaBytes,PressesInFrame,PressColumnMs,FirstPressAtMs,FscIter,Gen0Delta,Gen1Delta,Gen2Delta");

            for (int i = 0; i < sampleCount; i++)
            {
                var s = snapshot[(start + i) % detail_capacity];

                sb.Append(num(s.WallMs)).Append(',');
                sb.Append(s.FrameIndex).Append(',');
                sb.Append(num(s.ElapsedMs)).Append(',');
                sb.Append(num(s.GcPauseDeltaMs)).Append(',');
                sb.Append(s.ThreadAllocDeltaBytes).Append(',');
                sb.Append(s.PressesInFrame).Append(',');
                sb.Append(num(s.PressColumnMs)).Append(',');
                sb.Append(num(s.FirstPressAtMs)).Append(',');
                sb.Append(s.FscIterations).Append(',');
                sb.Append(s.Gen0Delta).Append(',');
                sb.Append(s.Gen1Delta).Append(',');
                sb.Append(s.Gen2Delta);
                sb.AppendLine();
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string dir = EzJudgmentDiagnostics.GetDiagnosticsDirectory();
            string path = Path.Combine(dir, $"framestall_{stamp}.csv");

            // 全帧分布只存在于摘要里（CSV 按定义只有尾部），所以摘要必须和 CSV 一起落盘，
            // 不能只丢进日志——日志路径随运行方式变化，分析脚本没法可靠地找到它。
            string summaryPath = Path.Combine(dir, $"framestall_{stamp}.summary.txt");

            string summary = FormatSummary();
            string content = sb.ToString();

            _ = Task.Run(async () =>
            {
                try
                {
                    await File.WriteAllTextAsync(path, content).ConfigureAwait(false);
                    await File.WriteAllTextAsync(summaryPath, summary + Environment.NewLine).ConfigureAwait(false);
                    Logger.Log($"[EzFrameStall] flushed {sampleCount} samples to {path}", Ez2ConfigManager.LOGGER_NAME);
                    Logger.Log(summary, Ez2ConfigManager.LOGGER_NAME);
                }
                catch (Exception ex)
                {
                    try { Logger.Log($"[EzFrameStall] flush failed: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, level: LogLevel.Error); }
                    catch { }
                }
            });

            return path;
        }

        /// <summary>CSV 一律用不变区域，避免逗号小数分隔符的地区写出畸形列。</summary>
        private static string num(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

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

            // 两组分布并排，是「一次按键把帧拉长了多少」的直接答案。
            sb.Append(Environment.NewLine);
            sb.Append("noPress  ").Append(withoutPress.Format());
            sb.Append(Environment.NewLine);
            sb.Append("withPress").Append(withPress.Format());

            if (pressFrameCount > 0)
            {
                double meanElapsed = withPress.Sum / pressFrameCount;
                double meanFirst = firstPressAtTotalMs / pressFrameCount;
                double meanColumn = pressColumnTotalMs / pressFrameCount;

                // 三段切分：帧起→按键进入本列 / 本列工时 / 按键之后到帧尾。
                sb.Append(Environment.NewLine);
                sb.Append(CultureInfo.InvariantCulture,
                    $"pressSplit n={pressFrameCount} elapsedMean={meanElapsed:F3}ms "
                    + $"firstPressAtMean={meanFirst:F3}ms pressColumnMean={meanColumn:F3}ms "
                    + $"afterPressMean={meanElapsed - meanFirst - meanColumn:F3}ms "
                    + $"inputQueue={inputQueueCount}");
            }

            return sb.ToString();
        }

        /// <summary>定长直方图：只做加法与读数，热路径零分配。</summary>
        private sealed class Histogram
        {
            private const double bucket_width_ms = 0.1;
            private const int bucket_count = 1024;

            private readonly int[] buckets = new int[bucket_count];

            public long Count { get; private set; }
            public double Sum { get; private set; }
            public double Max { get; private set; }

            public void Add(double ms)
            {
                Count++;
                Sum += ms;

                if (ms > Max)
                    Max = ms;

                int bucket = (int)(ms / bucket_width_ms);
                buckets[bucket >= bucket_count ? bucket_count - 1 : bucket < 0 ? 0 : bucket]++;
            }

            public void Reset()
            {
                Array.Clear(buckets);
                Count = 0;
                Sum = 0;
                Max = 0;
            }

            public double Mean => Count == 0 ? double.NaN : Sum / Count;

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
                        return (i + 0.5) * bucket_width_ms;
                }

                return bucket_count * bucket_width_ms;
            }

            public long CountOver(double ms)
            {
                int first = (int)Math.Ceiling(ms / bucket_width_ms);
                long count = 0;

                for (int i = first < 0 ? 0 : first; i < bucket_count; i++)
                    count += buckets[i];

                return count;
            }

            /// <summary>该组的占比：P(帧耗时 &gt; 阈值)。两组对比即可知道按键是否真的把帧推过了阈值。</summary>
            public double FractionOver(double ms) => Count == 0 ? double.NaN : CountOver(ms) / (double)Count;

            public string Format()
                => string.Create(CultureInfo.InvariantCulture,
                    $" n={Count} mean={Mean:F3} p50={Percentile(0.5):F3} p90={Percentile(0.9):F3} "
                    + $"p99={Percentile(0.99):F3} p99.9={Percentile(0.999):F3} max={Max:F3} "
                    + $"over0.5={CountOver(0.5)} over1={CountOver(1.0)} over2={CountOver(2.0)} over5={CountOver(5.0)}");
        }
    }
}
