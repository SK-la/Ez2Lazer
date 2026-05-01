// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.HUD;
using osu.Game.Graphics.Containers;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class BeatmapEzAnalysisWedge : VisibilityContainer
    {
        private EzHUDRadarPanel xxySrRadar = null!;
        private EzHUDRadarPanel keyPatternRadar = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Padding = new MarginPadding { Top = 4f };

            Width = 0.9f;

            InternalChild = new ShearAligningWrapper(new Container
            {
                CornerRadius = 10,
                Masking = true,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Shear = OsuGame.SHEAR,
                Children = new Drawable[]
                {
                    new WedgeBackground(),
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Left = SongSelect.WEDGE_CONTENT_MARGIN, Right = 35, Vertical = 16 },
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(-10f, 0f),
                            Children = new Drawable[]
                            {
                                // 左侧雷达图 - XxySR Pattern
                                xxySrRadar = new EzHUDRadarPanel
                                {
                                    Anchor = Anchor.TopLeft,
                                    Origin = Anchor.TopLeft,
                                    Shear = -OsuGame.SHEAR,
                                    RadarDisplayMode = { Value = EzRadarDisplayMode.XxySrPattern },
                                },
                                // 右侧雷达图 - Key Pattern
                                keyPatternRadar = new EzHUDRadarPanel
                                {
                                    Anchor = Anchor.TopLeft,
                                    Origin = Anchor.TopLeft,
                                    Shear = -OsuGame.SHEAR,
                                    RadarDisplayMode = { Value = EzRadarDisplayMode.KeyPattern },
                                },
                            },
                        },
                    },
                },
            });
        }

        protected override void PopIn()
        {
            this.MoveToX(0, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeIn(SongSelect.ENTER_DURATION / 3, Easing.In);
        }

        protected override void PopOut()
        {
            this.MoveToX(-150, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeOut(SongSelect.ENTER_DURATION / 3, Easing.In);
        }
    }
}
