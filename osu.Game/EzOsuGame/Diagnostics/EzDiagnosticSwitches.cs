// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Diagnostics
{
    /// <summary>
    /// 诊断探针开关的唯一写入口：开机时读一次配置与环境变量，把结果下发到各探针，之后不再变化。
    /// </summary>
    /// <remarks>
    /// 必须是「启动时定死」而不是运行期 bindable：探针关闭时热路径上只允许剩下一次静态 bool 读取，
    /// 只有进程级不变的值才让 JIT 有把整段采集消掉的可能（见 <c>docs/EZ-PERFORMANCE.md</c> §2.4.18）。
    /// 总开关 <see cref="Ez2Setting.EzJudgmentDiagEnabled"/>（设置项）决定启动时整套诊断是否工作；
    /// 跑哪些内容由 <c>EZ_DIAG_PROBES</c>（环境变量）选择，不进持久化设置。
    /// </remarks>
    public static class EzDiagnosticSwitches
    {
        /// <summary>
        /// Mania 判定热路径计数器是否采集。该探针在 Mania 程序集，<c>osu.Game</c> 无法直接写它的字段，
        /// 所以由这里持有、探针只读。
        /// </summary>
        public static bool JudgeHotPathTrace { get; private set; }

        /// <summary>
        /// FSC 每轮 <c>UpdateSubTree</c> 是否需要把归因数据（子树/时钟耗时、分配量、趟数）回报给帧探针。
        /// 帧探针要归因与明细，按键探针要趟数，所以任一开启即需要；两者都关时 FSC 热路径上只剩一次静态 bool 分支。
        /// </summary>
        public static bool FrameLoopAttribution { get; private set; }

        /// <summary>
        /// 内容选择环境变量。逗号 / 分号 / 空格分隔，取值：<c>judgment</c>、<c>press</c>、<c>frame</c>、
        /// <c>hotpath</c>、<c>trace</c>、<c>all</c>；空 / 未设置 = 全部。
        /// <para>
        /// 之所以是环境变量而不是设置项：它回答的是「这次实验想验什么」，不是用户偏好，
        /// 放持久化设置里只会把设置面板变成实验台。解析失败直接抛异常 ——
        /// 静默降级会让一整局采集白跑，而这正是本套件最贵的错误。
        /// </para>
        /// </summary>
        private const string probe_selection_env = "EZ_DIAG_PROBES";

        /// <summary>读总开关与内容选择并下发到所有探针。只应在启动时调用一次。</summary>
        public static void Apply(Ez2ConfigManager config)
        {
            // 总开关决定启动时整套诊断是否工作；子项（内容选择）决定跑哪些。
            bool suite = config.Get<bool>(Ez2Setting.EzJudgmentDiagEnabled);
            var probes = resolveProbeSelection();

            EzJudgmentDiagnostics.SetEnabled(suite && probes.Judgment);
            EzPressLatencyDiagnostics.SetEnabled(suite && probes.Press);
            EzFrameStallDiagnostics.SetEnabled(suite && probes.Frame);
            EzTimingTrace.SetEnabled(suite && probes.Trace);
            JudgeHotPathTrace = suite && probes.JudgeHotPath;

            FrameLoopAttribution = EzFrameStallDiagnostics.Enabled || EzPressLatencyDiagnostics.Enabled;

            applyPressTuning();
            applyFrameTuning();
        }

        /// <summary>解析 <see cref="probe_selection_env"/>；未设置时全部开启。</summary>
        private static (bool Judgment, bool Press, bool Frame, bool JudgeHotPath, bool Trace) resolveProbeSelection()
        {
            string? raw = Environment.GetEnvironmentVariable(probe_selection_env);

            if (string.IsNullOrWhiteSpace(raw))
                return (true, true, true, true, true);

            bool judgment = false, press = false, frame = false, judgeHotPath = false, trace = false;

            foreach (string rawToken in raw.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                switch (rawToken.Trim().ToLowerInvariant())
                {
                    case "judgment":
                        judgment = true;
                        break;

                    case "press":
                        press = true;
                        break;

                    case "frame":
                        frame = true;
                        break;

                    case "hotpath":
                        judgeHotPath = true;
                        break;

                    case "trace":
                        trace = true;
                        break;

                    case "all":
                        judgment = press = frame = judgeHotPath = trace = true;
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"{probe_selection_env} 无法识别的子项「{rawToken}」。"
                            + "可用：judgment, press, frame, hotpath, trace, all（空 = 全部）。"
                            + " 宁可现在启动失败，也不要静默降级后白跑一整局采集。");
                }
            }

            return (judgment, press, frame, judgeHotPath, trace);
        }

        /// <summary>
        /// 消融与调参走环境变量，不进持久化设置：它们回答的是「这次实验想验证什么」，不是用户偏好，
        /// 放进去只会把设置面板变成实验台。同样只在启动时读一次。
        /// </summary>
        private static void applyPressTuning()
        {
            EzPressLatencyDiagnostics.SkipForceMiss =
                Environment.GetEnvironmentVariable("EZ_PRESS_PROBE_SKIP_FORCE_MISS") == "1";
        }

        private static void applyFrameTuning()
        {
            // 0 = 抓全集（供帧节奏 / 平滑度分析）；想看「接近一帧」的次尖峰就调低（如 0.8）。
            if (double.TryParse(
                    Environment.GetEnvironmentVariable("EZ_FRAME_PROBE_MS"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double threshold)
                && threshold >= 0)
            {
                EzFrameStallDiagnostics.ThresholdMs = threshold;
            }

            // light 模式：只留直方图，跳过每帧的 GC/分配读数。
            EzFrameStallDiagnostics.Deep =
                Environment.GetEnvironmentVariable("EZ_FRAME_PROBE_LIGHT") != "1";
        }
    }
}
