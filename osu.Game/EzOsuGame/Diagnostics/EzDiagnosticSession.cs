// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;

namespace osu.Game.EzOsuGame.Diagnostics
{
    /// <summary>
    /// 一局游戏内所有诊断探针的生命周期：开局清残留，局末落盘。
    /// </summary>
    public static class EzDiagnosticSession
    {
        /// <summary>
        /// 开局：清掉上一局的残留样本。应在判定开始产生数据之前调用。
        /// </summary>
        /// <param name="startLabel">写进时序追踪的开场事实（如谱面名）。</param>
        public static void Begin(string startLabel)
        {
            if (EzJudgmentDiagnostics.Enabled)
                EzJudgmentDiagnostics.Clear();

            if (EzTimingTrace.Enabled)
            {
                EzTimingTrace.Clear();
                EzTimingTrace.Record("GameplayStarted", startLabel);
            }
        }

        /// <summary>
        /// 局末：先把结束事件写进时序追踪，再把各探针落盘。
        /// </summary>
        /// <param name="exitLabel">写进时序追踪的收场事实（如 HasPassed / HasFailed）。</param>
        public static void End(string exitLabel)
        {
            if (EzTimingTrace.Enabled)
                EzTimingTrace.Record("GameplayExited", exitLabel);

            _ = Task.Run(flushAll);
        }

        private static void flushAll()
        {
            if (EzJudgmentDiagnostics.Enabled)
            {
                EzJudgmentDiagnostics.Flush();
                EzJudgmentDiagnostics.Clear();
            }

            if (EzTimingTrace.Enabled)
            {
                EzTimingTrace.Flush();
                EzTimingTrace.Clear();
            }
        }
    }
}