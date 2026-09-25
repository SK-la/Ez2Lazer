// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace osu.Game.EzOsuGame.Diagnostics
{
    /// <summary>
    /// 判定诊断：时钟漂移 CSV，并含按键→判定检查耗时（InputToJudgeMs）。
    /// 音频闭环（In→Play→Acou）见 <c>InputAudioLatencyTracker</c>，不在此采集。
    /// </summary>
    public static class EzJudgmentDiagnostics
    {
        /// <summary>
        /// 是否启用采集（运行时可切换）。
        /// </summary>
        public static bool Enabled { get; set; }

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
        /// 记录一次判定的完整时序上下文。
        /// </summary>
        /// <param name="inputToJudgeMs">按键 → 本检查点的 wall 耗时（ms）；未知传 <see cref="double.NaN"/>。</param>
        public static void Record(
            double gameTime,
            double noteStartTime,
            double timeOffset,
            double interpDrift,
            double frameElapsed,
            double inputToJudgeMs = double.NaN)
        {
            if (!Enabled) return;
            if (samples.Count >= max_samples) return;

            samples.Enqueue(new JudgmentSample(
                EzProbeOutput.WallClockMs,
                gameTime,
                noteStartTime,
                timeOffset,
                interpDrift,
                frameElapsed,
                inputToJudgeMs));
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
