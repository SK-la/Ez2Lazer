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
    /// Children are reused when a refresh does not change what they are built from, so setting changes stay cheap.
    /// </summary>
    public partial class EzSkinEditorPreviewHost : Container
    {
        private EzSkinEditorSceneContext context;

        private Container playbackContainer = null!;
        private Container comparisonContainer = null!;

        private string? playbackToken;
        private object? playbackSource;
        private EzSkinEditorNoteComparisonHost? comparisonHost;

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
                if (!shouldRebuildPlayback("beatmap", context.PreviewBeatmap))
                    return;

                playbackContainer.Child = new EzSkinEditorBeatmapPreviewHost(context).With(d =>
                {
                    d.RelativeSizeAxes = Axes.Both;
                });
                return;
            }

            if (context.UseLoopBeatmap)
            {
                if (context.Provider != null && context.VirtualPreviewKeyCount > 0)
                {
                    // Provider factories hand out a fresh instance per resolve and hold no state, so only the type and key count identify the playfield.
                    if (!shouldRebuildPlayback($"virtual:{context.Provider.GetType().FullName}:{context.VirtualPreviewKeyCount}", null))
                        return;

                    playbackContainer.Child = context.Provider.CreateDynamicPart(context.EditorSkin, context.VirtualPreviewKeyCount)
                                                     .With(d => d.RelativeSizeAxes = Axes.Both);
                    return;
                }

                if (!shouldRebuildPlayback("loop-unsupported", context.PreviewRuleset))
                    return;

                playbackContainer.Child = createPlaceholder(EzEditorStrings.TOOLBAR_NOT_SUPPORTED);
                return;
            }

            if (!shouldRebuildPlayback("beatmap-missing", null))
                return;

            playbackContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_BEATMAP_NOT_LOADED);
        }

        /// <summary>
        /// Playback is rebuilt only when what it is built from changes. Setting changes reach the playfield through its own
        /// bindables, so recreating it per tick would only add allocation and layout churn.
        /// </summary>
        private bool shouldRebuildPlayback(string token, object? source)
        {
            if (playbackToken == token && ReferenceEquals(playbackSource, source))
                return false;

            playbackToken = token;
            playbackSource = source;
            return true;
        }

        private void populateComparison()
        {
            if (context.NoteComparisonSource == null)
            {
                comparisonHost = null;

                if (comparisonContainer.Child is not OsuSpriteText)
                    comparisonContainer.Child = createPlaceholder(EzEditorStrings.TOOLBAR_NOT_SUPPORTED);

                return;
            }

            if (comparisonHost != null && comparisonContainer.Child == comparisonHost)
            {
                comparisonHost.UpdateContext(context);
                return;
            }

            comparisonHost = new EzSkinEditorNoteComparisonHost(context);
            comparisonContainer.Child = comparisonHost;
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
