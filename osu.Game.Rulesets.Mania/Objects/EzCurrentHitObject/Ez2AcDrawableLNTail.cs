// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public partial class Ez2AcDrawableLNTail : DrawableHoldNoteTail
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (HoldNote.IsHolding.Value && timeOffset >= 0)
                ApplyResult(HitResult.Perfect);
            else
                base.CheckForResult(userTriggered, timeOffset);
        }
    }

    public partial class Ez2AcDrawableNote : DrawableNote
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            // 非用户输入（时间到了但用户没按键），直接判定为 Miss
            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyMinResult();

                return;
            }

            // 用户触发判定
            HitResult result = HitObject.HitWindows.ResultFor(timeOffset);

            // 如果在正常判定窗口外，检查是否在 Pool 窗口内
            if (result == HitResult.None && HitObject.HitWindows is ManiaHitWindows ezWindows)
            {
                double missWindow = ezWindows.WindowFor(HitResult.Miss);
                double poolEarlyWindow = missWindow + 500;
                double poolLateWindow = missWindow + 150;
                double absTimeOffset = System.Math.Abs(timeOffset);

                // 检查是否超出 miss 窗口但在 Pool 窗口内
                if (absTimeOffset > missWindow)
                {
                    // 提前按下（timeOffset < 0）且在提前 Pool 窗口内
                    if (timeOffset < 0 && absTimeOffset <= poolEarlyWindow)
                        result = HitResult.Pool;

                    // 晚按（timeOffset > 0）且在晚按 Pool 窗口内
                    if (timeOffset > 0 && timeOffset <= poolLateWindow)
                        result = HitResult.Pool;
                }
            }

            if (result == HitResult.None)
                return;

            // if (timeOffset >= 150 && timeOffset <= -500)
            //     ApplyResult(HitResult.Pool);

            result = GetCappedResult(result);

            ApplyResult(result);
        }
    }
}
