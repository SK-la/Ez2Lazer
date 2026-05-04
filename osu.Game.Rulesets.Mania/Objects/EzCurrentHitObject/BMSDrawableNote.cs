// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    /// <summary>
    /// BMS专用Note规则，注意查阅网站时，BMS的正负偏移与osu定义是相反的
    /// <para></para>1. Perfect>Great>Good>Bad>Poor>見逃しPOOR>KPoor(无note空按)
    /// <para>2. Poor判属于常规判定，只判按早的情况</para>
    /// 3. 見逃しPOOR不属于常规判定结果, 无判定区间。定义是Bad区间结束后都没有按的判定
    /// <para>4. KPoor不属于常规判定结果，不断Combo。定义是在没有note时按的判定</para>
    /// 5. 综合2~4可以简化为，当按键在Bad区间外，符合早按区间为Poor，没按为見逃しPOOR，其他情况为KPoor
    /// </summary>
    public static partial class BMSJudgeMapping
    {
        // 映射使用Meh时，需要特殊
        public static HitResult Bad => HitResult.Meh;
        public static HitResult Poor => HitResult.Miss; //普通Poor, 见逃しPOOR
        public static HitResult KPoor => HitResult.Poor; //KPoor在无note时按下发生，在比note晚发生时只会出现1次

        // BMS不适用下面这种方法，CaBeHit会按判定区间检查，这与bms不同
        // if (!HitObject.HitWindows.CanBeHit(timeOffset))
        //     ApplyMinResult();
    }

    public partial class BMSDrawableNote : DrawableNote
    {
        private bool hasKPoor;

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            var helper = HitObject.HitWindows as ManiaHitWindows;
            double badLate = helper!.WindowFor(BMSJudgeMapping.Bad, false);
            bool isWithinJudgementWindow = helper.ResultFor(timeOffset) != HitResult.None;

            if (!userTriggered)
            {
                if (!isWithinJudgementWindow && timeOffset > badLate)
                    ApplyResult(BMSJudgeMapping.Poor); // 见逃し KPoor

                return;
            }

            var result = helper.ResultFor(timeOffset);

            if (!isWithinJudgementWindow)
            {
                result = BMSJudgeMapping.KPoor;
            }

            if (result == BMSJudgeMapping.KPoor)
            {
                if (!HitObject.HitWindows.IsHitResultAllowed(BMSJudgeMapping.KPoor))
                    return;

                // 晚按时，最多只判 1 次 KPoor；早按（空按模拟）允许多次。
                bool isLatePress = timeOffset > badLate;

                if (!isLatePress || !hasKPoor)
                    DispatchNewResult(BMSJudgeMapping.KPoor);

                if (isLatePress)
                    hasKPoor = true;
                else
                    hasKPoor = false;

                return;
            }

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

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            var helper = HitObject.HitWindows as ManiaHitWindows;
            double badLate = helper!.WindowFor(BMSJudgeMapping.Bad, false);
            bool isWithinJudgementWindow = helper.ResultFor(timeOffset) != HitResult.None;

            if (!userTriggered)
            {
                if (!isWithinJudgementWindow && timeOffset > badLate)
                    ApplyResult(BMSJudgeMapping.Poor); // 见逃し KPoor

                return;
            }

            var result = helper.ResultFor(timeOffset);

            // 只要不在正常可判定区（CanBeHit返回false），就按了给KPoor
            if (!isWithinJudgementWindow)
            {
                result = BMSJudgeMapping.KPoor;
            }

            if (result == BMSJudgeMapping.KPoor)
            {
                if (!HitObject.HitWindows.IsHitResultAllowed(BMSJudgeMapping.KPoor))
                    return;

                // 晚按时，最多只判 1 次 KPoor；早按（空按模拟）允许多次。
                bool isLatePress = timeOffset > badLate;

                if (!isLatePress || !hasKPoor)
                    DispatchNewResult(BMSJudgeMapping.KPoor);

                if (isLatePress)
                    hasKPoor = true;
                else
                    hasKPoor = false;

                return;
            }

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
        public override bool DisplayResult => true;

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            var helper = HitObject.HitWindows as ManiaHitWindows;
            double badLate = helper!.WindowFor(BMSJudgeMapping.Bad, false);
            bool isWithinJudgementWindow = helper.ResultFor(timeOffset) != HitResult.None;

            if (!userTriggered)
            {
                if (!isWithinJudgementWindow && timeOffset > badLate)
                    ApplyResult(BMSJudgeMapping.Poor); // 见逃し KPoor

                return;
            }

            var result = helper.ResultFor(timeOffset);

            // 只要不在正常可判定区（CanBeHit返回false），就按了给KPoor
            if (!isWithinJudgementWindow)
            {
                result = BMSJudgeMapping.KPoor;
            }

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
                bool isLatePress = timeOffset > badLate;

                if (!isLatePress || !hasKPoor)
                    DispatchNewResult(BMSJudgeMapping.KPoor);

                if (isLatePress)
                    hasKPoor = true;
                else
                    hasKPoor = false;

                return;
            }

            // Logger.Log($"Tail result: {result}, IsHolding: {HoldNote.IsHolding.Value}, HasHoldBreak: {HoldNote.Body.HasHoldBreak}");
            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                if (state == HitResult.Meh || state == HitResult.Miss)
                    r.IsComboHit = false;
            }, result);
        }
    }
}
