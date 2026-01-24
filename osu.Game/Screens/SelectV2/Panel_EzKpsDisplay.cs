// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.Screens.SelectV2
{
    public partial class EzKpsDisplay : CompositeDrawable
    {
        private readonly OsuSpriteText kpsText;

        public EzKpsDisplay()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = kpsText = new OsuSpriteText
            {
                Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                Colour = Color4.CornflowerBlue,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft
            };
        }

        /// <summary>
        /// 设置KPS显示值
        /// </summary>
        /// <param name="averageKps">平均KPS</param>
        /// <param name="maxKps">最大KPS</param>
        public void SetKps(double averageKps, double maxKps)
        {
            kpsText.Text = averageKps > 0 ? $"  [KPS] {averageKps:F1} ({maxKps:F1} Max)" : "";
        }
    }
}
