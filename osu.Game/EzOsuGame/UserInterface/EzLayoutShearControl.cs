// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Mods;

namespace osu.Game.EzOsuGame.UserInterface
{
    public partial class EzLayoutShearControl : EzRailDragSlider
    {
        public EzLayoutShearControl()
            : base(createCurrent(), v => v.ToString("0.00"))
        {
            NubIcon = FontAwesome.Solid.ArrowsAltH;
            Tooltip = new EzLocalizationManager.EzLocalisableString(
                "左右拖动调整倾斜距离，单击重置",
                "Drag left/right to adjust shear, click to reset");
        }

        private static BindableDouble createCurrent() => new BindableDouble(EzModLayoutResolver.DEFAULT_COLUMN_SHEAR)
        {
            MinValue = 0,
            MaxValue = EzModLayoutResolver.DEFAULT_COLUMN_SHEAR,
            Precision = 0.01,
            Default = EzModLayoutResolver.DEFAULT_COLUMN_SHEAR,
        };
    }
}
