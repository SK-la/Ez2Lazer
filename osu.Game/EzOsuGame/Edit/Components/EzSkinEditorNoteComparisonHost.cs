// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Full-width live vs note-edit-snapshot comparison for the Note scene.
    /// </summary>
    public partial class EzSkinEditorNoteComparisonHost : Container
    {
        private readonly EzSkinEditorSceneContext context;

        private Container liveContainer = null!;
        private Container snapshotContainer = null!;

        public EzSkinEditorNoteComparisonHost(EzSkinEditorSceneContext context)
        {
            this.context = context;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Relative, 0.5f),
                    new Dimension(GridSizeMode.Relative, 0.5f),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        liveContainer = new Container { RelativeSizeAxes = Axes.Both },
                        snapshotContainer = new Container { RelativeSizeAxes = Axes.Both },
                    },
                },
            };

            populate();
        }

        public void RefreshPreviews() => populate();

        private void populate()
        {
            if (context.NoteSession == null || context.NoteSnapshot == null)
            {
                liveContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_NOTE_PREVIEW_NOT_AVAILABLE);
                snapshotContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_NOTE_PREVIEW_NOT_AVAILABLE);
                return;
            }

            var profile = EzSkinEditorNoteRulesetProfileRegistry.Get(context.NoteSession.Ruleset.Value);

            if (profile == null)
            {
                liveContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_NOTE_RULESET_NOT_SUPPORTED);
                snapshotContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_NOTE_RULESET_NOT_SUPPORTED);
                return;
            }

            liveContainer.Child = profile.CreateNotePreview(context.EditorSkin, context.NoteSession.ToPreviewRequest());
            snapshotContainer.Child = profile.CreateNotePreview(context.EditorSkin, context.NoteSnapshot.ToPreviewRequest());
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
