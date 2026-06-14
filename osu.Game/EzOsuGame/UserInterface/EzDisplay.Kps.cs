// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.UserInterface
{
    public partial class EzDisplayKps : CompositeDrawable
    {
        private const float pp_value_width = 32f;

        private readonly OsuSpriteText ppLabel;
        private readonly OsuSpriteText ppValue;
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
                    new Box
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
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.Absolute, pp_value_width),
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
                                ppLabel = new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = "PP",
                                    Margin = new MarginPadding { Bottom = 1.5f },
                                    Font = OsuFont.Torus.With(size: 14.4f, weight: FontWeight.Bold, fixedWidth: true),
                                    Shadow = false,
                                    Colour = Color4.LightSteelBlue,
                                },
                                ppValue = new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Margin = new MarginPadding { Bottom = 1.5f, Left = 2f },
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
        /// 设置 Panel PP 数值；「PP」标签固定，数值列预留宽度。无值传 <see langword="null"/>。
        /// </summary>
        public void SetPp(double? pp) =>
            ppValue.Text = pp?.ToString("F1") ?? string.Empty;

        /// <summary>
        /// 设置 KPS 指标；<paramref name="averageKps"/> ≤ 0 时清空 KPS 区（如 reset 传 0）。
        /// </summary>
        public void SetKpsMetrics(double averageKps, double maxKps)
        {
            if (averageKps <= 0)
            {
                kpsIcon.Alpha = 0;
                kpsText.Text = string.Empty;
                return;
            }

            kpsIcon.Alpha = 1;
            kpsText.Text = $"{averageKps:F1} ({maxKps:F1})";
        }

        public void SetKpsMetrics(in EzSongSelectAnalysisDisplay.PanelMetrics metrics) =>
            SetKpsMetrics(metrics.AverageKps, metrics.MaxKps);
    }
}
