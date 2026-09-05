// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Overlays;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Horizontal track + rounded fill bar (profile chart style).
    /// </summary>
    public partial class EzLocalProfileRoundedBar : Container
    {
        private readonly float fillRatio;

        public EzLocalProfileRoundedBar(float fillRatio)
        {
            this.fillRatio = float.IsFinite(fillRatio) ? Math.Clamp(fillRatio, 0f, 1f) : 0;
            RelativeSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 6;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Background6,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Width = fillRatio,
                    Colour = colours.Highlight1,
                }
            };
        }
    }
}
