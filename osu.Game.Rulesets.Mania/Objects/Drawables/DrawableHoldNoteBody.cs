// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.Drawables
{
    public partial class DrawableHoldNoteBody : DrawableManiaHitObject<HoldNoteBody>
    {
        public bool HasHoldBreak => AllJudged && !IsHit;

        public override bool DisplayResult => false;

        public DrawableHoldNoteBody()
            : this(null)
        {
        }

        public DrawableHoldNoteBody(HoldNoteBody hitObject)
            : base(hitObject)
        {
        }

        // TODO: 为Mod提供覆写，未来恢复此改动，使用更合适的方式实现Mod对LN判定的修改
        internal virtual void TriggerResult(bool hit)
        {
            if (AllJudged) return;

            if (hit)
                ApplyMaxResult();
            else
                ApplyMinResult();
        }

        /// <summary>
        /// 补发瞬态判定：只进统计与 combo，不改变 Body 的判定态（一条 LN 多次断连时用）。
        /// </summary>
        internal void EzDispatchExtraResult(HitResult result) => DispatchNewResult(result);
    }
}
