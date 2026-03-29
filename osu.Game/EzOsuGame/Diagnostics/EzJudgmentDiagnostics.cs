// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace osu.Game.EzOsuGame.Diagnostics
{
    /// <summary>
    /// 采集 mania 判定时序数据，用于分析"前-后-前-后"交替漂移问题。
    /// 线程安全、低开销。仅在 DEBUG 构建中启用文件写出。
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

        /// <summary>高精度计时器，提供微秒级别 wallclock。</summary>
        private static readonly Stopwatch wallclock = Stopwatch.StartNew();

        public readonly record struct JudgmentSample(
            double WallMs,
            double GameTime,
            double NoteStartTime,
            double TimeOffset,
            double InterpolatedClockTime,
            double BassSourceTime,
            double InterpolatedDrift,
            double FrameElapsed);

        /// <summary>
        /// 记录一次判定的完整时序上下文。
        /// </summary>
        public static void Record(
            double gameTime,
            double noteStartTime,
            double timeOffset,
            double interpClockTime,
            double bassSourceTime,
            double interpDrift,
            double frameElapsed)
        {
            if (!Enabled) return;
            if (samples.Count >= max_samples) return;

            samples.Enqueue(new JudgmentSample(
                wallclock.Elapsed.TotalMilliseconds,
                gameTime,
                noteStartTime,
                timeOffset,
                interpClockTime,
                bassSourceTime,
                interpDrift,
                frameElapsed));
        }

        /// <summary>
        /// 将当前采样缓冲区写入 CSV 并清空，返回文件路径。
        /// </summary>
        public static string Flush()
        {
            var sb = new StringBuilder();
            sb.AppendLine("WallMs,GameTime,NoteStart,TimeOffset,InterpClock,BassSource,Drift,FrameElapsed");

            // Drain current queue snapshot into the CSV builder.
            while (samples.TryDequeue(out var s))
            {
                sb.Append(s.WallMs.ToString("F3")).Append(',');
                sb.Append(s.GameTime.ToString("F3")).Append(',');
                sb.Append(s.NoteStartTime.ToString("F3")).Append(',');
                sb.Append(s.TimeOffset.ToString("F3")).Append(',');
                sb.Append(s.InterpolatedClockTime.ToString("F3")).Append(',');
                sb.Append(s.BassSourceTime.ToString("F3")).Append(',');
                sb.Append(s.InterpolatedDrift.ToString("F3")).Append(',');
                sb.Append(s.FrameElapsed.ToString("F3"));
                sb.AppendLine();
            }

            string dir = getDiagnosticsDirectory();
            string path = Path.Combine(dir, $"judgment_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            // Perform actual disk IO on a background thread to avoid blocking callers.
            try
            {
                string content = sb.ToString();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await File.WriteAllTextAsync(path, content).ConfigureAwait(false);
                        Logger.Log($"[EzJudgmentDiag] flushed to {path}");
                    }
                    catch (Exception ex)
                    {
                        try { Logger.Log($"[EzJudgmentDiag] flush failed: {ex.Message}", level: LogLevel.Error); }
                        catch { }
                    }
                });
            }
            catch { }

            return path;
        }

        /// <summary>
        /// 丢弃所有待处理样本。
        /// </summary>
        public static void Clear()
        {
            // Atomically replace the queue to avoid long-running dequeue loops on the caller thread.
            Interlocked.Exchange(ref samples, new ConcurrentQueue<JudgmentSample>());
        }

        private static string getDiagnosticsDirectory()
        {
            try
            {
                // Try to locate repository root by searching upwards for osu.sln or a .git folder.
                var di = new DirectoryInfo(AppContext.BaseDirectory);

                for (int i = 0; i < 8 && di != null; i++, di = di.Parent)
                {
                    if (di.GetFiles("osu.sln").Length > 0 || di.GetDirectories(".git").Length > 0)
                    {
                        string d = Path.Combine(di.FullName, "diagnostics");
                        Directory.CreateDirectory(d);
                        return d;
                    }
                }
            }
            catch { }

            try
            {
                string d = Path.Combine(Environment.CurrentDirectory, "diagnostics");
                Directory.CreateDirectory(d);
                return d;
            }
            catch { }

            string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "EzDiag");

            try { Directory.CreateDirectory(fallback); }
            catch { }

            return fallback;
        }
    }
}
