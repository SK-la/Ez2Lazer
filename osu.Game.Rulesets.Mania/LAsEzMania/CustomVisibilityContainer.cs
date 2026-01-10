// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Rulesets.Mania.LAsEZMania
{
    public partial class CustomVisibilityContainer : VisibilityContainer
    {
        public CustomVisibilityContainer()
        {
            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;
        }

        protected override void PopIn()
        {
            this.FadeIn();
        }

        protected override void PopOut()
        {
            this.FadeOut();
        }
    }
}
