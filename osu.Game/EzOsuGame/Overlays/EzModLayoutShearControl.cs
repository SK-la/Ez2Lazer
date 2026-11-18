// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzModLayoutShearControl : CompositeDrawable
    {
        private const float slot_width = 200;
        private const float height = 42;
        private const float label_width = 72;

        public Bindable<double> Current { get; }

        public EzModLayoutShearControl(Bindable<double>? external = null)
        {
            var bindable = external ?? new BindableDouble(EzModLayoutResolver.DEFAULT_COLUMN_SHEAR)
            {
                MinValue = 0,
                MaxValue = EzModLayoutResolver.DEFAULT_COLUMN_SHEAR,
                Precision = 0.01,
                Default = EzModLayoutResolver.DEFAULT_COLUMN_SHEAR,
            };

            Current = bindable.GetBoundCopy();

            Size = new Vector2(label_width + slot_width, height);
            InternalChildren = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = EzModLayoutStrings.COLUMN_SHEAR,
                    Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Width = label_width,
                },
                new EzRailDragSlider(Current, FontAwesome.Solid.ArrowsAltH, v => v.ToString("0.00"))
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Width = slot_width,
                    Height = height,
                    Tooltip = EzModLayoutStrings.COLUMN_SHEAR_TOOLTIP,
                },
            };
        }
    }
}
