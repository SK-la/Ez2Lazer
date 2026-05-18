// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    /// <summary>
    /// 预览模式按钮，用于切换不同的谱面预览模式。
    /// </summary>
    public partial class PreviewModeButton : OsuButton
    {
        private Color4 textColour = Color4.White;
        private bool selected;

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool Selected
        {
            set
            {
                if (selected == value)
                    return;

                selected = value;
                updateVisualState();
            }
        }

        /// <summary>
        /// 文本颜色
        /// </summary>
        public Color4 TextColour
        {
            set
            {
                textColour = value;
                SpriteText.FadeColour(textColour, 120, Easing.OutQuint);
            }
        }

        public PreviewModeButton()
        {
            Size = new Vector2(108, 28);
            Content.CornerRadius = 6;
        }

        protected override float HoverLayerFinalAlpha => 0.06f;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            SpriteText.Colour = textColour;
            updateVisualState();
        }

        private void updateVisualState()
        {
            BackgroundColour = selected ? Color4.CornflowerBlue.Opacity(0.85f) : Color4.Black.Opacity(0.5f);
            TextColour = selected ? Color4.White : Color4.White.Opacity(0.9f);
        }

        protected override SpriteText CreateText() => new OsuSpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold)
        };
    }
}
