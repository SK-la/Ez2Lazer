// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect
{
    /// <summary>
    /// Chart artwork drawn above the existing screen-stack background (the main menu background).
    /// Hidden when the selected chart has no image so that background shows through the transparent shell.
    /// </summary>
    public partial class BmsSongSelectBackgroundLayer : CompositeDrawable
    {
        private readonly Sprite sprite;

        public BmsSongSelectBackgroundLayer()
        {
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;

            InternalChild = sprite = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                FillMode = FillMode.Fill,
            };
        }

        public void SetFromWorking(BMSWorkingBeatmap? working)
        {
            Texture? texture = working?.GetBackground();

            if (texture == null)
            {
                ClearChart();
                return;
            }

            sprite.Texture = texture;
            this.FadeIn(400, Easing.OutQuint);
        }

        public void ClearChart() => this.FadeOut(250, Easing.OutQuint);
    }
}
