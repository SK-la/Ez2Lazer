// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// 结算界面底部的 HitResult Offset 控件。
    /// </summary>
    public partial class FooterButtonHitResultOffset : EzRailDragSlider
    {
        public FooterButtonHitResultOffset(Bindable<double> current)
            : base(current)
        {
            NubIcon = FontAwesome.Solid.ArrowsAltH;
            Tooltip = new EzLocalizationManager.EzLocalisableString(
                "左右拖动调整 HitResult Offset，单击重置",
                "Drag left/right to adjust the hit result offset, click to reset");
        }
    }
}
