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
    /// 总开关 <see cref="Ez2Setting.EzJudgmentDiagEnabled"/> 决定整个套件是否启动，<c>EzDiagProbe*</c> 子项决定跑哪些。
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

        /// <summary>读配置并下发到所有探针。只应在启动时调用一次。</summary>
        public static void Apply(Ez2ConfigManager config)
        {
            bool suite = config.Get<bool>(Ez2Setting.EzJudgmentDiagEnabled);

            EzJudgmentDiagnostics.SetEnabled(suite && config.Get<bool>(Ez2Setting.EzDiagProbeJudgment));
            EzPressLatencyDiagnostics.SetEnabled(suite && config.Get<bool>(Ez2Setting.EzDiagProbePress));
            EzFrameStallDiagnostics.SetEnabled(suite && config.Get<bool>(Ez2Setting.EzDiagProbeFrame));
            EzTimingTrace.SetEnabled(suite && config.Get<bool>(Ez2Setting.EzDiagProbeTimingTrace));
            JudgeHotPathTrace = suite && config.Get<bool>(Ez2Setting.EzDiagProbeJudgeHotPath);

            FrameLoopAttribution = EzFrameStallDiagnostics.Enabled || EzPressLatencyDiagnostics.Enabled;

            applyPressTuning();
            applyFrameTuning();
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
