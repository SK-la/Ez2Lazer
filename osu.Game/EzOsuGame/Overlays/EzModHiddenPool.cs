// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzModHiddenPool : CompositeDrawable
    {
        public Action<string, Vector2>? DragStarted { get; set; }

        public Action<Vector2>? Dragged { get; set; }

        public Action? DragEnded { get; set; }

        private readonly FillFlowContainer iconFlow;
        private readonly Box background;

        private IReadOnlyList<ModState> mods = Array.Empty<ModState>();
        private string searchTerm = string.Empty;

        public EzModHiddenPool()
        {
            RelativeSizeAxes = Axes.X;
            Height = 96;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            Alpha = 0;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.Black.Opacity(0.45f),
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Margin = new MarginPadding { Left = 18 },
                    Font = OsuFont.TorusAlternate.With(size: 16, weight: FontWeight.SemiBold),
                    Text = EzModLayoutStrings.HIDDEN_MODS,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Left = 150, Right = 16, Vertical = 8 },
                    Child = new OsuScrollContainer(Direction.Horizontal)
                    {
                        RelativeSizeAxes = Axes.Both,
                        ScrollbarOverlapsContent = false,
                        Child = iconFlow = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.X,
                            RelativeSizeAxes = Axes.Y,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8, 0),
                        },
                    },
                },
            };
        }

        public string SearchTerm
        {
            set
            {
                if (searchTerm == value)
                    return;

                searchTerm = value;
                rebuild();
            }
        }

        public void SetMods(IReadOnlyList<ModState> mods)
        {
            this.mods = mods;
            rebuild();
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) =>
            Alpha > 0.9f && base.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnClick(ClickEvent e) => true;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            // Keep the bar distinguishable from the column area without taking a theme layout colour.
            background.Colour = colours.Background6.Opacity(0.92f);
        }

        private void rebuild()
        {
            iconFlow.Clear();

            foreach (var state in mods)
            {
                if (!matches(state))
                    continue;

                iconFlow.Add(new PoolIcon(state, DragStarted, Dragged, DragEnded));
            }
        }

        private bool matches(ModState state)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return true;

            return state.Mod.Acronym.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                   || state.Mod.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
        }

        private partial class PoolIcon : CompositeDrawable
        {
            private readonly Action<string, Vector2>? dragStarted;
            private readonly Action<Vector2>? dragged;
            private readonly Action? dragEnded;
            private readonly ModState state;

            public PoolIcon(ModState state, Action<string, Vector2>? dragStarted, Action<Vector2>? dragged, Action? dragEnded)
            {
                this.state = state;
                this.dragStarted = dragStarted;
                this.dragged = dragged;
                this.dragEnded = dragEnded;

                Size = new Vector2(54, 68);
                InternalChildren = new Drawable[]
                {
                    new ModSwitchSmall(state.Mod)
                    {
                        Active = { BindTarget = state.Active },
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Size = new Vector2(46),
                    },
                    new OsuSpriteText
                    {
                        Text = state.Mod.Acronym,
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                    },
                };
            }

            protected override bool OnDragStart(DragStartEvent e)
            {
                dragStarted?.Invoke(state.Mod.Acronym, e.ScreenSpaceMousePosition);
                return true;
            }

            protected override void OnDrag(DragEvent e) => dragged?.Invoke(e.ScreenSpaceMousePosition);

            protected override void OnDragEnd(DragEndEvent e) => dragEnded?.Invoke();

            protected override bool OnClick(ClickEvent e) => true;
        }
    }
}
