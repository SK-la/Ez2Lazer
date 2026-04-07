// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIHoldNoteTailPiece : SbINotePiece
    {
        private readonly SbIHoldNoteHittingLayer hittingLayer = new SbIHoldNoteHittingLayer();

        private IBindable<double> tailAlpha = null!;
        private IBindable<double> tailMaskHeight = null!;

        [Resolved]
        private DrawableHitObject? drawableObject { get; set; }

        public SbIHoldNoteTailPiece()
        {
            // Height = 8;
            Rotation = 180;
        }

        [BackgroundDependencyLoader(true)]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            tailAlpha = ezSkinInfo.HoldTailAlpha;
            tailMaskHeight = ezSkinInfo.HoldTailMaskHeight;

            // 当设置为负值时显示 tail，非负值时隐藏
            tailMaskHeight.BindValueChanged(maskHeight =>
            {
                Alpha = maskHeight.NewValue < 0 ? (float)tailAlpha.Value : 0f;
            }, true);
        }
    }
}
