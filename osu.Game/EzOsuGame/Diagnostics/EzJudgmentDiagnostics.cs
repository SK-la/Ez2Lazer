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

            return EzProbeOutput.WriteAsync("judgment", sb.ToString(), sampleCount, "EzJudgmentDiag");
        }

        /// <summary>
        /// 丢弃所有待处理样本。
        /// </summary>
        public static void Clear()
        {
            // Atomically replace the queue to avoid long-running dequeue loops on the caller thread.
            Interlocked.Exchange(ref samples, new ConcurrentQueue<JudgmentSample>());
        }
    }
}
