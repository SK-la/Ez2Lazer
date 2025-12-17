// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace osu.Game.LAsEzExtensions.Screens.Edit
{
    public partial class LoopIntervalDisplay : CompositeDrawable
    {
        private readonly Box intervalBox;

        public LoopIntervalDisplay()
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;

            InternalChild = intervalBox = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Colour = Colour4.Blue.Opacity(0.5f), // Semi-transparent gray
            };
        }

        public void UpdateInterval(float startX, float endX)
        {
            if (endX > startX)
            {
                intervalBox.X = startX;
                intervalBox.Width = endX - startX;
                intervalBox.Alpha = 1;
            }
            else
            {
                intervalBox.Alpha = 0;
            }
        }
    }
}
