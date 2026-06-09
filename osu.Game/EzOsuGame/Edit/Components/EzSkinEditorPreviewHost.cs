// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Hosts the virtual playback scene. With comparison enabled (size scene only),
    /// playback is on the left and the before/after comparison placeholder is on the right.
    /// </summary>
    public partial class EzSkinEditorPreviewHost : Container
    {
        private readonly EzSkinEditorSceneContext context;
        private readonly bool showComparison;

        private Container playbackContainer = null!;
        private Container comparisonContainer = null!;

        public EzSkinEditorPreviewHost(EzSkinEditorSceneContext context, bool showComparison = false)
        {
            this.context = context;
            this.showComparison = showComparison;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (!showComparison)
            {
                InternalChild = playbackContainer = new Container { RelativeSizeAxes = Axes.Both };
                populatePlayback();
                return;
            }

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Relative, 0.55f),
                    new Dimension(GridSizeMode.Relative, 0.45f),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        playbackContainer = new Container { RelativeSizeAxes = Axes.Both },
                        comparisonContainer = new Container { RelativeSizeAxes = Axes.Both },
                    },
                },
            };

            populatePlayback();
            populateComparisonPlaceholder();
        }

        private void populatePlayback()
        {
            if (context.Provider != null)
            {
                playbackContainer.Child = context.Provider.CreateDynamicPart(context.EditorSkin).With(d =>
                {
                    d.RelativeSizeAxes = Axes.Both;
                    d.Anchor = Anchor.Centre;
                    d.Origin = Anchor.Centre;
                });
            }
            else
            {
                playbackContainer.Child = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = "Virtual playfield not supported",
                    Colour = Color4.White,
                };
            }
        }

        private void populateComparisonPlaceholder()
        {
            comparisonContainer.Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.35f),
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = "Note/LN 对比区（里程碑 2）",
                    Font = OsuFont.Default.With(size: 18, weight: FontWeight.Bold),
                    Colour = Color4.White,
                },
            };
        }
    }
}
