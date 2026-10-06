// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Configuration;
#if DEBUG
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
#endif
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.Drawables
{
    /// <summary>
    /// The tail of a <see cref="DrawableHoldNote"/>.
    /// </summary>
    public partial class DrawableHoldNoteTail : DrawableNote
    {
        /// <summary>
        /// The time at which the user starting missing the hold note.
        /// This could be the time at which they missed the head, broke on the body, or missed the tail.
        /// </summary>
        public readonly IBindable<double?> MissingStartTime = new Bindable<double?>();

        protected override ManiaSkinComponents Component => ManiaSkinComponents.HoldNoteTail;

        protected internal DrawableHoldNote HoldNote => (DrawableHoldNote)ParentHitObject;

        public DrawableHoldNoteTail()
            : this(null)
        {
        }

        public DrawableHoldNoteTail(TailNote tailNote)
            : base(tailNote)
        {
            Anchor = Anchor.TopCentre;
            Origin = Anchor.TopCentre;
        }

        protected override void OnApply()
        {
            base.OnApply();

            if (ParentHitObject is DrawableHoldNote parentHold)
                MissingStartTime.BindTo(parentHold.MissingStartTime);
        }

        protected override void OnFree()
        {
            base.OnFree();

            if (ParentHitObject is DrawableHoldNote parentHold)
                MissingStartTime.UnbindFrom(parentHold.MissingStartTime);
        }

        public void UpdateResult() => base.UpdateResult(true);

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (UsesEzJudgement && ManiaEzDrawableJudgement.TryHoldTailCheckForResult(this, userTriggered, timeOffset))
                return;

            // Factor in the release lenience
            base.CheckForResult(userTriggered, timeOffset / TailNote.RELEASE_WINDOW_LENIENCE);
        }

        protected override HitResult GetCappedResult(HitResult result)
        {
            // If the head wasn't hit or the hold note was broken, cap the max score to Meh.
            bool hasComboBreak = !HoldNote.Head.IsHit || HoldNote.Body.HasHoldBreak;

            if (result > HitResult.Meh && hasComboBreak)
                return HitResult.Meh;

            return result;
        }

        /// <summary>
        /// LN-INPUT-SLOT：tail 的松手由父 <see cref="DrawableHoldNote"/> 处理，空实现不进非位置输入队列。
        /// </summary>
#if DEBUG
        public override bool HandleNonPositionalInput => ManiaHoldAblation.EnqueueHoldEnds && base.HandleNonPositionalInput;
#else
        public override bool HandleNonPositionalInput => false;
#endif

        public override bool OnPressed(KeyBindingPressEvent<ManiaAction> e) => false; // Handled by the hold note

        public override void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
        }

        public new bool CanRouteToKPoor => ManiaEzDrawableJudgement.CanRouteToKPoor(this);

        public override bool DisplayResult => !ManiaEzDrawableJudgement.ShouldHideTailDisplayResult(ManiaEzDrawableJudgement.GetJudgementRound(this)) && base.DisplayResult;

        internal override void EzApplyFinalResult(HitResult result, EzEnumHitMode hitMode)
        {
            result = ManiaEzDrawableJudgement.SanitizeResult(this, result);

            ApplyResult(static (r, data) =>
            {
                r.Type = data.result;

                if (data.result == HitResult.Miss
                    || (data.result == HitResult.Meh && HitModeHelper.MehBreaksCombo(data.hitMode)))
                {
                    r.IsComboHit = false;
                }
            }, (result, hitMode));
        }

        internal new void EzDispatchExtraResult(HitResult result) => DispatchNewResult(result);

        internal void EzApplyMinResult() => ApplyMinResult();
    }
}
