// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.UI;
using osu.Game.Graphics.Containers;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class BeatmapEzAnalysisWedge : VisibilityContainer
    {
        private EzHUDRadarPanel leftRadar = null!;
        private EzHUDRadarPanel rightRadar = null!;

        public Bindable<string?> TargetUsername { get; } = new Bindable<string?>();

        public Bindable<EzRadarDisplayMode> LeftRadarMode { get; } = new Bindable<EzRadarDisplayMode>(EzRadarDisplayMode.XxySrPattern);

        public Bindable<EzRadarDisplayMode> RightRadarMode { get; } = new Bindable<EzRadarDisplayMode>(EzRadarDisplayMode.Skill);

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
                    new EzSongSelectWedgeBackground(),
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
                                leftRadar = new EzHUDRadarPanel
                                {
                                    Anchor = Anchor.TopLeft,
                                    Origin = Anchor.TopLeft,
                                    Shear = -OsuGame.SHEAR,
                                },
                                rightRadar = new EzHUDRadarPanel
                                {
                                    Anchor = Anchor.TopLeft,
                                    Origin = Anchor.TopLeft,
                                    Shear = -OsuGame.SHEAR,
                                },
                            },
                        },
                    },
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            leftRadar.RadarDisplayMode.BindTo(LeftRadarMode);
            rightRadar.RadarDisplayMode.BindTo(RightRadarMode);
            leftRadar.TargetUsername.BindTo(TargetUsername);
            rightRadar.TargetUsername.BindTo(TargetUsername);
        }

        protected override void PopIn()
        {
            this.MoveToX(0, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeIn(SongSelect.ENTER_DURATION / 3, Easing.In);
        }

        protected override void PopOut()
        {
            this.MoveToX(-100, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeOut(SongSelect.ENTER_DURATION / 3, Easing.In);
        }
    }
}
