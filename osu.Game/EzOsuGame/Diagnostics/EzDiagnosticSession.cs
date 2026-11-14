// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using osu.Framework.Audio.Wasapi;

namespace osu.Game.EzOsuGame.Diagnostics
{
    /// <summary>
    /// 一局游戏内所有诊断探针的生命周期：开局清残留，局末落盘。
    /// </summary>
    /// <remarks>
    /// 之所以需要这一层，是因为探针名单原来写在 <c>Player</c> 里 —— 每加一个探针都要去播放器里
    /// 改一处 <c>Enabled</c> 判断、一处 Clear、一处 Flush，职责完全错位。
    /// 现在 <c>Player</c> 只说「开一局」和「这局怎么结束的」，名单与时机归这里。
    /// </remarks>
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

            if (EzPressLatencyDiagnostics.Enabled)
                EzPressLatencyDiagnostics.Clear();

            if (EzFrameStallDiagnostics.Enabled)
            {
                EzFrameStallDiagnostics.Clear();

                // 帧摘要里的 wasapiRead 行靠它。只有帧探针开着才需要：关闭时这条热路径上只剩一个 bool 判断。
                WasapiReadStats.Reset();
                WasapiReadStats.Enabled = true;
            }

            if (EzTimingTrace.Enabled)
            {
                EzTimingTrace.Clear();
                EzTimingTrace.Record("GameplayStarted", startLabel);
            }
        }

        /// <summary>
        /// 局末：先把结束事件写进时序追踪，再把各探针落盘，并关掉局内才需要的 framework 侧统计。
        /// </summary>
        /// <param name="exitLabel">写进时序追踪的收场事实（如 HasPassed / HasFailed）。</param>
        public static void End(string exitLabel)
        {
            if (EzTimingTrace.Enabled)
                EzTimingTrace.Record("GameplayExited", exitLabel);

            // 帧探针的 CSV 可以到 20 万行，拼字符串本身就有几十毫秒。整段落盘移出调用线程：
            // 磁盘慢（杀软 / 网络盘）时这一步会直接卡住退出流程。
            _ = Task.Run(flushAll);
        }

        private static void flushAll()
        {
            if (EzJudgmentDiagnostics.Enabled)
            {
                EzJudgmentDiagnostics.Flush();
                EzJudgmentDiagnostics.Clear();
            }

            if (EzPressLatencyDiagnostics.Enabled)
            {
                EzPressLatencyDiagnostics.Flush();
                EzPressLatencyDiagnostics.Clear();
            }

            if (EzFrameStallDiagnostics.Enabled)
            {
                EzFrameStallDiagnostics.Flush();

                // 快照已在 Flush 内取走，这里可以把统计关掉了。
                WasapiReadStats.Enabled = false;
                EzFrameStallDiagnostics.Clear();
            }

            if (EzTimingTrace.Enabled)
            {
                EzTimingTrace.Flush();
                EzTimingTrace.Clear();
            }
        }
    }
}
