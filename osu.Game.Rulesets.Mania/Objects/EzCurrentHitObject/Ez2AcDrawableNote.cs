// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input.Events;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public partial class Ez2AcDrawableNote : DrawableNote
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyMinResult();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);

            if (result == HitResult.None)
                return;

            result = GetCappedResult(result);

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                if (state == HitResult.Meh || state == HitResult.Miss)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class Ez2AcDrawableLNeHead : DrawableHoldNoteHead
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyMinResult();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);

            if (result == HitResult.None)
                return;

            // 提前按住的情况, 先给Fail，然后给Good.
            if (timeOffset < 0 && result == HitResult.Miss)
            {
                ApplyResult(HitResult.Miss);
                return;
            }

            // 放宽1级
            switch (result)
            {
                case HitResult.Great:
                    result = HitResult.Perfect;
                    break;

                case HitResult.Good:
                    result = HitResult.Great;
                    break;

                case HitResult.Meh:
                    result = HitResult.Good;
                    break;
            }

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                if (state == HitResult.Meh || state == HitResult.Miss)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class Ez2AcDrawableLNTail : DrawableHoldNoteTail
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (timeOffset < 0)
                return;

            // 无论用户是否触发，都进行判定
            var result = HitObject.HitWindows.ResultFor(timeOffset);

            if (result == HitResult.None)
                return;

            // 放宽1级
            switch (result)
            {
                case HitResult.Great:
                    result = HitResult.Perfect;
                    break;

                case HitResult.Good:
                    result = HitResult.Great;
                    break;

                case HitResult.Meh:
                    result = HitResult.Good;
                    break;
            }

            // 大失误降级处理
            if (!HoldNote.Head.IsHit || HoldNote.Body.HasHoldBreak)
            {
                if (result > HitResult.Meh)
                {
                    // Head Miss 或 Body Break，但尾点判定时有操作，给 GOOD 判定
                    result = HitResult.Good;
                }
            }

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                if (state == HitResult.Meh || state == HitResult.Miss)
                    r.IsComboHit = false;
            }, result);
        }

        protected override HitResult GetCappedResult(HitResult result)
        {
            bool hasComboBreak = !HoldNote.Head.IsHit || HoldNote.Body.HasHoldBreak;

            if (result > HitResult.Meh && hasComboBreak)
                return HitResult.Good;

            return result;
        }

        public override bool OnPressed(KeyBindingPressEvent<ManiaAction> e) => false; // Handled by the hold note

        public override void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
        }
    }
}
