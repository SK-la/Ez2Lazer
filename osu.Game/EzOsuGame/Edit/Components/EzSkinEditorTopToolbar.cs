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

        public Bindable<EzSkinEditorPreviewSource>? PreviewSource { get; set; }

        public Bindable<EzBeatmapPreviewMode>? PreviewMode { get; set; }

        public BindableBool? LoopBeatmap { get; set; }

        /// <summary>Preview ruleset; drives which ruleset-specific buttons are present.</summary>
        public Bindable<RulesetInfo?>? Ruleset { get; set; }

        private BeatmapPlaybackButton playbackButton = null!;
        private EzSkinEditorBeatmapMenuButton beatmapButton = null!;
        private LoopBeatmapButton? loopButton;

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
        private void load()
        {
            Children = new Drawable[]
            {
                playbackButton = new BeatmapPlaybackButton(),
                beatmapButton = new EzSkinEditorBeatmapMenuButton
                {
                    BeatmapPreviewRequested = ruleset => BeatmapPreviewRequested?.Invoke(ruleset),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            playbackButton.Action = () => ToggleBeatmapPlaybackRequested?.Invoke();

            PreviewSource?.BindValueChanged(_ => updateActiveState(), true);
            PreviewMode?.BindValueChanged(_ => updateActiveState(), true);
            LoopBeatmap?.BindValueChanged(_ => updateActiveState(), true);
            Ruleset?.BindValueChanged(_ => updateActiveState(), true);
        }

        private void updateActiveState()
        {
            syncRulesetSpecificButtons();

            bool loop = LoopBeatmap?.Value == true;
            bool beatmapLoaded = PreviewSource?.Value == EzSkinEditorPreviewSource.Beatmap;
            bool canPlayback = beatmapLoaded || loop;
            bool playing = canPlayback && PreviewMode?.Value == EzBeatmapPreviewMode.Dynamic;

            playbackButton.Enabled.Value = canPlayback;
            playbackButton.ShowPausedState = canPlayback && !playing;

            beatmapButton.Active = beatmapLoaded && !loop;

            loopButton?.Active = loop;
        }

        /// <summary>
        /// Add/remove ruleset-only controls. Placement among children can be adjusted later.
        /// </summary>
        private void syncRulesetSpecificButtons()
        {
            if (isManiaLoopAvailable(Ruleset?.Value))
                ensureLoopButton();
            else
                removeLoopButton();
        }

        private void ensureLoopButton()
        {
            loopButton ??= new LoopBeatmapButton
            {
                Action = () =>
                {
                    LoopBeatmap?.Value = !LoopBeatmap.Value;
                },
            };

            if (loopButton.Parent != this)
                Add(loopButton);
        }

        private void removeLoopButton()
        {
            if (loopButton?.Parent == this)
                Remove(loopButton, false);

            if (LoopBeatmap?.Value == true)
                LoopBeatmap.Value = false;
        }

        /// <summary>
        /// Matches <see cref="SkinEditorProviderResolver.ResolveLoopProvider"/>:
        /// only mania is implemented; null ruleset defaults to mania.
        /// </summary>
        private static bool isManiaLoopAvailable(RulesetInfo? ruleset) =>
            ruleset is null || ruleset.OnlineID == 3;

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

        private partial class LoopBeatmapButton : SkinEditorSceneLibrary.SceneButton
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

            public LoopBeatmapButton()
            {
                Text = EzEditorStrings.TOOLBAR_LOOP_BEATMAP;
                Width = 110;
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
