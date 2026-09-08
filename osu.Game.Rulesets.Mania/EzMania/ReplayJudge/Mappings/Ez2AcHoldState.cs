// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    /// <summary>
    /// EZ2AC LN 持有态：tick 只问「已接上且未中途断开」+ 按住与否；不区分头 Kool/Good 档。
    /// </summary>
    public enum Ez2AcHoldPhase
    {
        Idle,

        /// <summary>已接上，按住时 tick 涨 combo。</summary>
        Holding,

        /// <summary>中途松手，再抓前 tick 不涨 combo。</summary>
        Broken,

        /// <summary>头 Fail / 从未接上，再抓前 tick 不涨 combo。</summary>
        Missing,
    }

    public sealed class Ez2AcHoldState
    {
        public Ez2AcHoldPhase Phase { get; private set; } = Ez2AcHoldPhase.Idle;

        public void Reset() => Phase = Ez2AcHoldPhase.Idle;

        /// <summary>
        /// 头判定落地后更新状态。<paramref name="preHeld"/> 为头到达时键已按下（预按）。
        /// </summary>
        public void OnHeadJudged(Ez2AcJudge judge, bool preHeld)
        {
            // 头档位只记在 Head 自身；此处只区分「接上」与「Fail 未接上」。
            if (preHeld)
            {
                Phase = Ez2AcHoldPhase.Holding;
                return;
            }

            Phase = judge switch
            {
                Ez2AcJudge.Fail or Ez2AcJudge.None => Ez2AcHoldPhase.Missing,
                _ => Ez2AcHoldPhase.Holding,
            };
        }

        public void OnRelease()
        {
            if (Phase == Ez2AcHoldPhase.Holding)
                Phase = Ez2AcHoldPhase.Broken;
        }

        public void OnRepress()
        {
            if (Phase is Ez2AcHoldPhase.Broken or Ez2AcHoldPhase.Missing)
                Phase = Ez2AcHoldPhase.Holding;
        }

        /// <summary>按住且处于可涨 combo 的相位时，tick 应 <see cref="osu.Game.Rulesets.Scoring.HitResult.SliderTailHit"/>。</summary>
        public bool ShouldIncreaseCombo => Phase == Ez2AcHoldPhase.Holding;
    }
}
