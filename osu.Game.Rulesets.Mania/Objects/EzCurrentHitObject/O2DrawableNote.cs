// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public partial class O2DrawableNote : DrawableNote
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            bool upgradeToPerfect = false;

            if (userTriggered)
            {
                // 使用当前时间进行动态 BPM 计算
                bool cont = O2HitModeExtension.PillCheck(timeOffset, Time.Current, out bool _, out upgradeToPerfect);
                if (!cont) return;
            }

            // 此处有潜在的崩溃风险，与播放动画有关，待调查。
            // Replicate base implementation to allow attaching combo semantics overrides.
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

            if (upgradeToPerfect)
                result = HitResult.Perfect;

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                // In O2Jam hit mode, Meh should break combo.
                if (state == HitResult.Meh)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class O2DrawableHoldNoteHead : DrawableHoldNoteHead
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            bool upgradeToPerfect = false;

            if (userTriggered)
            {
                // 使用当前时间进行动态 BPM 计算
                bool cont = O2HitModeExtension.PillCheck(timeOffset, Time.Current, out bool _, out upgradeToPerfect);
                if (!cont) return;
            }

            // Replicate base implementation to allow attaching combo semantics overrides.
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

            if (upgradeToPerfect)
                result = HitResult.Perfect;

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                // In O2Jam hit mode, Meh should break combo.
                if (state == HitResult.Meh)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class O2DrawableHoldNoteTail : DrawableHoldNoteTail
    {
        public override bool DisplayResult => false;

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            bool upgradeToPerfect = false;

            if (userTriggered)
            {
                // 使用当前时间进行动态 BPM 计算
                bool cont = O2HitModeExtension.PillCheck(timeOffset, Time.Current, out bool _, out upgradeToPerfect);
                if (!cont) return;
            }

            double adjustedOffset = timeOffset;

            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(adjustedOffset))
                    ApplyMinResult();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(adjustedOffset);

            if (result == HitResult.None)
                return;

            // 如果 Body 已断连，在到达尾点之前不应出任何判定，等待 adjustedOffset >= 0。
            if (HoldNote.Body.HasHoldBreak && adjustedOffset < 0)
                return;

            // 到达尾点（或其后）且玩家未按住时，因 Body 已断连而强制 Miss。
            if (HoldNote.Body.HasHoldBreak && adjustedOffset >= 0)
                result = HitResult.Miss;

            if (upgradeToPerfect)
                result = HitResult.Perfect;

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                if (state == HitResult.Meh)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class O2DrawableHoldNote : DrawableHoldNote
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (Tail.AllJudged)
            {
                if (Tail.IsHit)
                {
                    bool breakComboFromTailMeh = Tail.Result.Type == HitResult.Meh;

                    ApplyResult(static (r, breakCombo) =>
                    {
                        r.Type = r.Judgement.MaxResult;

                        // In O2Jam hit mode, a Meh on the tail should terminally break combo.
                        if (breakCombo)
                            r.IsComboHit = false;
                    }, breakComboFromTailMeh);
                }
                else
                    MissForcefully();

                // Make sure that the hold note is fully judged by giving the body a judgement.
                if (!Body.AllJudged)
                    Body.TriggerResult(Tail.IsHit);

                // Important that this is always called when a result is applied.
                Result.ReportHoldState(Time.Current, false);
            }
        }

        private System.Func<DrawableHitObject, double, bool>? originalCheckHittable;

        protected override void Update()
        {
            base.Update();

            // 如果 Body 已断连且尚未到达尾点，强制锁定 IsHolding 状态。
            if (Body != null && Tail != null && Body.HasHoldBreak && Time.Current < Tail.HitObject.StartTime)
            {
                if (IsHolding is Bindable<bool> b)
                    b.Value = false;
            }
            else
            {
                // 当不处于锁定期，恢复可能被替换的 CheckHittable
                if (originalCheckHittable != null)
                {
                    CheckHittable = originalCheckHittable;
                    originalCheckHittable = null;
                }
            }

            // 在锁定期，确保输入不可命中
            if (Body != null && Tail != null && Body.HasHoldBreak && Time.Current < Tail.HitObject.StartTime)
            {
                originalCheckHittable ??= CheckHittable;

                CheckHittable = (d, t) => false;
            }
        }
    }
}
