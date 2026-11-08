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
                    new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        // A horizontal fill requires every child to share one relative anchor, so the gap is the
                        // container's spacing rather than a zero-size spacer child (which carries the default
                        // top-left anchor and would fail the layout assertion).
                        Spacing = new Vector2(3, 0),
                        Margin = new MarginPadding { Horizontal = 7f },
                        Children = new Drawable[]
                        {
                            new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.TachometerAlt,
                                Size = new Vector2(14f),
                                Colour = Color4.CornflowerBlue,
                            },
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
                    },
                }
            };
        }

        /// <summary>
        /// 设置 KPS 指标；<paramref name="averageKps"/> ≤ 0 时清空 KPS 区（如 reset 传 0）。
        /// </summary>
        public void SetKpsMetrics(double averageKps, double maxKps)
        {
            kpsText.Text = averageKps <= 0
                ? string.Empty
                : $"{averageKps:F1} ({maxKps:F1})";
        }

        public void SetKpsMetrics(in EzSongSelectAnalysisDisplay.PanelMetrics metrics) => SetKpsMetrics(metrics.AverageKps, metrics.MaxKps);
    }
}
