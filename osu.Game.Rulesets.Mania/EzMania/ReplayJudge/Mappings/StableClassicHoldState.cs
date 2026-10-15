// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    /// <summary>
    /// osu!stable classic 长条持有态：整条 LN 只出一个判定，尾判需要稳定的「首次按下 / 首次有效松开」与断连标志。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Ez2AcHoldState"/> 同为 per-LN 状态，但语义不同：
    /// <list type="bullet">
    /// <item>不驱动 tick（stable classic 长条没有 tick）；</item>
    /// <item>不可恢复——<see cref="HasHoldBreak"/> 一旦置位，终档永久封到 Good200；</item>
    /// <item>头档不参与（stable <c>StartWasHit</c> 的封顶只存在于 ScoreV2 分支）。</item>
    /// </list>
    /// </remarks>
    public sealed class StableClassicHoldState
    {
        /// <summary>持有期连击增量的节拍（stable <c>JudgementMania.ComboIntv</c>，毫秒）。</summary>
        public const double HOLD_COMBO_INTERVAL = 100;

        /// <summary>本段起点（stable <c>StartTime</c>）。</summary>
        public double StartTime { get; }

        /// <summary>本段终点（stable <c>EndTime</c>）。</summary>
        public double EndTime { get; }

        /// <summary>首次按下时刻（stable <c>TimePress</c>）；从未按下为 <c>null</c>。</summary>
        public double? TimePress { get; private set; }

        /// <summary>
        /// 首次「不早于 <c>EndTime - HitWindow50</c>」的松开时刻（stable <c>TimeRelease</c>）；
        /// 更早的松手只记 <see cref="HasHoldBreak"/>，不写本值。从未有效松开为 <c>null</c>。
        /// </summary>
        public double? TimeRelease { get; private set; }

        /// <summary>中途断连（stable <c>hasHoldBreak</c>）：终档封到 Good200，不可恢复。</summary>
        public bool HasHoldBreak { get; private set; }

        /// <summary>此刻是否按住（stable <c>Pressed</c>），决定判定时取当前时刻还是用 <see cref="TimeRelease"/>。</summary>
        public bool Pressed { get; private set; }

        /// <summary>
        /// 持有期连击增量的上一拍时刻（stable <c>HitCircleManiaLong.lastScoreTime</c>），
        /// 时钟与 <see cref="StartTime"/> / <see cref="EndTime"/> 相同。
        /// </summary>
        private double holdComboBaseline;

        public StableClassicHoldState(double startTime, double endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }

        /// <summary>按下：只有首次按下写 <see cref="TimePress"/>（stable <c>TimePress</c> 不在重按时刷新）。</summary>
        public void OnPressed(double time)
        {
            Pressed = true;
            TimePress ??= time;

            // stable 在按下（含重按）把 lastScoreTime 设成当前时刻，节拍锚点因此随重按重排。
            holdComboBaseline = time;
        }

        /// <summary>
        /// 帧钟推进一步：仍按住、帧时刻落在 <c>[StartTime, EndTime]</c> 内、且距上一拍已满
        /// <see cref="HOLD_COMBO_INTERVAL"/> 时产出一枚连击增量。
        /// </summary>
        /// <remarks>
        /// 节拍锚在基线而不是帧时刻（每拍 <c>+100</c>），帧抖动不会让增量随时间漂移；
        /// 单帧最多产出一枚，滞后由后续帧补齐。是否仍按住由调用方门禁。
        /// </remarks>
        public bool TryAdvanceHoldComboTick(double frameTime)
        {
            if (!Pressed || frameTime < StartTime || frameTime > EndTime)
                return false;

            if (frameTime - holdComboBaseline < HOLD_COMBO_INTERVAL)
                return false;

            holdComboBaseline += HOLD_COMBO_INTERVAL;
            return true;
        }

        /// <summary>
        /// 松手。只有不早于 <c>EndTime - <paramref name="earlyReleaseWindow"/></c> 的松手才算有效松开并写
        /// <see cref="TimeRelease"/>；更早的松手只标 <see cref="HasHoldBreak"/>。
        /// </summary>
        public void OnReleased(double time, double earlyReleaseWindow)
        {
            Pressed = false;

            if (time >= EndTime - earlyReleaseWindow)
                TimeRelease ??= time;
            else
                HasHoldBreak = true;
        }
    }
}
