// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.UserInterface;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzModLayoutShearControl : CompositeDrawable
    {
        public Bindable<double> Current { get; }

        public EzModLayoutShearControl(Bindable<double>? external = null)
        {
            Current = external ?? new BindableDouble(EzModLayoutResolver.DEFAULT_COLUMN_SHEAR)
            {
                MinValue = 0,
                MaxValue = EzModLayoutResolver.DEFAULT_COLUMN_SHEAR,
                Precision = 0.01,
                Default = EzModLayoutResolver.DEFAULT_COLUMN_SHEAR,
            };

            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new EzRailDragSlider(Current, FontAwesome.Solid.ArrowsAltH, v => v.ToString("0.00"))
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    RelativeSizeAxes = Axes.Both,
                    Tooltip = EzModLayoutStrings.COLUMN_SHEAR_TOOLTIP,
                },
            };
        }
    }
}
