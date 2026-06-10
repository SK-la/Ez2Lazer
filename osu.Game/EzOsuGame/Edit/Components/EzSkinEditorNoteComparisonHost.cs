// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Shared live vs snapshot note comparison for Note, Size and Colour scenes.
    /// </summary>
    public partial class EzSkinEditorNoteComparisonHost : Container
    {
        private EzSkinEditorSceneContext context;

        private Container liveContainer = null!;
        private Container snapshotContainer = null!;
        private EzSkinEditorNoteMemoryPreview? livePreview;
        private EzSkinEditorNoteMemoryPreview? snapshotPreview;

        public EzSkinEditorNoteComparisonHost(EzSkinEditorSceneContext context)
        {
            this.context = context;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var compareKind = context.NoteCompareKind ?? new Bindable<EzSkinEditorNoteCompareKind>();

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    new SettingsEnumDropdown<EzSkinEditorNoteCompareKind>
                    {
                        LabelText = EzEditorStrings.NOTE_COMPARE_KIND_LABEL,
                        Current = compareKind,
                    },
                    new GridContainer
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
                    },
                },
            };

            compareKind.BindValueChanged(_ => populate(), false);
            populate();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (context.NoteSession == null)
                return;

            context.NoteSession.VariantId.BindValueChanged(_ => refreshLivePane(), false);
            context.NoteSession.Ruleset.BindValueChanged(_ => refreshLivePane(), false);
            context.NoteSession.NoteColour.BindValueChanged(_ => applyLiveLayout(), false);
            context.NoteSession.Width.BindValueChanged(_ => applyLiveLayout(), false);
            context.NoteSession.Height.BindValueChanged(_ => applyLiveLayout(), false);
        }

        public void UpdateContext(EzSkinEditorSceneContext newContext)
        {
            context = newContext;
            populate();
        }

        public void RefreshSnapshotPane() => refreshSnapshotPane();

        private void populate()
        {
            refreshLivePane();
            refreshSnapshotPane();
        }

        private EzSkinEditorNoteCompareKind currentCompareKind =>
            context.NoteCompareKind?.Value ?? EzSkinEditorNoteCompareKind.Tap;

        private void refreshLivePane()
        {
            var source = context.NoteComparisonSource;

            if (source == null)
            {
                liveContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_NOTE_PREVIEW_NOT_AVAILABLE);
                livePreview = null;
                return;
            }

            var profile = EzSkinEditorNoteRulesetProfileRegistry.Get(source.Ruleset);

            if (profile == null)
            {
                liveContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_NOTE_RULESET_NOT_SUPPORTED);
                livePreview = null;
                return;
            }

            livePreview ??= attachPreview(liveContainer);
            livePreview.Apply(context.EditorSkin, profile, source.GetLiveRequest(currentCompareKind));
        }

        private void refreshSnapshotPane()
        {
            var source = context.NoteComparisonSource;

            if (source == null)
            {
                snapshotContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_NOTE_PREVIEW_NOT_AVAILABLE);
                snapshotPreview = null;
                return;
            }

            var profile = EzSkinEditorNoteRulesetProfileRegistry.Get(source.Ruleset);

            if (profile == null)
            {
                snapshotContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_NOTE_RULESET_NOT_SUPPORTED);
                snapshotPreview = null;
                return;
            }

            snapshotPreview ??= attachPreview(snapshotContainer);
            snapshotPreview.Apply(context.EditorSkin, profile, source.GetSnapshotRequest(currentCompareKind));
        }

        private void applyLiveLayout()
        {
            if (livePreview == null || context.NoteComparisonSource == null)
                return;

            var profile = EzSkinEditorNoteRulesetProfileRegistry.Get(context.NoteComparisonSource.Ruleset);

            if (profile == null)
                return;

            livePreview.Apply(context.EditorSkin, profile, context.NoteComparisonSource.GetLiveRequest(currentCompareKind));
        }

        private static EzSkinEditorNoteMemoryPreview attachPreview(Container container)
        {
            var preview = new EzSkinEditorNoteMemoryPreview();
            container.Child = preview;
            return preview;
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
