// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Input;
using osu.Game.EzOsuGame.Diagnostics;
using osu.Game.EzOsuGame.Timing;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.EzMania.Diagnostics
{
    /// <summary>
    /// 按键延迟探针的 Mania 适配层：把「有按键进了本列」这一个事实翻译成
    /// <see cref="EzPressLatencyDiagnostics.PressSample"/>。
    /// <para>
    /// 帧内偏移、本列工时、帧龄、按键序号、单位换算与 NaN 判定都在这里算完，<see cref="Column"/> 只声明事件本身。
    /// 之所以落在 Mania 侧：这些派生量要读列号、车道条目、按键来源与 FSC 帧号，而 <c>osu.Game</c> 里的探针
    /// 不能反向依赖规则集。开关关闭时本方法只在调用点留一次静态 bool 读取。
    /// </para>
    /// </summary>
    internal static class ManiaPressProbe
    {
        /// <summary>
        /// 采集一次按键。调用方只需给出事件本身与判定结果，其余读数与派生一律在此完成。
        /// </summary>
        /// <param name="forceMissScan">本次按键在 <c>handleHit</c> 中扫描的强制 miss 候选数（探针列）。</param>
        internal static void Capture(Column column, long pressEnterTs, double gameTime, bool routed, bool judged, int forceMissScan)
        {
            if (!EzPressLatencyDiagnostics.Enabled)
                return;

            long now = Stopwatch.GetTimestamp();
            double tickToMs = 1000.0 / Stopwatch.Frequency;

            long keyTs = InputManager.EzSubFrameTimestamp;
            long frameTs = EzSubFrameCorrection.LastUpdateTimestamp;
            long frameId = EzSubFrameCorrection.UpdateCount;

            double preColumnMs = keyTs > 0 ? (pressEnterTs - keyTs) * tickToMs : double.NaN;
            double columnMs = (now - pressEnterTs) * tickToMs;
            double frameAgeMs = frameTs > 0 ? (pressEnterTs - frameTs) * tickToMs : double.NaN;

            // 本帧处理的按键数、它们在本列花掉的总时长、以及首个按键进入本列的帧内偏移，
            // 供帧级 stall 探针把一次按键帧切成「帧起→本列 / 本列工时 / 本列之后」三段。
            EzFrameStallDiagnostics.NotifyPress(pressEnterTs, columnMs);

            EzPressLatencyDiagnostics.Record(new EzPressLatencyDiagnostics.PressSample(
                EzProbeOutput.WallClockMs,
                gameTime,
                double.IsNaN(preColumnMs) ? double.NaN : preColumnMs + columnMs,
                preColumnMs,
                columnMs,
                frameAgeMs,
                column.Index,
                frameId,
                EzPressLatencyDiagnostics.BeginPress(frameId),
                routed,
                judged,
                column.LaneController.Entries.Count,
                forceMissScan,
                column.Clock.ElapsedFrameTime,
                EzFrameStallDiagnostics.LastUpdateIterations,
                GC.CollectionCount(0),
                GC.CollectionCount(1),
                GC.GetTotalPauseDuration().TotalMilliseconds,
                ManiaLaneController.EarliestCacheHits,
                ManiaLaneController.EarliestCacheMisses));
        }
    }
}
