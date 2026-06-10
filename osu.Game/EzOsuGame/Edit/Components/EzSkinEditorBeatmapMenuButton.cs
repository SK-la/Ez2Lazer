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
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Components
{
    public partial class EzSkinEditorBeatmapMenuButton : SkinEditorSceneLibrary.SceneButton, IHasPopover
    {
        public Action<RulesetInfo, EzBeatmapPreviewMode>? BeatmapPreviewRequested;

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
            Text = "实际谱面";
            Width = 110;
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
        private readonly Action<RulesetInfo, EzBeatmapPreviewMode>? onSelected;

        public BeatmapPreviewPopover(IRulesetStore rulesets, Action<RulesetInfo, EzBeatmapPreviewMode>? onSelected)
        {
            this.rulesets = rulesets;
            this.onSelected = onSelected;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new OsuScrollContainer
            {
                Width = 280,
                Height = 360,
                Child = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Y,
                    Width = 260,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Padding = new MarginPadding(10),
                    Children = buildSections().ToArray(),
                },
            };
        }

        private IEnumerable<Drawable> buildSections()
        {
            foreach (var ruleset in rulesets.AvailableRulesets.OfType<RulesetInfo>())
            {
                yield return new OsuSpriteText
                {
                    Text = ruleset.Name,
                    Font = OsuFont.Default.With(weight: FontWeight.Bold, size: 14),
                    Margin = new MarginPadding { Top = 4 },
                };

                if (!EzSkinEditorPreviewModes.SupportsBeatmapPreview(ruleset))
                {
                    yield return new OsuSpriteText
                    {
                        Text = "预览尚未支持",
                        Font = OsuFont.Default.With(size: 12),
                        Colour = Color4.Gray,
                        Margin = new MarginPadding { Bottom = 4 },
                    };

                    continue;
                }

                foreach (var mode in EzSkinEditorPreviewModes.GetAvailableModes(ruleset))
                {
                    var capturedRuleset = ruleset;
                    var capturedMode = mode;

                    yield return new ModeSelectButton(getModeLabel(mode), () =>
                    {
                        onSelected?.Invoke(capturedRuleset, capturedMode);
                        this.HidePopover();
                    });
                }
            }
        }

        private static LocalisableString getModeLabel(EzBeatmapPreviewMode mode) => mode switch
        {
            EzBeatmapPreviewMode.Dynamic => EzEnumStrings.DYNAMIC,
            EzBeatmapPreviewMode.Static => EzEnumStrings.STATIC,
            EzBeatmapPreviewMode.StaticFullMap => EzEnumStrings.STATIC_FULL_MAP,
            EzBeatmapPreviewMode.StaticScroll => EzEnumStrings.STATIC_SCROLL,
            _ => mode.ToString(),
        };

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
