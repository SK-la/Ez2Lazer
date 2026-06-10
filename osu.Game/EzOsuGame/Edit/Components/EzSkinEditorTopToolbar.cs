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
        public Action? ToggleBeatmapPlaybackRequested { get; set; }

        public Action<RulesetInfo>? BeatmapPreviewRequested { get; set; }

        public Action? ClearBeatmapPreviewRequested { get; set; }

        public Bindable<EzSkinEditorPreviewSource>? PreviewSource { get; set; }

        public Bindable<EzBeatmapPreviewMode>? PreviewMode { get; set; }

        private BeatmapPlaybackButton playbackButton = null!;
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
                playbackButton = new BeatmapPlaybackButton(),
                beatmapButton = new EzSkinEditorBeatmapMenuButton
                {
                    BeatmapPreviewRequested = ruleset => BeatmapPreviewRequested?.Invoke(ruleset),
                    ClearBeatmapPreviewRequested = () => ClearBeatmapPreviewRequested?.Invoke(),
                },
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

            playbackButton.Action = () => ToggleBeatmapPlaybackRequested?.Invoke();

            PreviewSource?.BindValueChanged(_ => updateActiveState(), true);
            PreviewMode?.BindValueChanged(_ => updateActiveState(), true);
        }

        private void updateActiveState()
        {
            bool beatmapLoaded = PreviewSource?.Value == EzSkinEditorPreviewSource.Beatmap;
            bool playing = beatmapLoaded && PreviewMode?.Value == EzBeatmapPreviewMode.Dynamic;

            playbackButton.Enabled.Value = beatmapLoaded;
            playbackButton.ShowPausedState = beatmapLoaded && !playing;

            beatmapButton.Active = beatmapLoaded;
        }

        private partial class BeatmapPlaybackButton : SkinEditorSceneLibrary.SceneButton
        {
            public bool ShowPausedState
            {
                set => Text = value ? EzEditorStrings.TOOLBAR_PLAY_BEATMAP : EzEditorStrings.TOOLBAR_PAUSE_BEATMAP;
            }

            public BeatmapPlaybackButton()
            {
                Text = EzEditorStrings.TOOLBAR_PAUSE_BEATMAP;
                Width = 110;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider? overlayColourProvider, OsuColour colours)
            {
                BackgroundColour = overlayColourProvider?.Background3 ?? colours.Blue3;
                Content.CornerRadius = 5;
            }
        }
    }
}
