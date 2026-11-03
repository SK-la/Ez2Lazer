// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Development;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Startup metrics. Search runtime logs for <c>[EzStartupTrace]</c>.
    /// Each line is prefixed with <c>+elapsed ms</c> from <see cref="MarkProcessStart"/>.
    ///
    /// Two entry points:
    /// <list type="bullet">
    /// <item><see cref="LogDiagnostic"/> — fine-grained investigation lines. Debug builds only; nothing is written in a release build.</item>
    /// <item><see cref="Log"/> — high-level milestones that stay useful in a release build. Gated by <see cref="EzStartupTuning.EnableStartupTrace"/> (default off).</item>
    /// </list>
    /// </summary>
    public static class EzStartupTrace
    {
        /// <summary>缓冲上限：日志存储就绪前只留这么多行，避免存储始终不就绪（例如测试宿主）时无限增长。</summary>
        private const int pending_capacity = 64;

        /// <summary>
        /// 强制打开启动追踪的环境变量。release 构建里 <see cref="EzStartupTuning.EnableStartupTrace"/> 默认关闭，
        /// 靠它才能在不改源码的前提下、对发布产物取到分段日志（测量脚本与现场排障都用它）。
        /// </summary>
        private const string force_trace_env = "EZ2LAZER_STARTUP_TRACE";

        private static readonly bool force_trace = Environment.GetEnvironmentVariable(force_trace_env) == "1";

        private static readonly Lock pendingLock = new Lock();
        private static readonly List<string> pendingLines = new List<string>();

        /// <summary>日志存储就绪后置位，之后的写入直接落盘、不再走缓冲。</summary>
        private static volatile bool storageReady;

        /// <summary>首次触达本类型的时间；未显式标注进程启动时用它兜底，避免出现负数时间轴。</summary>
        private static readonly long fallbackEpochTicks = Environment.TickCount64;

        /// <summary>进程启动时刻的 tick 基准，0 表示尚未标注。</summary>
        private static long processStartTicks;

        /// <summary>
        /// 标注进程启动时刻，作为 <c>+Nms</c> 的零点。
        /// </summary>
        /// <remarks>
        /// 必须尽早调用（<c>Program.Main</c> 第一条语句）：本类型的静态初始化不在进程启动时发生，
        /// 标注晚于入口就会把「入口之前那段启动」漏在零点之后，各次启动之间无法横向比较。
        /// </remarks>
        public static void MarkProcessStart() => processStartTicks = Environment.TickCount64;

        public static long ElapsedMilliseconds => Environment.TickCount64 - (processStartTicks != 0 ? processStartTicks : fallbackEpochTicks);

        /// <summary>
        /// High-level startup milestones (BDSP, song-select preload, settings ready, effective GC mode, enter timings).
        /// Runs in a release build when <see cref="EzStartupTuning.EnableStartupTrace"/> is enabled or
        /// <c>EZ2LAZER_STARTUP_TRACE=1</c> is set.
        /// </summary>
        public static void Log(string message)
        {
            if (!force_trace && !EzStartupTuning.EnableStartupTrace)
                return;

            write(message);
        }

        /// <summary>
        /// Fine-grained investigation lines (per-section timings, frame/GC sampling, sweep timings).
        /// Debug builds only; never written in a release build.
        /// </summary>
        public static void LogDiagnostic(string message)
        {
            if (!DebugUtils.IsDebugBuild)
                return;

            write(message);
        }

        private static void write(string message)
        {
            string line = $"[EzStartupTrace] +{ElapsedMilliseconds}ms {message}";

            if (storageReady)
            {
                Logger.Log(line, Ez2ConfigManager.LOGGER_NAME);
                return;
            }

            lock (pendingLock)
            {
                // 入口阶段的日志早于 GameHost.Run 里的 Logger.Storage 赋值，此时 Logger 会静默丢弃；
                // 先攒着，等存储就绪后补写，进程启动到「首条已落盘日志」之间才不会有空洞。
                if (Logger.Storage == null)
                {
                    if (pendingLines.Count < pending_capacity)
                        pendingLines.Add(line);

                    return;
                }

                storageReady = true;

                foreach (string pending in pendingLines)
                    Logger.Log(pending, Ez2ConfigManager.LOGGER_NAME);

                pendingLines.Clear();
            }

            Logger.Log(line, Ez2ConfigManager.LOGGER_NAME);
        }
    }
}
