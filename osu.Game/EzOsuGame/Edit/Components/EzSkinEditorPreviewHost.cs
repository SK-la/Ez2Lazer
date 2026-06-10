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
    /// Hosts the virtual playback scene. With comparison enabled (size scene),
    /// playback is on the left and Note/LN static preview is on the right.
    /// </summary>
    public partial class EzSkinEditorPreviewHost : Container
    {
        private EzSkinEditorSceneContext context;

        private Container playbackContainer = null!;
        private Container comparisonContainer = null!;

        public EzSkinEditorPreviewHost(EzSkinEditorSceneContext context)
        {
            this.context = context;
            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Rebuilds the playback area from updated context (e.g. key mode change) without recreating this host.
        /// </summary>
        public void RefreshFromContext(EzSkinEditorSceneContext newContext)
        {
            context = newContext;

            if (!IsLoaded)
                return;

            populatePlayback();

            if (context.UseVirtualComparisonPreview)
                populateComparison();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (!context.UseVirtualComparisonPreview)
            {
                InternalChild = playbackContainer = new Container { RelativeSizeAxes = Axes.Both, Masking = true };
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
                        playbackContainer = new Container { RelativeSizeAxes = Axes.Both, Masking = true },
                        comparisonContainer = new Container { RelativeSizeAxes = Axes.Both },
                    },
                },
            };

            populatePlayback();
            populateComparison();
        }

        private void populatePlayback()
        {
            if (context.AllowBeatmapPreview && context.PreviewSource == EzSkinEditorPreviewSource.Beatmap)
            {
                playbackContainer.Child = new EzSkinEditorBeatmapPreviewHost(context).With(d =>
                {
                    d.RelativeSizeAxes = Axes.Both;
                });
                return;
            }

            if (context.VirtualPreviewKeyCount <= 0)
            {
                playbackContainer.Child = createPlaceholder(EzEditorStrings.SELECT_KEY_MODE_FIRST);
                return;
            }

            if (context.Provider != null)
            {
                playbackContainer.Child = context.Provider.CreateDynamicPart(context.EditorSkin, context.VirtualPreviewKeyCount)
                    .With(d => d.RelativeSizeAxes = Axes.Both);
            }
            else
            {
                playbackContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_VIRTUAL_PLAYFIELD_NOT_SUPPORTED);
            }
        }

        private void populateComparison()
        {
            if (context.NoteComparisonSource == null)
            {
                comparisonContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_COMPARISON_NOT_SUPPORTED);
                return;
            }

            comparisonContainer.Child = new EzSkinEditorNoteComparisonHost(context);
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
