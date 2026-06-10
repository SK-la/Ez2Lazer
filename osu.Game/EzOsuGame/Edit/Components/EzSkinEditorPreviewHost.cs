// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Hosts the virtual playback scene. With comparison enabled (size scene only),
    /// playback is on the left and Note/LN static preview is on the right.
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
            populateComparison();
        }

        private void populatePlayback()
        {
            if (context.PreviewSource == EzSkinEditorPreviewSource.Beatmap)
            {
                playbackContainer.Child = new EzSkinEditorBeatmapPreviewHost(context).With(d =>
                {
                    d.RelativeSizeAxes = Axes.Both;
                });
                return;
            }

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
                playbackContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_VIRTUAL_PLAYFIELD_NOT_SUPPORTED);
            }
        }

        private void populateComparison()
        {
            if (context.Provider != null)
            {
                comparisonContainer.Child = context.Provider.CreateStaticPart(context.EditorSkin).With(d =>
                {
                    d.RelativeSizeAxes = Axes.Both;
                    d.Anchor = Anchor.Centre;
                    d.Origin = Anchor.Centre;
                });
            }
            else
            {
                comparisonContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_COMPARISON_NOT_SUPPORTED);
            }
        }

        private static OsuSpriteText createPlaceholder(LocalisableString text) => new OsuSpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = text,
            Colour = Color4.White,
        };
    }
}
