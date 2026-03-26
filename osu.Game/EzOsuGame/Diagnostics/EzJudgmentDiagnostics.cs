// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
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

        private static readonly ConcurrentQueue<JudgmentSample> samples = new ConcurrentQueue<JudgmentSample>();

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

            string dir = GetDiagnosticsDirectory();
            string path = Path.Combine(dir, $"judgment_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            try
            {
                File.WriteAllText(path, sb.ToString());
                Logger.Log($"[EzJudgmentDiag] flushed to {path}");
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzJudgmentDiag] flush failed: {ex.Message}", level: LogLevel.Error);
                return string.Empty;
            }

            return path;
        }

        /// <summary>
        /// 丢弃所有待处理样本。
        /// </summary>
        public static void Clear()
        {
            while (samples.TryDequeue(out _)) { }
        }

        private static string GetDiagnosticsDirectory()
        {
            try
            {
                // Try to locate repository root by searching upwards for osu.sln or a .git folder.
                var di = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
                for (int i = 0; i < 8 && di != null; i++, di = di.Parent)
                {
                    if (di.GetFiles("osu.sln").Length > 0 || di.GetDirectories(".git").Length > 0)
                    {
                        var d = System.IO.Path.Combine(di.FullName, "diagnostics");
                        System.IO.Directory.CreateDirectory(d);
                        return d;
                    }
                }
            }
            catch { }

            try
            {
                var d = System.IO.Path.Combine(System.Environment.CurrentDirectory, "diagnostics");
                System.IO.Directory.CreateDirectory(d);
                return d;
            }
            catch { }

            var fallback = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "EzDiag");
            try { System.IO.Directory.CreateDirectory(fallback); } catch { }
            return fallback;
        }
    }
}
