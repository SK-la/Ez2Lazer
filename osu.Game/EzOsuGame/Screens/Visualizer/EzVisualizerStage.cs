// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Screens.Menu;
using osu.Game.Screens.Menu;
using osuTK;

namespace osu.Game.EzOsuGame.Screens.Visualizer
{
    public partial class EzVisualizerStage : Container
    {
        private const float visualiser_default_alpha = 0.5f;
        private const float logo_to_controls_spacing = 20;

        public EzVisualizerStage()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    Size = new Vector2(EzVisualizerBeatmapLogo.BASE_SIZE),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Children = new Drawable[]
                    {
                        new EzMenuLogoVisualisation
                        {
                            Size = new Vector2(EzVisualizerBeatmapLogo.BASE_SIZE) * OsuLogo.SCALE_ADJUST,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Alpha = visualiser_default_alpha,
                        },
                        new EzVisualizerBeatmapLogo(),
                    }
                },
                new EzVisualizerPlaybackBar
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.TopCentre,
                    Y = EzVisualizerBeatmapLogo.BASE_SIZE / 2 + logo_to_controls_spacing,
                }
            };
        }
    }
}
