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
        /// <summary>是否采集。进局前由 <see cref="EzDiagnosticSwitches.Enabled"/> 赋值时写一次；关闭时热路径上只剩一次静态 bool 读取。</summary>
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
        /// 记录一次判定的完整时序上下文。
        /// </summary>
        /// <param name="keyTs">触发本次判定的按键的 wall 戳（<see cref="Stopwatch.GetTimestamp"/> 基准）；无按键传 0。</param>
        public static void Capture(DrawableHitObject hitObject, double timeOffset, long keyTs)
        {
            if (!Enabled) return;
            if (samples.Count >= max_samples) return;

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
            sb.AppendLine("WallMs,GameTime,NoteStart,TimeOffset,Drift,FrameElapsed,InputToJudgeMs");

            int sampleCount = 0;

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
            Interlocked.Exchange(ref samples, new ConcurrentQueue<JudgmentSample>());

            chainRecorded = false;
            chain = default;
            tEnter = tCheckStart = tCheckEnd = tApplyStart = tApplyEnd = 0;
        }
    }
}