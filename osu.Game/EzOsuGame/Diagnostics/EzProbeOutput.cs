// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Diagnostics
{
    /// <summary>
    /// Ez 诊断探针的共用落盘设施。探针只负责自己的样本结构与摘要文本；时钟、输出目录、
    /// CSV 数值格式与后台写盘全部由这里提供，不再各自复制一份。
    /// </summary>
    /// <remarks>
    /// 时钟必须是全探针共用的同一个实例：按键 / 判定 / 帧三份 CSV 靠各自的 <c>WallMs</c> 列跨文件对齐，
    /// 某个探针自带一份 <see cref="Stopwatch"/> 会让这个对齐失去参照。
    /// </remarks>
    public static class EzProbeOutput
    {
        private static readonly Stopwatch wallclock = Stopwatch.StartNew();
        private static readonly Lazy<string> directory_path = new Lazy<string>(resolveDirectory);

        /// <summary>与所有探针 CSV 同源的单调 wall 时钟（ms）。</summary>
        public static double WallClockMs => wallclock.Elapsed.TotalMilliseconds;

        /// <summary>诊断文件的输出目录（仓库内 <c>diagnostics/</c>；找不到仓库则退回当前目录或桌面 <c>EzDiag</c>）。</summary>
        public static string Directory => directory_path.Value;

        /// <summary>CSV 数值一律用不变区域，避免逗号小数分隔符的地区写出畸形列。</summary>
        public static string Csv(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

        /// <summary>同 <see cref="Csv"/>，但 <see cref="double.NaN"/> 写空串，表示该项本次无值。</summary>
        public static string CsvOrEmpty(double value) => double.IsNaN(value) ? string.Empty : Csv(value);

        /// <summary>CSV 字符串字段转义（含逗号 / 引号 / 换行时加引号）。</summary>
        public static string CsvEscape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return '"' + value.Replace("\"", "\"\"") + '"';

            return value;
        }

        /// <summary>
        /// 把 <paramref name="csv"/> 写到 <c>&lt;prefix&gt;_&lt;stamp&gt;.csv</c>；<paramref name="summary"/> 非 null 时
        /// 另写一份同尾缀的 <c>.summary.txt</c>，并返回 CSV 路径。
        /// 磁盘 IO 在后台线程执行，失败只记日志、不抛给调用方。
        /// </summary>
        public static string WriteAsync(string prefix, string csv, int sampleCount, string logTag, string? summary = null)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(Directory, $"{prefix}_{stamp}.csv");
            string? summaryPath = summary == null ? null : Path.Combine(Directory, $"{prefix}_{stamp}.summary.txt");
            string? summaryText = summary;

            _ = Task.Run(async () =>
            {
                try
                {
                    await File.WriteAllTextAsync(path, csv).ConfigureAwait(false);

                    if (summaryPath != null)
                        await File.WriteAllTextAsync(summaryPath, summaryText + Environment.NewLine).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    try { Logger.Log($"[{logTag}] flush failed: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, level: LogLevel.Error); }
                    catch { }

                    return;
                }

                try
                {
                    Logger.Log($"[{logTag}] flushed {sampleCount} samples to {path}", Ez2ConfigManager.LOGGER_NAME);

                    // 摘要本身也进日志：分析脚本找不到 .summary.txt 时（换机器、只留日志）还有退路。
                    if (summaryText != null)
                        Logger.Log(summaryText, Ez2ConfigManager.LOGGER_NAME);
                }
                catch { }
            });

            return path;
        }

        private static string resolveDirectory()
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
                        System.IO.Directory.CreateDirectory(d);
                        return d;
                    }
                }
            }
            catch { }

            try
            {
                string d = Path.Combine(Environment.CurrentDirectory, "diagnostics");
                System.IO.Directory.CreateDirectory(d);
                return d;
            }
            catch { }

            string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "EzDiag");

            try { System.IO.Directory.CreateDirectory(fallback); }
            catch { }

            return fallback;
        }
    }
}
