// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.Drawables
{
    public partial class DrawableHoldNoteTick : DrawableManiaHitObject<HoldNoteTick>
    {
        public override bool DisplayResult => false;

        protected internal DrawableHoldNote HoldNote => ParentHitObject as DrawableHoldNote;

        public DrawableHoldNoteTick()
            : this(null)
        {
        }

        public DrawableHoldNoteTick(HoldNoteTick hitObject)
            : base(hitObject)
        {
        }

        public override void PlaySamples()
        {
            // LN tick 不播 note 音效。
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (ManiaEzDrawableJudgement.TryHoldTickCheckForResult(this, userTriggered, timeOffset))
                return;

            // 非 EZ2AC：Ignore 判定，到点直接完结以免卡 AllJudged。
            if (timeOffset >= 0)
                ApplyMaxResult();
        }

        internal void EzApplyTickResult(HitResult result)
            => ApplyResult(static (r, t) => r.Type = t, result);

        internal void UpdateTickResult() => UpdateResult(false);
    }
}
