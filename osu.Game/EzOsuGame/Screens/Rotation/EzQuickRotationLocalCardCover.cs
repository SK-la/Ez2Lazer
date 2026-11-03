// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osuTK;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    /// <summary>
    /// Ranked-play card cover using the beatmap's on-disk background, falling back to the game default background.
    /// </summary>
    public partial class EzQuickRotationLocalCardCover : CompositeDrawable
    {
        private readonly BeatmapInfo beatmap;
        private readonly ColourInfo gradientColour;

        public EzQuickRotationLocalCardCover(BeatmapInfo beatmap, ColourInfo gradientColour)
        {
            this.beatmap = beatmap;
            this.gradientColour = gradientColour;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(BeatmapManager beatmaps)
        {
            InternalChildren =
            [
                new BufferedContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    GrayscaleStrength = 0.25f,
                    Child = new EzQuickRotationLocalBeatmapBackgroundSprite(beatmap, beatmaps)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        FillMode = FillMode.Fill,
                        EdgeSmoothness = new Vector2(2),
                    },
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = gradientColour,
                },
            ];
        }

        private partial class EzQuickRotationLocalBeatmapBackgroundSprite : Sprite
        {
            private readonly BeatmapInfo beatmapInfo;
            private readonly BeatmapManager beatmaps;

            public EzQuickRotationLocalBeatmapBackgroundSprite(BeatmapInfo beatmapInfo, BeatmapManager beatmaps)
            {
                this.beatmapInfo = beatmapInfo;
                this.beatmaps = beatmaps;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var working = beatmaps.GetWorkingBeatmap(beatmapInfo);
                Texture = working.GetBackground() ?? beatmaps.DefaultBeatmap.GetBackground();
            }
        }
    }
}
