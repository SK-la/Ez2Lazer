// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Diagnostics
{
    /// <summary>
    /// 判定诊断：时钟漂移 CSV，并含按键→判定检查耗时（InputToJudgeMs）。
    /// 音频闭环（In→Play→Acou）见 <c>InputAudioLatencyTracker</c>，不在此采集。
    /// </summary>
    public static class EzJudgmentDiagnostics
    {
        /// <summary>是否采集。启动时由 <see cref="EzDiagnosticSwitches.Apply"/> 写一次；关闭时热路径上只剩一次静态 bool 读取。</summary>
        public static bool Enabled { get; private set; }

        /// <summary>唯一的生产写入口；测试可直接调用。</summary>
        public static void SetEnabled(bool enabled) => Enabled = enabled;

        private static ConcurrentQueue<JudgmentSample> samples = new ConcurrentQueue<JudgmentSample>();

        /// <summary>采样上限，防止 OOM。</summary>
        private const int max_samples = 8000;

        /// <summary>按键 wall 戳到本条判定检查的耗时（ms）；无有效按键戳时为 NaN。</summary>
        public readonly record struct JudgmentSample(
            double WallMs,
            double GameTime,
            double NoteStartTime,
            double TimeOffset,
            double InterpolatedDrift,
            double FrameElapsed,
            double InputToJudgeMs);

        /// <summary>
        /// 本局首次「判定真正落地」那一次的内部构成（ms）。
        /// <para>
        /// 按键探针只能把一次命中拆到 <c>apply</c>（<c>UpdateResult</c> 全程），而首次命中那里有 ~13 ms。
        /// 本结构把它再切三块：<c>UpdatePrologueMs</c>（入口 → <c>CheckForResult</c> 前，含子帧修正与判定探针）、
        /// <c>CheckForResultMs</c>（含派生类判定的 <c>HitWindows</c> 计算，以及其内的 <c>ApplyResult</c>）、
        /// <c>ApplyResultMs</c>（结果落地与 <c>OnNewResult</c> 扇出）。
        /// </para>
        /// <para>
        /// 三者嵌套而非并列：<c>ApplyResult</c> 由派生类的 <c>CheckForResult</c> 调用，故
        /// <c>CheckForResultMs - ApplyResultMs</c> 才是判定计算自身的净耗时。
        /// </para>
        /// </summary>
        public readonly record struct JudgeChain(
            double WallMs,
            double UpdatePrologueMs,
            double CheckForResultMs,
            double ApplyResultMs);

        private static bool chainRecorded;
        private static long tEnter;
        private static long tCheckStart;
        private static long tCheckEnd;
        private static long tApplyStart;
        private static long tApplyEnd;

        /// <summary>首次判定链是否仍在等样本；关闭探针或已取到样本后恒为 false。</summary>
        public static bool ChainWanted => Enabled && !chainRecorded;

        /// <summary><c>UpdateResult</c> 入口。</summary>
        public static void ChainEnterUpdateResult()
        {
            if (!ChainWanted)
                return;

            tEnter = Stopwatch.GetTimestamp();
        }

        /// <summary><c>CheckForResult</c> 入口。</summary>
        public static void ChainEnterCheckForResult()
        {
            if (!ChainWanted || tEnter == 0)
                return;

            tCheckStart = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// <c>CheckForResult</c> 出口。<paramref name="judged"/> 为 false 表示这次按键没有落地判定，
        /// 本次链记录作废，等下一次真正落地的判定。
        /// </summary>
        public static void ChainExitCheckForResult(bool judged)
        {
            if (!ChainWanted || !judged)
                return;

            tCheckEnd = Stopwatch.GetTimestamp();
        }

        /// <summary><c>ApplyResult</c> 入口。</summary>
        public static void ChainEnterApplyResult()
        {
            if (!ChainWanted)
                return;

            if (tApplyStart == 0)
                tApplyStart = Stopwatch.GetTimestamp();
        }

        /// <summary><c>ApplyResult</c> 出口（<c>OnNewResult</c> 扇出之后）；在此提交整条链。</summary>
        public static void ChainExitApplyResult()
        {
            if (!ChainWanted)
                return;

            if (tApplyEnd == 0)
                tApplyEnd = Stopwatch.GetTimestamp();

            commitChain();
        }

        private static void commitChain()
        {
            if (tEnter == 0 || tCheckStart == 0 || tCheckEnd == 0 || tApplyStart == 0 || tApplyEnd == 0)
                return;

            double tickToMs = 1000.0 / Stopwatch.Frequency;

            chainRecorded = true;

            chain = new JudgeChain(
                EzProbeOutput.WallClockMs,
                (tCheckStart - tEnter) * tickToMs,
                (tCheckEnd - tCheckStart) * tickToMs,
                (tApplyEnd - tApplyStart) * tickToMs);
        }

        private static JudgeChain chain;

        private static string formatChain()
        {
            if (!chainRecorded)
                return "[EzJudgmentDiag.firstJudge] no sample";

            var c = chain;

            return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"[EzJudgmentDiag.firstJudge] wall={c.WallMs:F1} prologue={c.UpdatePrologueMs:F3} "
                + $"CheckForResult={c.CheckForResultMs:F3}(own={c.CheckForResultMs - c.ApplyResultMs:F3}) "
                + $"ApplyResult={c.ApplyResultMs:F3}");
        }

        /// <summary>
        /// 记录一次判定的完整时序上下文。派生量（插值漂移、按键 → 判定检查的 wall 耗时及其有效性）
        /// 都在这里算完，调用点只声明「用户刚触发了一次判定」。
        /// </summary>
        /// <param name="keyTs">触发本次判定的按键的 wall 戳（<see cref="Stopwatch.GetTimestamp"/> 基准）；无按键传 0。</param>
        public static void Capture(DrawableHitObject hitObject, double timeOffset, long keyTs)
        {
            if (!Enabled) return;
            if (samples.Count >= max_samples) return;

            // 插值漂移只有经由帧稳定时钟下的 gameplay 时钟才读得到；读不到就记 0（等价于「本帧没有漂移」）。
            double interpDrift = 0;

            if (hitObject.EzDrawableRuleset?.FrameStableClock is FrameStabilityContainer fsc
                && fsc.ParentGameplayClock is GameplayClockContainer gcc)
            {
                interpDrift = gcc.InterpolatedDrift;
            }

            samples.Enqueue(new JudgmentSample(
                EzProbeOutput.WallClockMs,
                hitObject.Time.Current,
                hitObject.HitObject.GetEndTime(),
                timeOffset,
                interpDrift,
                hitObject.Clock.ElapsedFrameTime,
                resolveInputToJudgeMs(keyTs)));
        }

        /// <summary>
        /// 按键 wall 戳到本次判定检查的耗时（ms）；没有有效按键戳时为 <see cref="double.NaN"/>。
        /// 超过 1s 的一律按陈旧戳 / 无关按键处理，同样记 NaN —— 这种值留在 CSV 里会把尾部分布整个拉开。
        /// </summary>
        private static double resolveInputToJudgeMs(long keyTs)
        {
            if (keyTs <= 0)
                return double.NaN;

            double ms = (Stopwatch.GetTimestamp() - keyTs) / (double)Stopwatch.Frequency * 1000.0;

            return ms is < 0 or > 1000 ? double.NaN : ms;
        }

        /// <summary>
        /// 将当前采样缓冲区写入 CSV 并清空，返回文件路径。
        /// </summary>
        public static string Flush()
        {
            var sb = new StringBuilder();
            // InterpClock / BassSource 两列已删（2026-09-25）：前者与 GameTime 是同一属性连读两次；
            // 后者恒等于 GameTime − Drift − 15.000。详见 docs/EZ-PERFORMANCE.md §2.4.19。
            sb.AppendLine("WallMs,GameTime,NoteStart,TimeOffset,Drift,FrameElapsed,InputToJudgeMs");

            int sampleCount = 0;

            // Drain current queue snapshot into the CSV builder.
            while (samples.TryDequeue(out var s))
            {
                sampleCount++;
                sb.Append(EzProbeOutput.Csv(s.WallMs)).Append(',');
                sb.Append(EzProbeOutput.Csv(s.GameTime)).Append(',');
                sb.Append(EzProbeOutput.Csv(s.NoteStartTime)).Append(',');
                sb.Append(EzProbeOutput.Csv(s.TimeOffset)).Append(',');
                sb.Append(EzProbeOutput.Csv(s.InterpolatedDrift)).Append(',');
                sb.Append(EzProbeOutput.Csv(s.FrameElapsed)).Append(',');
                sb.Append(EzProbeOutput.CsvOrEmpty(s.InputToJudgeMs));
                sb.AppendLine();
            }

            return EzProbeOutput.WriteAsync("judgment", sb.ToString(), sampleCount, "EzJudgmentDiag", formatChain());
        }

        /// <summary>
        /// 丢弃所有待处理样本。
        /// </summary>
        public static void Clear()
        {
            // Atomically replace the queue to avoid long-running dequeue loops on the caller thread.
            Interlocked.Exchange(ref samples, new ConcurrentQueue<JudgmentSample>());

            // 首次判定链按局清零：同进程第二局起即无一次性成本，需要一个干净的"局内首次"。
            chainRecorded = false;
            chain = default;
            tEnter = tCheckStart = tCheckEnd = tApplyStart = tApplyEnd = 0;
        }
    }
}
