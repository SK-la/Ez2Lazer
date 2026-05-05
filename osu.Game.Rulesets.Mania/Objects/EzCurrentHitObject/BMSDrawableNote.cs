// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input.Events;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    /*
     * BMS 判定状态机（Note / LN Head / LN Tail 共用）
     *
     * [状态输入]
     * - userTriggered: 是否由按键触发
     * - timeOffset: 当前输入相对目标物件的偏移
     * - badLate: Bad 的晚侧边界（用于 Poor/KPoor 分流）
     *
     * [主流程]
     * 1) userTriggered == false（自动结算路径）
     *    - ResultFor(timeOffset) == None 且 timeOffset > badLate -> ApplyResult(Poor)
     *    - 其它情况不结算
     *
     * 2) userTriggered == true（按键路径）
     *    - ResolvePressedResult() 返回非 None 的正常档 -> ApplyResult(该档)
     *    - ResolvePressedResult() 返回 Poor -> ApplyResult(Poor)
     *    - ResolvePressedResult() 返回 KPoor -> DispatchNewResult(KPoor)
     *    - ResolvePressedResult() 返回 None -> 忽略该物件（Raja 7k 的越窗空按走此分支）
     *
     * 3) KPoor 分发规则（当 KPoor 区间存在时生效）
     *    - 晚按路径（timeOffset > badLate）最多分发一次
     *    - 早按路径允许重复分发
     *
     * 4) LN Tail 追加规则
     *    - HoldBreak 或 持有状态下晚于 badLate -> 强制 Poor
     */

    /// <summary>
    /// BMS 判定定义与映射。
    /// <para>判定层级：Perfect &gt; Great &gt; Good &gt; Bad &gt; Poor(含见逃) &gt; KPoor。</para>
    /// 在本文件中，对话描述的简称默认指BMSJudgeMapping名。说poor时，默认指BMSJudgeMapping.Poor。
    /// </summary>
    public static partial class BMSJudgeMapping
    {
        /// <summary>
        /// BD，常规Bad。
        /// 判定的最小窗口结果。
        /// </summary>
        public static HitResult Bad => HitResult.Meh;

        /// <summary>
        /// 普通 poor，见逃し，也叫见逃しpoor。
        /// 常规判定的非窗口结果，通过机制判断。
        /// </summary>
        public static HitResult Poor => HitResult.Miss;

        /// <summary>
        /// MS, 空 POOR, KPoor。
        /// 有固定区间的非常规判定，用于惩罚，与note无直接的one by one关系，数量不与 note 数量绑定，且不作为 note 的最终结算结果。
        /// <para></para>
        /// </summary>
        public static HitResult KPoor => HitResult.Poor;

        // BMS不适用下面这种方法，CaBeHit会按判定区间检查，这与bms不同
        // if (!HitObject.HitWindows.CanBeHit(timeOffset))
        //     ApplyMinResult();

        /// <summary>
        /// 是否已经越过 Bad 的晚侧边界。
        /// </summary>
        public static bool IsLateOutsideBad(double timeOffset, double badLate) => timeOffset > badLate;

        /// <summary>
        /// 自动结算路径：当已越过可判区间且晚于 Bad 边界时，触发 Poor（见逃）。
        /// </summary>
        public static bool ShouldAutoMiss(ManiaHitWindows windows, double timeOffset, double badLate)
            => windows.ResultFor(timeOffset) == HitResult.None && IsLateOutsideBad(timeOffset, badLate);

        /// <summary>
        /// 按键路径的判定分流：
        /// <para>1) 命中常规窗口：返回窗口对应结果；</para>
        /// <para>2) 越过 Bad 晚界：返回 Poor；</para>
        /// <para>3) 落在 KPoor 早侧区间（[-kPoorEarly, -badEarly)）：返回 KPoor；</para>
        /// <para>4) 其余越窗按下：返回 None。</para>
        /// </summary>
        public static HitResult ResolvePressedResult(ManiaHitWindows windows, double timeOffset, double badLate)
        {
            var result = windows.ResultFor(timeOffset);

            if (result != HitResult.None)
                return result;

            // 未判定前：晚按越过 bad 窗口应算正常 miss（见逃同类），而非 KPoor。
            if (IsLateOutsideBad(timeOffset, badLate))
                return Poor;

            double badEarly = windows.WindowFor(Bad, true);
            double kPoorEarly = windows.WindowFor(KPoor, true);

            // KPoor 仅走早侧区间：[-kPoorEarly, -badEarly)。
            if (kPoorEarly > badEarly && timeOffset < -badEarly && timeOffset >= -kPoorEarly)
                return KPoor;

            return HitResult.None;
        }
    }

    public partial class BMSDrawableNote : DrawableNote
    {
        private bool hasKPoor;
        public bool CanRouteToKPoor { get; private set; }

        public override bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action != Action.Value)
                return false;

            // 二次判定。晚按时，最多只判 1 次 KPoor；早按（空按模拟）允许多次。
            if (CanRouteToKPoor && HitObject.HitWindows!.IsHitResultAllowed(BMSJudgeMapping.KPoor))
            {
                DispatchNewResult(BMSJudgeMapping.KPoor);
                CanRouteToKPoor = false;
                return true;
            }

            return base.OnPressed(e);
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            var helper = (ManiaHitWindows)HitObject.HitWindows!;
            double badLate = helper.WindowFor(BMSJudgeMapping.Bad, false);

            if (!userTriggered)
            {
                if (BMSJudgeMapping.ShouldAutoMiss(helper, timeOffset, badLate))
                    ApplyResult(BMSJudgeMapping.Poor);
                return;
            }

            var result = BMSJudgeMapping.ResolvePressedResult(helper, timeOffset, badLate);

            if (result == HitResult.None)
                return;

            if (result == BMSJudgeMapping.KPoor)
            {
                if (!HitObject.HitWindows.IsHitResultAllowed(BMSJudgeMapping.KPoor))
                    return;

                // 晚按时，最多只判 1 次 KPoor；早按（空按模拟）允许多次。
                bool isLatePress = BMSJudgeMapping.IsLateOutsideBad(timeOffset, badLate);

                if (!isLatePress || !hasKPoor)
                    DispatchNewResult(BMSJudgeMapping.KPoor);

                if (isLatePress)
                    hasKPoor = true;
                else
                    hasKPoor = false;

                return;
            }

            CanRouteToKPoor = userTriggered && result == BMSJudgeMapping.Bad && timeOffset < 0;

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                if (state == HitResult.Meh || state == HitResult.Miss)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class BMSDrawableHoldNoteHead : DrawableHoldNoteHead
    {
        private bool hasKPoor;
        public bool CanRouteToKPoor { get; private set; }

        public override bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action != Action.Value)
                return false;

            if (CanRouteToKPoor && HitObject.HitWindows!.IsHitResultAllowed(BMSJudgeMapping.KPoor))
            {
                DispatchNewResult(BMSJudgeMapping.KPoor);
                CanRouteToKPoor = false;
                return true;
            }

            return base.OnPressed(e);
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            var helper = (ManiaHitWindows)HitObject.HitWindows!;
            double badLate = helper.WindowFor(BMSJudgeMapping.Bad, false);

            if (!userTriggered)
            {
                if (BMSJudgeMapping.ShouldAutoMiss(helper, timeOffset, badLate))
                    ApplyResult(BMSJudgeMapping.Poor);
                return;
            }

            var result = BMSJudgeMapping.ResolvePressedResult(helper, timeOffset, badLate);

            if (result == HitResult.None)
                return;

            if (result == BMSJudgeMapping.KPoor)
            {
                if (!HitObject.HitWindows.IsHitResultAllowed(BMSJudgeMapping.KPoor))
                    return;

                // 晚按时，最多只判 1 次 KPoor；早按（空按模拟）允许多次。
                bool isLatePress = BMSJudgeMapping.IsLateOutsideBad(timeOffset, badLate);

                if (!isLatePress || !hasKPoor)
                    DispatchNewResult(BMSJudgeMapping.KPoor);

                if (isLatePress)
                    hasKPoor = true;
                else
                    hasKPoor = false;

                return;
            }

            CanRouteToKPoor = userTriggered && result == BMSJudgeMapping.Bad && timeOffset < 0;

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                if (state == HitResult.Meh || state == HitResult.Miss)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class BMSDrawableHoldNoteTail : DrawableHoldNoteTail
    {
        private bool hasKPoor;
        public bool CanRouteToKPoor { get; private set; }
        public override bool DisplayResult => true;

        public override bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action != Action.Value)
                return false;

            if (CanRouteToKPoor && HitObject.HitWindows!.IsHitResultAllowed(BMSJudgeMapping.KPoor))
            {
                DispatchNewResult(BMSJudgeMapping.KPoor);
                CanRouteToKPoor = false;
                return true;
            }

            return base.OnPressed(e);
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            var helper = (ManiaHitWindows)HitObject.HitWindows!;
            double badLate = helper.WindowFor(BMSJudgeMapping.Bad, false);

            if (!userTriggered)
            {
                if (BMSJudgeMapping.ShouldAutoMiss(helper, timeOffset, badLate))
                    ApplyResult(BMSJudgeMapping.Poor);
                return;
            }

            var result = BMSJudgeMapping.ResolvePressedResult(helper, timeOffset, badLate);

            if (result == HitResult.None)
                return;

            // LN特殊规则
            if (HoldNote.Body.HasHoldBreak)
                result = BMSJudgeMapping.Poor;

            if (HoldNote.IsHolding.Value && timeOffset > badLate)
                result = BMSJudgeMapping.Poor;

            if (result == BMSJudgeMapping.KPoor)
            {
                if (!HitObject.HitWindows.IsHitResultAllowed(BMSJudgeMapping.KPoor))
                    return;

                // 晚按时，最多只判 1 次 KPoor；早按（空按模拟）允许多次。
                bool isLatePress = BMSJudgeMapping.IsLateOutsideBad(timeOffset, badLate);

                if (!isLatePress || !hasKPoor)
                    DispatchNewResult(BMSJudgeMapping.KPoor);

                if (isLatePress)
                    hasKPoor = true;
                else
                    hasKPoor = false;

                return;
            }

            // Logger.Log($"Tail result: {result}, IsHolding: {HoldNote.IsHolding.Value}, HasHoldBreak: {HoldNote.Body.HasHoldBreak}");
            CanRouteToKPoor = userTriggered && result == BMSJudgeMapping.Bad && timeOffset < 0;

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                if (state == HitResult.Meh || state == HitResult.Miss)
                    r.IsComboHit = false;
            }, result);
        }
    }
}
