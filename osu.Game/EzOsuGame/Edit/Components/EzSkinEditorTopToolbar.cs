// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    public partial class EzSkinEditorTopToolbar : FillFlowContainer
    {
        public Action? StaticPreviewRequested { get; set; }

        public Action<RulesetInfo, EzBeatmapPreviewMode>? BeatmapPreviewRequested { get; set; }

        public Bindable<EzSkinEditorPreviewSource>? PreviewSource { get; set; }

        private StaticPreviewButton staticButton = null!;
        private EzSkinEditorBeatmapMenuButton beatmapButton = null!;

        public EzSkinEditorTopToolbar()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.CentreRight;
            Origin = Anchor.CentreRight;
            Direction = FillDirection.Horizontal;
            Spacing = new Vector2(8);
            Padding = new MarginPadding { Horizontal = 10, Vertical = 5 };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            Children = new Drawable[]
            {
                staticButton = new StaticPreviewButton
                {
                    Action = () => StaticPreviewRequested?.Invoke(),
                },
                beatmapButton = new EzSkinEditorBeatmapMenuButton
                {
                    BeatmapPreviewRequested = (ruleset, mode) => BeatmapPreviewRequested?.Invoke(ruleset, mode),
                },
                new EzSkinEditorSkinDropdown(),
                new OsuTextFlowContainer
                {
                    TextAnchor = Anchor.CentreLeft,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    Margin = new MarginPadding { Left = 6 },
                }.With(t =>
                {
                    t.AddText(@"Ez ", cp => cp.Font = OsuFont.TorusAlternate);
                    t.AddText(@"Skin Editor", cp =>
                    {
                        cp.Font = OsuFont.TorusAlternate;
                        cp.Colour = colourProvider.Highlight1;
                    });
                }),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            PreviewSource?.BindValueChanged(_ => updateActiveState(), true);
        }

        private void updateActiveState()
        {
            if (PreviewSource == null)
                return;

            staticButton.Active = PreviewSource.Value == EzSkinEditorPreviewSource.Static;
            beatmapButton.Active = PreviewSource.Value == EzSkinEditorPreviewSource.Beatmap;
        }

        private partial class StaticPreviewButton : SkinEditorSceneLibrary.SceneButton
        {
            private bool active;

            public bool Active
            {
                get => active;
                set
                {
                    active = value;
                    if (IsLoaded)
                        updateColours();
                }
            }

            private OsuColour colours = null!;
            private OverlayColourProvider? colourProvider;

            public StaticPreviewButton()
            {
                Text = EzEditorStrings.TOOLBAR_STATIC_SKIN;
                Width = 100;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours, OverlayColourProvider? overlayColourProvider)
            {
                this.colours = colours;
                colourProvider = overlayColourProvider;
                updateColours();
            }

            private void updateColours()
            {
                BackgroundColour = active
                    ? colours.YellowDark
                    : colourProvider?.Background3 ?? colours.Blue3;
            }
        }
    }
}
