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

namespace osu.Game.EzOsuGame.Diagnostics
{
    /// <summary>
    /// 帧耗时（update 线程 stall）探针，挂在 <c>FrameStabilityContainer.UpdateSubTree</c> 末尾的帧边界上。
    /// <para>
    /// 与 <see cref="EzPressLatencyDiagnostics"/> 的关系：那个探针只采样「有按键的帧」，
    /// 于是在没有按键落下时帧有多慢是看不见的——而按键延迟的上界正是「它落在的那一帧的时长」。
    /// 本探针补上这条基线：每帧都记一次耗时（定长直方图，零分配），只在超过阈值时留明细。
    /// </para>
    /// <para>
    /// 明细行里带该帧的 <c>GcPauseDeltaMs</c>（跨该帧的 <c>GC.GetTotalPauseDuration</c> 增量）。
    /// 这是把 GC 从「嫌疑」定成「因果」的判据：若一次 20ms 的 stall 里 <c>GcPauseDeltaMs</c> 也接近 20ms，
    /// GC 就是原因；若接近 0，GC 即可排除。
    /// </para>
    /// <para>
    /// 同时记 update 线程的分配增量（<c>GC.GetAllocatedBytesForCurrentThread</c>）与全进程分配增量，
    /// 两者的比值说明高 gen0 频率（实测 ~24/s）是不是 update 线程自己在分配。
    /// </para>
    /// 热路径不做 IO、不产生字符串；落盘在局末。
    /// </summary>
    public static class EzFrameStallDiagnostics
    {
        /// <summary>是否采集。与其它探针同源，由 <c>Player</c> 统一开关。</summary>
        public static bool Enabled { get; set; }

        /// <summary>超过该时长的帧才留明细；直方图则覆盖所有帧。可用 <c>EZ_FRAME_PROBE_MS</c> 覆盖。</summary>
        public static double ThresholdMs { get; set; } = 1.5;

        private const double bucket_width_ms = 0.1;
        private const int bucket_count = 1024;
        private const int detail_capacity = 32768;

        private static readonly int[] histogram = new int[bucket_count];
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
        private static int pressesInFrame;

        // 会话累计
        private static long frames;
        private static long stallFrames;
        private static long stallFramesWithPress;
        private static long framesWithPress;
        private static double maxElapsedMs;
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
            int FscIterations,
            int Gen0Delta,
            int Gen1Delta,
            int Gen2Delta);

        /// <summary>本帧内已处理（进入列入口）的按键数，由 <c>Column.OnPressed</c> 调用。</summary>
        public static void NotifyPress()
        {
            if (Enabled)
                pressesInFrame++;
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

            long prev = lastFrameTimestamp;
            lastFrameTimestamp = now;
            frameIndex++;

            long gcPauseTicks = GC.GetTotalPauseDuration().Ticks;
            long threadAllocated = GC.GetAllocatedBytesForCurrentThread();
            long processAllocated = GC.GetTotalAllocatedBytes(false);
            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);

            if (prev == 0)
            {
                seed(gcPauseTicks, threadAllocated, processAllocated, gen0, gen1, gen2);
                pressesInFrame = 0;
                return;
            }

            double elapsedMs = (now - prev) * 1000.0 / Stopwatch.Frequency;

            frames++;
            if (elapsedMs > maxElapsedMs)
                maxElapsedMs = elapsedMs;

            int bucket = (int)(elapsedMs / bucket_width_ms);
            histogram[bucket >= bucket_count ? bucket_count - 1 : bucket]++;

            double gcPauseDeltaMs = (gcPauseTicks - lastGcPauseTicks) / (double)TimeSpan.TicksPerMillisecond;
            long threadAllocDelta = threadAllocated - lastThreadAllocated;

            gcPauseTotalMs += gcPauseDeltaMs;
            threadAllocatedTotal += threadAllocDelta;
            processAllocatedTotal += processAllocated - lastProcessAllocated;

            if (pressesInFrame > 0)
                framesWithPress++;

            if (elapsedMs >= ThresholdMs)
            {
                stallFrames++;

                if (pressesInFrame > 0)
                    stallFramesWithPress++;

                details[detailCursor] = new FrameSample(
                    EzJudgmentDiagnostics.WallClockMs,
                    frameIndex,
                    elapsedMs,
                    gcPauseDeltaMs,
                    threadAllocDelta,
                    pressesInFrame,
                    osu.Game.Rulesets.UI.FrameStabilityContainer.EzLastUpdateIterations,
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
            Array.Clear(histogram);

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

            frames = 0;
            stallFrames = 0;
            stallFramesWithPress = 0;
            framesWithPress = 0;
            maxElapsedMs = 0;
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
            sb.AppendLine("WallMs,FrameIndex,ElapsedMs,GcPauseDeltaMs,ThreadAllocDeltaBytes,PressesInFrame,FscIter,Gen0Delta,Gen1Delta,Gen2Delta");

            for (int i = 0; i < sampleCount; i++)
            {
                var s = snapshot[(start + i) % detail_capacity];

                sb.Append(num(s.WallMs)).Append(',');
                sb.Append(s.FrameIndex).Append(',');
                sb.Append(num(s.ElapsedMs)).Append(',');
                sb.Append(num(s.GcPauseDeltaMs)).Append(',');
                sb.Append(s.ThreadAllocDeltaBytes).Append(',');
                sb.Append(s.PressesInFrame).Append(',');
                sb.Append(s.FscIterations).Append(',');
                sb.Append(s.Gen0Delta).Append(',');
                sb.Append(s.Gen1Delta).Append(',');
                sb.Append(s.Gen2Delta);
                sb.AppendLine();
            }

            string dir = EzJudgmentDiagnostics.GetDiagnosticsDirectory();
            string path = Path.Combine(dir, $"framestall_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            string summary = FormatSummary();
            string content = sb.ToString();

            _ = Task.Run(async () =>
            {
                try
                {
                    await File.WriteAllTextAsync(path, content).ConfigureAwait(false);
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
            if (frames == 0)
                return "[EzFrameStall] no frames";

            // 直方图分位数：帧数足够多时，这比只有「有按键的帧」的读数更能代表真实分布。
            double p50 = percentile(0.50);
            double p99 = percentile(0.99);
            double p999 = percentile(0.999);

            long over05 = countOver(0.5);
            long over1 = countOver(1.0);
            long over2 = countOver(2.0);
            long over5 = countOver(5.0);
            long over10 = countOver(10.0);

            double threadMb = threadAllocatedTotal / 1024.0 / 1024.0;
            double processMb = processAllocatedTotal / 1024.0 / 1024.0;
            double wallSeconds = frames * p50 / 1000.0;

            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture,
                $"[EzFrameStall] frames={frames} wall≈{wallSeconds:F1}s threshold={ThresholdMs:F2}ms "
                + $"p50={p50:F3} p99={p99:F3} p99.9={p999:F3} max={maxElapsedMs:F3} "
                + $"over0.5={over05} over1={over1} over2={over2} over5={over5} over10={over10} "
                + $"stallFrames={stallFrames} (withPress={stallFramesWithPress}) framesWithPress={framesWithPress} "
                + $"overwritten={Interlocked.Read(ref detailOverwritten)}");

            string share = processMb > 0
                ? string.Create(CultureInfo.InvariantCulture, $" share={(100 * threadMb / processMb):F1}%")
                : string.Empty;

            string pauseShare = wallSeconds > 0
                ? string.Create(CultureInfo.InvariantCulture, $" ({100 * gcPauseTotalMs / (wallSeconds * 1000):F2}%)")
                : string.Empty;

            sb.Append(CultureInfo.InvariantCulture, $" | alloc(updateThread)={threadMb:F1}MB process={processMb:F1}MB");
            sb.Append(share);
            sb.Append(CultureInfo.InvariantCulture, $" gcPause={gcPauseTotalMs:F0}ms");
            sb.Append(pauseShare);

            return sb.ToString();
        }

        private static double percentile(double p)
        {
            long target = (long)Math.Floor((frames - 1) * p);
            long cumulative = 0;

            for (int i = 0; i < bucket_count; i++)
            {
                cumulative += histogram[i];

                if (cumulative > target)
                    return (i + 0.5) * bucket_width_ms;
            }

            return bucket_count * bucket_width_ms;
        }

        private static long countOver(double ms)
        {
            int firstBucket = (int)Math.Ceiling(ms / bucket_width_ms);
            long count = 0;

            for (int i = firstBucket; i < bucket_count; i++)
                count += histogram[i];

            return count;
        }
    }
}
