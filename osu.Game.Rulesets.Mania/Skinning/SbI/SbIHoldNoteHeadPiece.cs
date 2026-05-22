// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    /// <summary>
    /// 完整 note 尺寸绘制，由裁切容器仅显示下半区，露出底部两个圆角。
    /// </summary>
    internal partial class SbIHoldNoteHeadPiece : SbINotePiece
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            MainContainer.Anchor = Anchor.BottomCentre;
            MainContainer.Origin = Anchor.BottomCentre;
            Container.Anchor = Anchor.BottomCentre;
            Container.Origin = Anchor.BottomCentre;
        }

        protected override void UpdateDrawable()
        {
            if (DrawWidth <= 1)
            {
                Schedule(UpdateDrawable);
                return;
            }

            float radius = (float)CornerRadiusBindable.Value;
            float height = UnitHeight;
            float halfHeight = height / 2;

            Height = halfHeight;
            MainContainer.Height = halfHeight;
            Container.Height = height;
            Container.CornerRadius = radius;
        }
    }
}
