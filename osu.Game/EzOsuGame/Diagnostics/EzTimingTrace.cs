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
    /// 记录玩法生命周期关键节点的时序事件，用于诊断结算界面卡住、判定完成
    /// 状态翻转等问题。线程安全、低开销，数据在游戏退出时写出为 CSV。
    /// </summary>
    public static class EzTimingTrace
    {
        /// <summary>是否启用采集（运行时可切换）。</summary>
        public static bool Enabled { get; set; }

        private static readonly ConcurrentQueue<TraceEvent> events = new ConcurrentQueue<TraceEvent>();

        /// <summary>采样上限，防止 OOM。</summary>
        private const int max_events = 10_000;

        /// <summary>高精度计时器，提供微秒级别 wallclock。</summary>
        private static readonly Stopwatch wallclock = Stopwatch.StartNew();

        public readonly record struct TraceEvent(
            double WallMs,
            string Tag,
            string Extra);

        /// <summary>
        /// 记录一个命名事件。立即返回，不抛出异常。
        /// </summary>
        /// <param name="tag">事件分类标签，例如 "CheckScoreCompleted"。</param>
        /// <param name="extra">附加键值信息，例如 "hasCompleted=True waitMs=1234"。</param>
        public static void Record(string tag, string extra = "")
        {
            if (!Enabled) return;
            if (events.Count >= max_events) return;

            events.Enqueue(new TraceEvent(
                wallclock.Elapsed.TotalMilliseconds,
                tag ?? string.Empty,
                extra ?? string.Empty));
        }

        /// <summary>
        /// 将当前事件缓冲区写入 CSV 并清空，返回文件路径。
        /// 在失败时记录日志并返回空字符串。
        /// </summary>
        public static string Flush()
        {
            var sb = new StringBuilder();
            sb.AppendLine("WallMs,Tag,Extra");

            int count = 0;

            while (events.TryDequeue(out var e))
            {
                count++;
                sb.Append(e.WallMs.ToString("F3")).Append(',');
                sb.Append(CsvEscape(e.Tag)).Append(',');
                sb.AppendLine(CsvEscape(e.Extra));
            }

            string dir = GetDiagnosticsDirectory();
            string path = Path.Combine(dir, $"trace_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            try
            {
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                Logger.Log($"[EzTimingTrace] flushed {count} events to {path}");
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzTimingTrace] flush failed: {ex.Message}", level: LogLevel.Error);
                return string.Empty;
            }

            return path;
        }

        /// <summary>丢弃所有待处理事件。</summary>
        public static void Clear()
        {
            while (events.TryDequeue(out _)) { }
        }

        private static string GetDiagnosticsDirectory()
        {
            try
            {
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

        private static string CsvEscape(string s)
        {
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
                return '"' + s.Replace("\"", "\"\"") + '"';

            return s;
        }
    }
}
