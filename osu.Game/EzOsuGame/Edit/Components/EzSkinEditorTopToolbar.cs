// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    public partial class EzSkinEditorTopToolbar : FillFlowContainer
    {
        public Action<RulesetInfo>? BeatmapPreviewRequested { get; set; }

        public Bindable<EzSkinEditorPreviewSource>? PreviewSource { get; set; }

        public Bindable<EzBeatmapPreviewMode>? PreviewMode { get; set; }

        public BindableBool? LoopBeatmap { get; set; }

        /// <summary>Preview ruleset; drives which ruleset-specific buttons are present.</summary>
        public Bindable<RulesetInfo?>? Ruleset { get; set; }

        private EzSkinEditorBeatmapMenuButton beatmapButton = null!;
        // private LoopBeatmapButton? loopButton;

        public EzSkinEditorTopToolbar()
        {
            AutoSizeAxes = Axes.X;
            RelativeSizeAxes = Axes.Y;
            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;
            Direction = FillDirection.Horizontal;
            Spacing = new Vector2(8);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                beatmapButton = new EzSkinEditorBeatmapMenuButton
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    BeatmapPreviewRequested = ruleset => BeatmapPreviewRequested?.Invoke(ruleset),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            PreviewSource?.BindValueChanged(_ => updateActiveState(), true);
            PreviewMode?.BindValueChanged(_ => updateActiveState(), true);
            LoopBeatmap?.BindValueChanged(_ => updateActiveState(), true);
            Ruleset?.BindValueChanged(_ => updateActiveState(), true);
        }

        private void updateActiveState()
        {
            // Loop beatmap button temporarily disabled — keep loop off when ruleset cannot use it.
            // syncRulesetSpecificButtons();
            if (!isManiaLoopAvailable(Ruleset?.Value) && LoopBeatmap?.Value == true)
                LoopBeatmap.Value = false;

            bool loop = LoopBeatmap?.Value == true;
            bool beatmapLoaded = PreviewSource?.Value == EzSkinEditorPreviewSource.Beatmap;

            beatmapButton.Active = beatmapLoaded && !loop;

            // loopButton?.Active = loop;
        }

        /*
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
        */

        /// <summary>
        /// Matches <see cref="SkinEditorProviderResolver.ResolveLoopProvider"/>:
        /// only mania is implemented; null ruleset defaults to mania.
        /// </summary>
        private static bool isManiaLoopAvailable(RulesetInfo? ruleset) =>
            ruleset is null || ruleset.OnlineID == 3;

        /*
        private partial class LoopBeatmapButton : SkinEditorSceneLibrary.SceneButton
        {
            public LoopBeatmapButton()
            {
                Text = EzEditorStrings.TOOLBAR_LOOP_BEATMAP;
                Width = 110;
            }
        }
        */
    }
}
