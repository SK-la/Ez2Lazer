// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    /// <summary>
    /// osu!stable classic 长条持有态：整条 LN 只出一个判定，尾判需要稳定的「最近按下 / 首次有效松开」与断连标志。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Ez2AcHoldState"/> 同为 per-LN 状态，但语义不同：
    /// <list type="bullet">
    /// <item>不驱动 tick（stable classic 长条没有 tick）；</item>
    /// <item>不可恢复——<see cref="HasHoldBreak"/> 一旦置位，终档永久封到 Good200；</item>
    /// <item>头档不参与封顶。</item>
    /// </list>
    /// </remarks>
    public sealed class StableClassicHoldState
    {
        /// <summary>持有期连击增量的节拍（100ms）。</summary>
        public const double HOLD_COMBO_INTERVAL = 100;

        /// <summary>本段起点（<c>StartTime</c>）。</summary>
        public double StartTime { get; private set; }

        /// <summary>本段终点（<c>EndTime</c>）。</summary>
        public double EndTime { get; private set; }

        /// <summary>
        /// 最近一次按下时刻（<c>TimePress</c>），每次「由松到按」的上升沿覆盖；从未按下为 <c>null</c>。
        /// </summary>
        public double? TimePress { get; private set; }

        /// <summary>
        /// 首次「不早于 <c>EndTime - HitWindow50</c>」的松开时刻（<c>TimeRelease</c>）；
        /// 更早的松手只记 <see cref="HasHoldBreak"/>，不写本值。从未有效松开为 <c>null</c>。
        /// </summary>
        public double? TimeRelease { get; private set; }

        /// <summary>中途断连：终档封到 Good200，不可恢复。</summary>
        public bool HasHoldBreak { get; private set; }

        /// <summary>
        /// 标记中途断连。由「该按下的时候没按」触发，不写 <see cref="TimePress"/> / <see cref="TimeRelease"/>。
        /// </summary>
        public void MarkHoldBreak() => HasHoldBreak = true;

        /// <summary>此刻是否按住（<c>Pressed</c>），决定判定时取当前时刻还是用 <see cref="TimeRelease"/>。</summary>
        public bool Pressed { get; private set; }

        /// <summary>
        /// 持有期连击增量的上一拍时刻，时钟与 <see cref="StartTime"/> / <see cref="EndTime"/> 相同。
        /// </summary>
        private double holdComboBaseline;

        /// <summary>本持有态当前所属的谱面条目；<c>null</c> 表示尚未换绑（见 <see cref="EnsureBoundTo"/>）。</summary>
        private HitObjectLifetimeEntry? boundEntry;

        public StableClassicHoldState(double startTime, double endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }

        /// <summary>
        /// 把持有态绑定到 <paramref name="entry"/> 这条 LN 上；换了条目就清空上一段的按下 / 松开 / 断连。
        /// </summary>
        /// <remarks>
        /// 持有态按 <c>DrawableHoldNote</c> 实例缓存，而该实例由 <c>Column.RegisterPool</c> 池化复用，
        /// 会顺序承载多条 LN。不换绑就会把上一条 LN 的 <see cref="TimePress"/> / <see cref="HasHoldBreak"/>
        /// 带进下一条（该实例的 <c>HitObject</c> 已换，但状态还在），整段由此判错档。
        /// </remarks>
        public void EnsureBoundTo(HitObjectLifetimeEntry entry, double startTime, double endTime)
        {
            if (ReferenceEquals(boundEntry, entry))
                return;

            boundEntry = entry;
            StartTime = startTime;
            EndTime = endTime;

            TimePress = null;
            TimeRelease = null;
            HasHoldBreak = false;
            Pressed = false;
            holdComboBaseline = 0;
        }

        /// <summary>按下：每次「由松到按」的上升沿都刷新 <see cref="TimePress"/>（按下分支只在未按住时进入）。</summary>
        public void OnPressed(double time)
        {
            if (Pressed)
                return;

            Pressed = true;
            TimePress = time;

            // 按下（含重按）把节拍锚点设成当前时刻，增量节拍因此随重按重排。
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
