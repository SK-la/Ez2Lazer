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
    /// Hosts virtual playback and comparison preview placeholders for appearance/size/colour scenes.
    /// </summary>
    public partial class EzSkinEditorPreviewHost : Container
    {
        private readonly EzSkinEditorSceneType sceneType;
        private readonly EzSkinEditorSceneContext context;
        private readonly bool playbackOnly;

        private Container playbackContainer = null!;
        private Container comparisonContainer = null!;

        public EzSkinEditorPreviewHost(EzSkinEditorSceneType sceneType, EzSkinEditorSceneContext context, bool playbackOnly = false)
        {
            this.sceneType = sceneType;
            this.context = context;
            this.playbackOnly = playbackOnly;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (playbackOnly)
            {
                InternalChild = playbackContainer = new Container { RelativeSizeAxes = Axes.Both };
                populatePlayback();
                return;
            }

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.Relative, 0.55f),
                    new Dimension(GridSizeMode.Relative, 0.45f),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        playbackContainer = new Container { RelativeSizeAxes = Axes.Both },
                    },
                    new Drawable[]
                    {
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
                    Text = $"{sceneType} — Note/LN 对比区（里程碑 2）",
                    Font = OsuFont.Default.With(size: 18, weight: FontWeight.Bold),
                    Colour = Color4.White,
                },
            };
        }
    }
}
