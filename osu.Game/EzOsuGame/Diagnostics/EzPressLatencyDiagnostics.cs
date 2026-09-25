// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Text;
using System.Threading;

namespace osu.Game.EzOsuGame.Diagnostics
{
    /// <summary>
    /// 按键 → 判定 的**分段**延迟探针（Mania live 路径）。
    /// <para>
    /// 与 <see cref="EzJudgmentDiagnostics"/> 分工：后者记录判定的时钟上下文（偏移侧），
    /// 本类记录一次按键在各阶段花掉的 wall 时间。之所以独立成流，是因为「空按」（本列没有可判目标）
    /// 不会走到 <c>DrawableHitObject.UpdateResult</c>，却恰恰是延迟尾部最可疑的场景。
    /// </para>
    /// <para>
    /// 两个计时段（单位 ms，全部为 wall-clock）：
    /// <c>PreColumnMs</c> = 按键事件入队 → 本列 <c>OnPressed</c> 入口（输入线程交班 + 等待帧 + 派发遍历，列不可控）；
    /// <c>ColumnMs</c> = 本列 <c>OnPressed</c> 全程（键音触发 + 列路由 + 判定 + 同步结果扇出，列可控）。
    /// <c>TotalMs</c> 为二者之和。
    /// </para>
    /// <para>
    /// <c>FrameAgeMs</c> = 本列处理该按键时，「当前游戏帧的时钟」已经旧了多少 ms
    /// （负值表示帧在按键入队之后才建立）。它是帧陈旧度 / catch-up 的直接读数，不是延迟的组成部分。
    /// </para>
    /// 热路径只写预分配环形缓冲，不做 IO、不产生字符串；落盘在局末。
    /// </summary>
    public static class EzPressLatencyDiagnostics
    {
        /// <summary>是否采集。启动时由 <see cref="EzDiagnosticSwitches.Apply"/> 写一次；与判定探针各自独立。</summary>
        public static bool Enabled { get; private set; }

        /// <summary>唯一的生产写入口；测试可直接调用。</summary>
        public static void SetEnabled(bool enabled) => Enabled = enabled;

        /// <summary>
        /// 消融开关：跳过 <c>Column.handleHit</c> 的「提前判 miss」扫描。
        /// 用于把「强制 miss 扫描是否属于延迟尾部」由可疑变成因果，默认关闭。
        /// </summary>
        public static bool SkipForceMiss;

        private const int capacity = 32768;

        private static PressSample[] samples = new PressSample[capacity];
        private static int cursor;
        private static int count;

        /// <summary>环形缓冲覆盖掉的旧样本数（高 KPS 长曲时可能非 0，分析时需知情）。</summary>
        public static long Overwritten => Interlocked.Read(ref overwritten);

        private static long overwritten;

        private static long lastPressFrameId = long.MinValue;
        private static int pressOrdinalInFrame;

        public readonly record struct PressSample(
            double WallMs,
            double GameTime,
            double TotalMs,
            double PreColumnMs,
            double ColumnMs,
            double FrameAgeMs,
            int Column,
            long FrameId,
            int PressOrdinalInFrame,
            bool Routed,
            bool Judged,
            int Entries,
            int ForceMissScan,
            double FrameElapsed,
            int FscIterations,
            int Gen0,
            int Gen1,
            double GcPauseMs);

        /// <summary>开始记录一次按键，返回它在其所属帧内的序号（1 起）。</summary>
        public static int BeginPress(long frameId)
        {
            if (frameId != lastPressFrameId)
            {
                lastPressFrameId = frameId;
                pressOrdinalInFrame = 0;
            }

            return ++pressOrdinalInFrame;
        }

        public static void Record(in PressSample sample)
        {
            if (!Enabled)
                return;

            samples[cursor] = sample;
            cursor++;

            if (cursor >= capacity)
            {
                cursor = 0;
                Interlocked.Increment(ref overwritten);
            }

            if (count < capacity)
                count++;
        }

        /// <summary>
        /// 一次按键在 <c>Column.OnPressed</c> 内的分段耗时（ms，wall-clock，均为相邻两段之差）。
        /// <para>
        /// <c>ColumnMs</c> 只给整段工时，无法回答「首次命中的一次性成本落在哪一段」。本结构把整段切成五块：
        /// <c>BookkeepingMs</c> = 入口 → 键音触发之后（输入队列计数、按键历史、采样触发）；
        /// <c>RouteMs</c> = → 路由配置之后（<c>resolvePressRouting</c>，含首次惰性建表的可能）；
        /// <c>SelectMs</c> = → 选目标之后（<c>SelectPressEntry</c>）；
        /// <c>ApplyMs</c> = → 判定落地之后（<c>applyRoutedPress</c> 的判定 + 同步结果扇出）；
        /// <c>CaptureMs</c> = → 采集结束（探针自身的读数与建样本）。
        /// </para>
        /// <para>
        /// 未走到的阶段差值恒为 0（例如本列不路由输入时 <c>RouteMs</c>…<c>ApplyMs</c> 全为 0），
        /// 所以 <c>RoutedInput</c> 必须一起读：它为 false 时那三段为 0 是「没走」，不是「走了但很快」。
        /// <c>EntriesBefore</c> → <c>EntriesAfter</c> 用来识别「本次按键把某个 note 首次注册进车道」这类事件
        /// （条目数 0 → 1 落在按键内部，说明首次注册的成本记在这次按键上）。
        /// </para>
        /// <para>
        /// 每局清零，故同一进程的第 1 局即可看到冷启动曲线，后续局是同进程的对照。
        /// </para>
        /// </summary>
        public readonly record struct PressBreakdown(
            double WallMs,
            int Column,
            bool RoutedInput,
            bool Routed,
            bool Judged,
            int EntriesBefore,
            int EntriesAfter,
            double BookkeepingMs,
            double RouteMs,
            double SelectMs,
            double ApplyMs,
            double CaptureMs,
            double TotalMs);

        /// <summary>「本局前几次按键」配额：输入路径自身的冷成本（键音池、按键历史、路由惰性表）。</summary>
        public const int BreakdownFirstPressQuota = 16;

        /// <summary>
        /// 「本局前几次已判定按键」配额：按判定与结果扇出（<c>UpdateResult</c> → <c>ApplyResult</c> → 结果订阅者）
        /// 的冷成本。它必须独立于按键配额——引导期空按可以把按键配额占满，而首个判定恰恰是要看的对象。
        /// </summary>
        public const int BreakdownJudgedQuota = 8;

        private const int breakdown_capacity = BreakdownFirstPressQuota + BreakdownJudgedQuota;

        /// <summary>取时间戳的硬上限：判定极少（短局、或整局大量空按）时，防止整局都在取样。</summary>
        private const int breakdown_sample_cap = 192;

        private static readonly PressBreakdown[] breakdown = new PressBreakdown[breakdown_capacity];
        private static int breakdownCount;
        private static int firstPressStored;
        private static int judgedStored;
        private static int breakdownSampled;

        /// <summary>
        /// 这次按键要不要取分段时间戳。配额只看「已存条数」、不看本次结果，所以会变成判定的那次按键
        /// 本身一定已经取了时间戳。两份配额都满、或取样次数触顶后恒为 false。
        /// </summary>
        public static bool BreakdownActive =>
            Enabled
            && breakdownSampled < breakdown_sample_cap
            && (firstPressStored < BreakdownFirstPressQuota || judgedStored < BreakdownJudgedQuota);

        /// <summary>记一次按键的分段：先按「前几次按键」配额填，超出后只收已判定按键（判定配额）。</summary>
        public static void RecordBreakdown(in PressBreakdown sample)
        {
            if (!Enabled || breakdownCount >= breakdown_capacity)
                return;

            if (firstPressStored < BreakdownFirstPressQuota)
            {
                breakdown[breakdownCount++] = sample;
                firstPressStored++;

                if (sample.Judged)
                    judgedStored++;

                return;
            }

            if (sample.Judged && judgedStored < BreakdownJudgedQuota)
            {
                breakdown[breakdownCount++] = sample;
                judgedStored++;
            }
        }

        /// <summary>本次按键已取过分段时间戳（封顶 <see cref="breakdown_sample_cap"/> 用）。</summary>
        public static void MarkBreakdownSampled() => breakdownSampled++;

        public static void Clear()
        {
            Interlocked.Exchange(ref samples, new PressSample[capacity]);
            cursor = 0;
            count = 0;
            Interlocked.Exchange(ref overwritten, 0);
            lastPressFrameId = long.MinValue;
            pressOrdinalInFrame = 0;
            breakdownCount = 0;
            firstPressStored = 0;
            judgedStored = 0;
            breakdownSampled = 0;
        }

        /// <summary>把样本写成 CSV，返回文件路径。IO 在后台线程执行；调用方负责随后 <see cref="Clear"/>。</summary>
        public static string Flush()
        {
            int sampleCount = count;
            int start = sampleCount < capacity ? 0 : cursor;

            var sb = new StringBuilder();
            sb.AppendLine(
                "WallMs,GameTime,TotalMs,PreColumnMs,ColumnMs,FrameAgeMs,Column,FrameId,PressOrdinalInFrame,PressesInFrame,"
                + "Routed,Judged,Entries,ForceMissScan,FrameElapsed,FscIter,Gen0,Gen1,GcPauseMs");

            int[] frameLengths = computeFrameLengths(start, sampleCount);

            for (int i = 0; i < sampleCount; i++)
            {
                var s = samples[(start + i) % capacity];

                sb.Append(EzProbeOutput.Csv(s.WallMs)).Append(',');
                sb.Append(EzProbeOutput.Csv(s.GameTime)).Append(',');
                sb.Append(EzProbeOutput.CsvOrEmpty(s.TotalMs)).Append(',');
                sb.Append(EzProbeOutput.CsvOrEmpty(s.PreColumnMs)).Append(',');
                sb.Append(EzProbeOutput.Csv(s.ColumnMs)).Append(',');
                sb.Append(EzProbeOutput.CsvOrEmpty(s.FrameAgeMs)).Append(',');
                sb.Append(s.Column).Append(',');
                sb.Append(s.FrameId).Append(',');
                sb.Append(s.PressOrdinalInFrame).Append(',');
                sb.Append(frameLengths[i]).Append(',');
                sb.Append(s.Routed ? 1 : 0).Append(',');
                sb.Append(s.Judged ? 1 : 0).Append(',');
                sb.Append(s.Entries).Append(',');
                sb.Append(s.ForceMissScan).Append(',');
                sb.Append(EzProbeOutput.Csv(s.FrameElapsed)).Append(',');
                sb.Append(s.FscIterations).Append(',');
                sb.Append(s.Gen0).Append(',');
                sb.Append(s.Gen1).Append(',');
                sb.Append(EzProbeOutput.Csv(s.GcPauseMs));
                sb.AppendLine();
            }

            return EzProbeOutput.WriteAsync(
                "presslatency",
                sb.ToString(),
                sampleCount,
                "EzPressLatency",
                formatSummary(start, sampleCount, frameLengths) + formatBreakdown());
        }

        /// <summary>
        /// 前若干次按键的分段明细，附在摘要末尾。段序 bookkeeping/route/select/apply/capture，单位 ms。
        /// </summary>
        private static string formatBreakdown()
        {
            if (breakdownCount == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.Append("\n[EzPressLatency.firstPress] n=").Append(breakdownCount)
              .Append(" 段序=bookkeeping/route/select/apply/capture 单位ms 列码=c<列号>"
                      + " [route±]=本列是否路由输入 [R]=路由到目标 [J]=已判定 e<前>><后>=车道条目数");

            for (int i = 0; i < breakdownCount; i++)
            {
                var b = breakdown[i];

                sb.Append("\n[EzPressLatency.firstPress] c").Append(b.Column)
                  .Append(" route").Append(b.RoutedInput ? '+' : '-')
                  .Append(' ').Append(b.Routed ? 'R' : '-')
                  .Append(b.Judged ? 'J' : '-')
                  .Append(" e").Append(b.EntriesBefore).Append('>').Append(b.EntriesAfter)
                  .Append(" | ").Append(b.BookkeepingMs.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(' ').Append(b.RouteMs.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(' ').Append(b.SelectMs.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(' ').Append(b.ApplyMs.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(' ').Append(b.CaptureMs.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(" | total ").Append(b.TotalMs.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(" | wall ").Append(b.WallMs.ToString("F1", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        /// <summary>同一帧内处理的按键数（按 FrameId 连续段统计）。</summary>
        private static int[] computeFrameLengths(int start, int sampleCount)
        {
            int[] lengths = new int[sampleCount];
            int i = 0;

            while (i < sampleCount)
            {
                long id = samples[(start + i) % capacity].FrameId;
                int j = i;

                while (j < sampleCount && samples[(start + j) % capacity].FrameId == id)
                    j++;

                for (int k = i; k < j; k++)
                    lengths[k] = j - i;

                i = j;
            }

            return lengths;
        }

        private static string formatSummary(int start, int sampleCount, int[] frameLengths)
        {
            if (sampleCount == 0)
                return "[EzPressLatency] no samples";

            double preSum = 0, columnSum = 0, totalSum = 0, frameAgeSum = 0;
            double preMax = 0, columnMax = 0, totalMax = 0, frameAgeMax = 0;
            double unroutedColumnSum = 0, routedColumnSum = 0;
            int unrouted = 0, routed = 0, judged = 0, multiPass = 0, maxPressesInFrame = 1;

            for (int i = 0; i < sampleCount; i++)
            {
                var s = samples[(start + i) % capacity];

                if (!double.IsNaN(s.PreColumnMs))
                    preSum += s.PreColumnMs;

                columnSum += s.ColumnMs;
                totalSum += double.IsNaN(s.TotalMs) ? s.ColumnMs : s.TotalMs;
                frameAgeSum += s.FrameAgeMs;

                preMax = Math.Max(preMax, s.PreColumnMs);
                columnMax = Math.Max(columnMax, s.ColumnMs);
                totalMax = Math.Max(totalMax, s.TotalMs);
                frameAgeMax = Math.Max(frameAgeMax, s.FrameAgeMs);

                if (s.Routed)
                {
                    routed++;
                    routedColumnSum += s.ColumnMs;
                }
                else
                {
                    unrouted++;
                    unroutedColumnSum += s.ColumnMs;
                }

                if (s.Judged)
                    judged++;

                if (s.FscIterations > 1)
                    multiPass++;

                maxPressesInFrame = Math.Max(maxPressesInFrame, frameLengths[i]);
            }

            double n = sampleCount;

            return string.Create(CultureInfo.InvariantCulture,
                $"[EzPressLatency] n={sampleCount} overwritten={Overwritten} routed={routed} judged={judged} "
                + $"multiPassFrames={multiPass} maxPressesInFrame={maxPressesInFrame} "
                + $"PreColumn avg/max={preSum / n:F3}/{preMax:F3} "
                + $"Column avg/max={columnSum / n:F3}/{columnMax:F3} "
                + $"Total avg/max={totalSum / n:F3}/{totalMax:F3} "
                + $"FrameAge avg/max={frameAgeSum / n:F3}/{frameAgeMax:F3} "
                + $"Column(routed) avg={(routed == 0 ? 0 : routedColumnSum / routed):F3} "
                + $"Column(unrouted) avg={(unrouted == 0 ? 0 : unroutedColumnSum / unrouted):F3}");
        }
    }
}
