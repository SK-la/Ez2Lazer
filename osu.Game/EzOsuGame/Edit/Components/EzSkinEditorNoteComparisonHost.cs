// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
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
    /// Shared live vs snapshot note comparison for Note and Size scenes. Each side shows Note and LN together.
    /// Panes are reused across refresh so a single setting change only touches the drawables it actually has to.
    /// </summary>
    public partial class EzSkinEditorNoteComparisonHost : Container
    {
        private EzSkinEditorSceneContext context;

        private Container liveContainer = null!;
        private Container snapshotContainer = null!;
        private EzSkinEditorNoteComparisonPane? livePane;
        private EzSkinEditorNoteComparisonPane? snapshotPane;

        // The note edit session outlives scene switches, so bindings must be owned (and released) here.
        private readonly List<IUnbindable> sessionBindings = new List<IUnbindable>();

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
                        liveContainer = new Container { RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Right = 4 } },
                        snapshotContainer = new Container { RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Left = 4 } },
                    },
                },
            };

            populate();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (context.NoteSession == null)
                return;

            var session = context.NoteSession;

            bindSession(session.VariantId, () => refreshLivePane());
            bindSession(session.Ruleset, () => refreshLivePane());
            bindSession(session.NoteColour, () => applyLiveLayout());
            bindSession(session.Width, () => applyLiveLayout());
            bindSession(session.Height, () => applyLiveLayout());
        }

        private void bindSession<T>(Bindable<T> source, Action onChanged)
        {
            var copy = source.GetBoundCopy();
            copy.BindValueChanged(_ => onChanged());
            sessionBindings.Add(copy);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (var binding in sessionBindings)
                    binding.UnbindAll();

                sessionBindings.Clear();
            }

            base.Dispose(isDisposing);
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

        private void refreshLivePane()
        {
            var source = context.NoteComparisonSource;

            if (source == null)
            {
                showPlaceholder(liveContainer, EzEditorStrings.PLACEHOLDER_NOTE_PREVIEW_NOT_AVAILABLE);
                livePane = null;
                return;
            }

            var profile = EzSkinEditorNoteRulesetProfileRegistry.Get(source.Ruleset);

            if (profile == null)
            {
                showPlaceholder(liveContainer, EzEditorStrings.PLACEHOLDER_NOTE_RULESET_NOT_SUPPORTED);
                livePane = null;
                return;
            }

            livePane = getOrCreatePane(liveContainer, livePane);
            livePane.Apply(context.EditorSkin, profile, source.GetLiveRequest);
        }

        private void refreshSnapshotPane()
        {
            var source = context.NoteComparisonSource;

            if (source == null)
            {
                showPlaceholder(snapshotContainer, EzEditorStrings.PLACEHOLDER_NOTE_PREVIEW_NOT_AVAILABLE);
                snapshotPane = null;
                return;
            }

            var profile = EzSkinEditorNoteRulesetProfileRegistry.Get(source.Ruleset);

            if (profile == null)
            {
                showPlaceholder(snapshotContainer, EzEditorStrings.PLACEHOLDER_NOTE_RULESET_NOT_SUPPORTED);
                snapshotPane = null;
                return;
            }

            snapshotPane = getOrCreatePane(snapshotContainer, snapshotPane);
            snapshotPane.Apply(context.EditorSkin, profile, source.GetSnapshotRequest);
        }

        private void applyLiveLayout()
        {
            if (livePane == null || context.NoteComparisonSource == null)
                return;

            var profile = EzSkinEditorNoteRulesetProfileRegistry.Get(context.NoteComparisonSource.Ruleset);

            if (profile == null)
                return;

            livePane.Apply(context.EditorSkin, profile, context.NoteComparisonSource.GetLiveRequest);
        }

        private static EzSkinEditorNoteComparisonPane getOrCreatePane(Container container, EzSkinEditorNoteComparisonPane? current)
        {
            if (current != null && container.Child == current)
                return current;

            var pane = new EzSkinEditorNoteComparisonPane();
            container.Child = pane;
            return pane;
        }

        private static void showPlaceholder(Container container, LocalisableString text)
        {
            if (container.Child is OsuSpriteText sprite && sprite.Text.Equals(text))
                return;

            container.Child = createPlaceholder(text);
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
