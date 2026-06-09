// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    public partial class EzSkinEditorCollapsibleSection : Container
    {
        private readonly EzSkinEditorSidebarGroupDefinition definition;
        private Container contentContainer = null!;
        private bool expanded;

        public EzSkinEditorCollapsibleSection(EzSkinEditorSidebarGroupDefinition definition)
        {
            this.definition = definition;
            expanded = definition.ExpandedByDefault;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(4),
                Children = new Drawable[]
                {
                    new SectionHeaderButton(definition.Title.ToString(), () => setExpanded(!expanded)),
                    contentContainer = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Left = 5, Right = 5, Bottom = 8 },
                        Child = definition.CreateContent(),
                    },
                },
            };

            applyExpandedState();
        }

        private void setExpanded(bool value)
        {
            expanded = value;
            applyExpandedState();
        }

        private void applyExpandedState()
        {
            if (expanded)
                contentContainer.Show();
            else
                contentContainer.Hide();
        }

        private partial class SectionHeaderButton : OsuButton
        {
            public SectionHeaderButton(string title, System.Action action)
            {
                RelativeSizeAxes = Axes.X;
                Height = 28;
                Action = action;
                Text = title;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider, OsuColour colours)
            {
                BackgroundColour = colourProvider.Background4;
                Content.CornerRadius = 4;
            }
        }
    }
}
