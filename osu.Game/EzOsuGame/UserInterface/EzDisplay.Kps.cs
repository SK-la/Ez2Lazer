// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.UserInterface
{
    public partial class EzDisplayKps : CompositeDrawable
    {
        private readonly OsuSpriteText kpsText;

        public EzDisplayKps()
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
        /// <param name="pp">理论满分PP</param>
        /// <param name="averageKps">平均KPS</param>
        /// <param name="maxKps">最大KPS</param>
        public void SetKps(double? pp, double averageKps, double maxKps)
        {
            string ppText = pp is double ppValue ? $"[PP] {ppValue:F1}" : string.Empty;
            string kpsValueText = averageKps > 0 ? $"[KPS] {averageKps:F1} ({maxKps:F1} Max)" : string.Empty;

            if (string.IsNullOrEmpty(ppText) && string.IsNullOrEmpty(kpsValueText))
            {
                kpsText.Text = string.Empty;
                return;
            }

            if (string.IsNullOrEmpty(ppText))
            {
                kpsText.Text = $"  {kpsValueText}";
                return;
            }

            if (string.IsNullOrEmpty(kpsValueText))
            {
                kpsText.Text = $"  {ppText}";
                return;
            }

            kpsText.Text = $"  {ppText}  {kpsValueText}";
        }
    }
}
