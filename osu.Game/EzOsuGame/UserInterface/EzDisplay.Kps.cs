// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using osu.Framework.Graphics.Sprites;

namespace osu.Game.EzOsuGame.UserInterface
{
    public partial class EzDisplayKps : CompositeDrawable
    {
        private readonly Box background;

        // private readonly OsuSpriteText ppIcon;
        private readonly OsuSpriteText ppText;
        private readonly SpriteIcon kpsIcon;
        private readonly OsuSpriteText kpsText;

        public EzDisplayKps()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = new CircularContainer
            {
                Masking = true,
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4Extensions.FromHex("303d47"),
                    },
                    new GridContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Margin = new MarginPadding { Horizontal = 7f },
                        ColumnDimensions = new[]
                        {
                            // new Dimension(GridSizeMode.AutoSize),
                            // new Dimension(GridSizeMode.Absolute, 3f),
                            new Dimension(GridSizeMode.AutoSize, minSize: 25f),
                            new Dimension(GridSizeMode.Absolute, 8f),
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.Absolute, 3f),
                            new Dimension(GridSizeMode.AutoSize, minSize: 25f),
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new[]
                            {
                                // ppIcon = new OsuSpriteText
                                // {
                                //     Anchor = Anchor.Centre,
                                //     Origin = Anchor.Centre,
                                //     Text = LegacySpriteText.PP_SUFFIX_CHAR.ToString(),
                                //     Font = OsuFont.Style.Body.With(size: 16f, weight: FontWeight.Bold),
                                //     Colour = Color4.Gold,
                                // },
                                // Empty(),
                                ppText = new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Margin = new MarginPadding { Bottom = 1.5f },
                                    Font = OsuFont.Torus.With(size: 14.4f, weight: FontWeight.Bold, fixedWidth: true),
                                    Shadow = false,
                                    Colour = Color4.LightSteelBlue,
                                },
                                Empty(),
                                kpsIcon = new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Icon = FontAwesome.Solid.TachometerAlt,
                                    Size = new Vector2(14f),
                                    Colour = Color4.CornflowerBlue,
                                },
                                Empty(),
                                kpsText = new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Margin = new MarginPadding { Bottom = 1.5f },
                                    Font = OsuFont.Torus.With(size: 14.4f, weight: FontWeight.Bold, fixedWidth: true),
                                    Shadow = false,
                                    Colour = Color4.LightSteelBlue,
                                },
                            }
                        }
                    },
                }
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
            string ppValueText = pp is double ppValue ? $"PP {ppValue:F1}" : string.Empty;
            string kpsValueText = averageKps > 0 ? $"{averageKps:F1} ({maxKps:F1})" : string.Empty;

            // 更新PP显示
            if (string.IsNullOrEmpty(ppValueText))
            {
                // ppIcon.Alpha = 0;
                ppText.Text = string.Empty;
            }
            else
            {
                // ppIcon.Alpha = 1;
                ppText.Text = ppValueText;
            }

            // 更新KPS显示
            if (string.IsNullOrEmpty(kpsValueText))
            {
                kpsIcon.Alpha = 0;
                kpsText.Text = string.Empty;
            }
            else
            {
                kpsIcon.Alpha = 1;
                kpsText.Text = kpsValueText;
            }

            // 如果两者都为空，隐藏整个组件
            if (string.IsNullOrEmpty(ppValueText) && string.IsNullOrEmpty(kpsValueText))
            {
                Alpha = 0;
            }
            else
            {
                Alpha = 1;
            }
        }
    }
}
