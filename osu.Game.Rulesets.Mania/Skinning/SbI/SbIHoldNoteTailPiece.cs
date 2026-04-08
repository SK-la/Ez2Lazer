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
    public partial class SbIHoldNoteTailPiece : SbINotePiece
    {
        private bool lnGradientEnable;

        private readonly IBindable<double> tailAlphaBindable = new Bindable<double>();
        private readonly IBindable<double> tailMaskHeightBindable = new Bindable<double>();

        public SbIHoldNoteTailPiece()
        {
            Rotation = 180;
        }

        [BackgroundDependencyLoader(true)]
        private void load(IEzSkinInfo ezSkinInfo, Ez2ConfigManager ezConfig)
        {
            lnGradientEnable = ezConfig.Get<bool>(Ez2Setting.ManiaLNGradientEnable);

            if (lnGradientEnable)
            {
                Alpha = 0;
                return;
            }

            Alpha = 1;

            tailAlphaBindable.BindTo(ezSkinInfo.HoldTailAlpha);
            tailMaskHeightBindable.BindTo(ezSkinInfo.HoldTailMaskHeight);

            tailAlphaBindable.BindValueChanged(_ => UpdateDrawable());
            tailMaskHeightBindable.BindValueChanged(_ => UpdateDrawable(), true);
        }

        protected override void UpdateDrawable()
        {
            if (lnGradientEnable || MainContainer == null)
                return;

            // 与 SbIHoldBodyPiece 中 topContainer 的 Y 规则一致
            MainContainer.Y = tailMaskHeightBindable.Value > 0
                ? (float)tailMaskHeightBindable.Value
                : 0;
        }

        protected override void UpdateColor()
        {
            if (lnGradientEnable)
                return;

            if (MainContainer != null)
            {
                MainContainer.Colour = ColourInfo.GradientVertical(
                    NoteColor.Opacity((float)tailAlphaBindable.Value),
                    NoteColor);
                return;
            }

            base.UpdateColor();
        }
    }
}
