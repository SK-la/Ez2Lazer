// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Screens
{
    public partial class EzSkinColorButton : CompositeDrawable
    {
        private readonly Box background;
        private readonly Box colorBox;
        private readonly Container content;
        private readonly OsuSpriteText label;

        public Action? Action;
        private bool selected;
        private bool isHovered;

        public bool Selected
        {
            get => selected;
            set
            {
                if (selected == value)
                    return;

                selected = value;
                updateVisualState();
            }
        }

        public EzSkinColorButton(string colorName, Color4 color, float height)
        {
            RelativeSizeAxes = Axes.X;
            Height = height;
            Masking = true;
            CornerRadius = 6;

            EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Radius = 3f,
                Colour = Color4.Black.Opacity(0.2f),
                Offset = new Vector2(0, 1),
            };

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White.Opacity(0.2f),
                    Alpha = 0.3f
                },
                content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(2),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 3,
                        Children = new Drawable[]
                        {
                            colorBox = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = color
                            },
                            label = new OsuSpriteText
                            {
                                Text = colorName,
                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                                Colour = getContrastColor(color),
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Shadow = true,
                                ShadowColour = getContrastColor(color).Opacity(0.3f)
                            }
                        }
                    }
                }
            };
        }

        public void UpdateColor(Color4 newColor)
        {
            colorBox.Colour = newColor;
            label.Colour = getContrastColor(newColor);
            label.ShadowColour = getContrastColor(newColor).Opacity(0.3f);
        }

        private void updateVisualState()
        {
            // 根据状态设置背景效果
            if (selected)
            {
                background.Alpha = 1;
                background.Colour = Color4.CornflowerBlue; // 选中状态颜色更亮DeepSkyBlue
            }
            else if (isHovered)
            {
                background.Alpha = 0.8f;
                background.FadeColour(Color4.White.Opacity(0.3f), 200, Easing.OutQuint); // 悬浮高亮效果
            }
            else
            {
                background.Alpha = 0.3f;
                background.FadeColour(Color4.White.Opacity(0.2f), 200, Easing.OutQuint); // 恢复默认
            }

            if (selected)
            {
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Radius = 10f,
                    Colour = colorBox.Colour,
                    Roundness = 3f
                };

                label.Shadow = true;
                label.ShadowColour = getContrastColor(colorBox.Colour).Opacity(0.7f);
                label.ShadowOffset = new Vector2(0.02f, 0.02f);

                this.MoveToY(-2, 200, Easing.OutQuint);
            }
            else if (isHovered)
            {
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Radius = 10f,
                    Colour = Color4.Black.Opacity(0.3f),
                    Roundness = 8f
                };

                label.Shadow = true;
                label.ShadowColour = getContrastColor(colorBox.Colour).Opacity(0.5f);
                label.ShadowOffset = new Vector2(0.02f, 0.02f);

                this.MoveToY(-1, 200, Easing.OutQuint);
            }
            else
            {
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Radius = 3f,
                    Colour = Color4.Black.Opacity(0.2f),
                    Offset = new Vector2(0, 1),
                };

                label.Shadow = true;
                label.ShadowColour = getContrastColor(colorBox.Colour).Opacity(0.3f);
                label.ShadowOffset = new Vector2(0.02f, 0.02f);

                this.MoveToY(0, 200, Easing.OutQuint);
            }

            this.ScaleTo(1.0f, 200, Easing.OutQuint); // 保持原始大小
            content.Scale = Vector2.One;
        }

        //对比色
        private Color4 getContrastColor(Color4 background)
        {
            float brightness = 0.299f * background.R + 0.587f * background.G + 0.114f * background.B;
            return brightness > 0.5f ? Color4.Black : Color4.White;
        }

        protected override bool OnClick(ClickEvent e)
        {
            Action?.Invoke();
            return true;
        }

        protected override bool OnHover(HoverEvent e)
        {
            isHovered = true;
            updateVisualState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            isHovered = false;
            updateVisualState();
        }

        public override bool HandlePositionalInput => Action != null;
    }
}
