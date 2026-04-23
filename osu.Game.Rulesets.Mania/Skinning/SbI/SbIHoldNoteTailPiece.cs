// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Colour;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    /// <summary>
    /// <see cref="Ez2Setting.ManiaLNGradientEnable"/> 开启时：尾部隐藏，由 <see cref="SbIHoldBodyPiece"/> 的 top 段代替。
    /// 关闭时：常规 body + 独立 tail；tail 使用与 body 顶部段相同的 <see cref="IEzSkinInfo.HoldTailMaskHeight"/> / <see cref="IEzSkinInfo.HoldTailAlpha"/>（高度修正与纵向渐变）。
    /// LN 渐变开关仅在加载时读取；tail 的高度修正与透明度在常规模式下仍随皮肤绑定更新。
    /// </summary>
    internal partial class SbIHoldNoteTailPiece : SbINotePiece
    {
        private readonly IBindable<double> tailAlphaBindable = new Bindable<double>();
        private readonly IBindable<double> tailMaskHeightBindable = new Bindable<double>();

        private bool gradient;

        [BackgroundDependencyLoader(true)]
        private void load(IEzSkinInfo ezSkinInfo, Ez2ConfigManager ezConfig)
        {
            gradient = ezConfig.Get<bool>(Ez2Setting.ManiaLNGradientEnable);

            if (gradient)
            {
                Alpha = 0;
                return;
            }

            Alpha = 1;

            tailMaskHeightBindable.BindTo(ezSkinInfo.HoldTailMaskHeight);
            tailAlphaBindable.BindTo(ezSkinInfo.HoldTailAlpha);

            tailMaskHeightBindable.BindValueChanged(_ => UpdateDrawable(), true);
            tailAlphaBindable.BindValueChanged(_ => UpdateColor(), true);
        }

        protected override void UpdateDrawable()
        {
            base.UpdateDrawable();

            if (gradient)
                return;

            float visibleHeight = UnitHeight - (float)tailMaskHeightBindable.Value;
            Height = visibleHeight;
        }

        protected override void UpdateColor()
        {
            if (gradient)
                return;

            MainContainer.Colour = ColourInfo.GradientVertical(
                NoteColor.Opacity((float)tailAlphaBindable.Value),
                NoteColor);
        }
    }
}
