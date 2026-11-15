// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Diagnostics
{
    /// <summary>
    /// 诊断探针开关的唯一写入口：<c>Player</c> 每次进局前给 <see cref="Enabled"/> 赋一次值，
    /// 赋值即把总开关连同内容选择一起下发，本局内不再变化。
    /// </summary>
    /// <remarks>
    /// 必须是「进局前定死」而不是运行期 bindable：探针关闭时热路径上只允许剩下一次静态 bool 读取。
    /// 总开关 <see cref="Ez2Setting.EzJudgmentDiagEnabled"/>（设置项）决定整套诊断是否工作；
    /// 跑哪些内容由 <c>EZ_DIAG_PROBES</c>（环境变量）选择，不进持久化设置。
    /// </remarks>
    public static class EzDiagnosticSwitches
    {
        private static bool enabled;

        /// <summary>
        /// 总开关，取值来自 <see cref="Ez2Setting.EzJudgmentDiagEnabled"/>。
        /// setter 会一并读内容选择并下发到各探针 —— 写这一处就等于定下本局的诊断范围。
        /// </summary>
        public static bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                applySelection();
            }
        }

        /// <summary>
        /// Mania 判定热路径计数器是否采集。该探针在 Mania 程序集，<c>osu.Game</c> 无法直接写它的字段，
        /// 所以由这里持有、探针只读。
        /// </summary>
        public static bool JudgeHotPathTrace { get; private set; }

        /// <summary>
        /// 内容选择环境变量。逗号 / 分号 / 空格分隔，取值：<c>judgment</c>、<c>hotpath</c>、<c>all</c>；
        /// <c>press</c> / <c>frame</c> 可写在列表里但当前未接线（预留与 mania-input-press 分支对齐）。
        /// 空 / 未设置 = judgment + hotpath 全开。
        /// </summary>
        private const string probe_selection_env = "EZ_DIAG_PROBES";

        private static void applySelection()
        {
            var probes = resolveProbeSelection();

            EzJudgmentDiagnostics.SetEnabled(enabled && probes.Judgment);
            JudgeHotPathTrace = enabled && probes.JudgeHotPath;
        }

        private static (bool Judgment, bool JudgeHotPath) resolveProbeSelection()
        {
            string? raw = Environment.GetEnvironmentVariable(probe_selection_env);

            if (string.IsNullOrWhiteSpace(raw))
                return (true, true);

            bool judgment = false;
            bool judgeHotPath = false;

            foreach (string rawToken in raw.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                switch (rawToken.Trim().ToLowerInvariant())
                {
                    case "judgment":
                        judgment = true;
                        break;

                    case "hotpath":
                        judgeHotPath = true;
                        break;

                    case "press":
                    case "frame":
                        break;

                    case "all":
                        judgment = judgeHotPath = true;
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"{probe_selection_env} 无法识别的子项「{rawToken}」。"
                            + "可用：judgment, hotpath, press, frame, all（空 = judgment+hotpath）。"
                            + " 宁可现在进局失败，也不要静默降级后白跑一整局采集。");
                }
            }

            return (judgment, judgeHotPath);
        }
    }
}
