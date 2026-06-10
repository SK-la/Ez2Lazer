// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Framework.Graphics.UserInterface;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    public partial class EzSkinEditorBeatmapMenuButton : SkinEditorSceneLibrary.SceneButton, IHasPopover
    {
        public Action<RulesetInfo>? BeatmapPreviewRequested;

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

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        public EzSkinEditorBeatmapMenuButton()
        {
            Text = EzEditorStrings.TOOLBAR_SELECT_MODE;
            Width = 100;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, OverlayColourProvider? overlayColourProvider)
        {
            this.colours = colours;
            colourProvider = overlayColourProvider;
            Action = this.ShowPopover;
            updateColours();
        }

        public Popover GetPopover() => new BeatmapPreviewPopover(rulesets, BeatmapPreviewRequested);

        private void updateColours()
        {
            BackgroundColour = active
                ? colours.YellowDark
                : colourProvider?.Background3 ?? colours.Blue3;
        }
    }

    public partial class BeatmapPreviewPopover : OsuPopover
    {
        private readonly IRulesetStore rulesets;
        private readonly Action<RulesetInfo>? onSelected;

        public BeatmapPreviewPopover(IRulesetStore rulesets, Action<RulesetInfo>? onSelected)
        {
            this.rulesets = rulesets;
            this.onSelected = onSelected;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new FillFlowContainer
            {
                Width = 220,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Children = buildRulesetButtons().ToArray(),
            };
        }

        private IEnumerable<Drawable> buildRulesetButtons()
        {
            foreach (var ruleset in rulesets.AvailableRulesets.OfType<RulesetInfo>())
            {
                if (!EzSkinEditorPreviewModes.SupportsBeatmapPreview(ruleset))
                    continue;

                var capturedRuleset = ruleset;

                yield return new ModeSelectButton(ruleset.Name, () =>
                {
                    onSelected?.Invoke(capturedRuleset);
                    this.HidePopover();
                });
            }
        }

        private partial class ModeSelectButton : OsuButton
        {
            public ModeSelectButton(LocalisableString text, Action action)
            {
                Text = text;
                Action = action;
                RelativeSizeAxes = Axes.X;
                Height = 32;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider? overlayColourProvider, OsuColour colours)
            {
                BackgroundColour = overlayColourProvider?.Background4 ?? colours.Blue3;
            }
        }
    }
}
